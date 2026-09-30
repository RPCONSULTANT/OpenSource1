using MediatR;
using OpenSource1.Application.Data;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.FacturasVenta.Borradores;
using OpenSource1.Application.Features.FacturasVenta.Calculo;
using OpenSource1.Application.Services.Clientes;
using OpenSource1.Application.Services.Contabilidad;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Application.Services.Registro;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Entities.Ventas;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.FacturasVenta.Posteo;

/// <summary>
/// Motor de posteo de facturas de venta (spec 6.5 con las desviaciones de la Fase 6). Todo ocurre en UNA transacción de
/// <see cref="IUnitOfWork"/> (EF y Dapper comparten la conexión): un <c>Result</c> fallido o una excepción deshacen TODO, incluido
/// el número de la serie configurada para el tipo <c>FacturaVenta</c>, que así nunca deja huecos.
/// <list type="number">
/// <item>Bloqueo del borrador (<c>FOR UPDATE</c>) y de sus líneas: dos posteos del mismo borrador se serializan y el segundo lo
/// ve ya borrado (404). Un borrador Abierta o Liberada se puede postear: liberar es una revisión opcional, no un requisito.</item>
/// <item>Bloqueo compartido de los socios vender-a y facturar-a (contra su borrado concurrente) y de los productos (orden único,
/// antes de revalidarlos: el factor y la unidad base no pueden cambiar hasta el commit).</item>
/// <item>Revalidación contra el estado ACTUAL (socios, término, productos, almacenes, unidades y factor congelado, cuentas de
/// líneas CuentaContable, IVA coherente, fecha de registro permitida — Task 8.5) y <b>derivación de TODAS las cuentas</b> (CxC, Ventas, IVA, inventario) antes de
/// escribir nada: cualquier fallo devuelve todos los errores (con el número de línea) sin haber intentado un solo INSERT.</item>
/// <item>IVA agrupado (<see cref="CalculadoraIvaFactura"/>).</item>
/// <item>Número de la serie de facturas (tipo <c>FacturaVenta</c>; si la línea alcanza su número de aviso, el resultado trae la
/// advertencia), salidas de inventario (Venta), movimiento de cliente (facturar-a), asiento contable y, al
/// final, el documento posteado con su <c>RegistroContableId</c> (sin UPDATE de la factura). Borrado lógico del borrador.</item>
/// <item>Total 0 (Task 8.4: todas las líneas al 100 % de descuento, un regalo): se postea el documento (líneas y líneas de IVA de
/// base 0) y sale el inventario, pero NO hay movimiento de cliente ni asiento (<c>RegistroContableId</c> null, número
/// de asiento sin consumir). Las demás validaciones y derivaciones (CxC, Ventas, IVA, inventario) se exigen igual.</item>
/// </list>
/// <para>
/// Orden GLOBAL de locks: borrador → líneas → socios (compartido) → productos (ordenados) → serie de facturas (<c>FOR SHARE</c>) y su línea →
/// almacenes (compartidos, dentro de <see cref="IRegistroMovimientosInventario.RegistrarAsync"/>) → cuentas (<c>FOR SHARE</c>,
/// orden de Id, dentro de <see cref="IRegistroContable"/>) → serie de asientos y su línea.
/// </para>
/// </summary>
public sealed class PostearFacturaVentaCommandHandler(
    IUnitOfWork unitOfWork,
    IFacturaVentaBorradorBloqueoService borradorBloqueo,
    IFacturaVentaBorradorDatos borradorDatos,
    IPosteoFacturaVentaDatos datos,
    IConversionUnidadMedidaService conversion,
    IDerivadorCuentas derivador,
    IRegistroMovimientosInventario registroInventario,
    IRegistroMovimientosCliente registroClientes,
    IRegistroContable registroContable,
    IGeneradorNumeroDocumento generadorNumero,
    IUsuarioActual usuario,
    IValidadorFechaRegistro validadorFecha)
    : IRequestHandler<PostearFacturaVentaCommand, Result<ResultadoPosteoFactura>>
{
    private const int LongitudNumero = 20;
    private const int LongitudCreadoPor = 100;
    private const int LongitudDescripcion = 200;

    public async Task<Result<ResultadoPosteoFactura>> Handle(PostearFacturaVentaCommand request, CancellationToken cancellationToken)
    {
        // Garantiza su propia atomicidad (ver PostearFacturaVentaCommand): dentro de otra transacción, un "return Fallo" no
        // desharía nada y el CommitAsync confirmaría la transacción del llamador.
        if (unitOfWork.HayTransaccionActiva)
        {
            throw new InvalidOperationException(
                "PostearFacturaVenta debe ejecutarse fuera de otra transacción: garantiza su propia atomicidad.");
        }

        // Sin CommitAsync, salir del "await using" deshace la transacción y libera los bloqueos.
        await using var transaccion = await unitOfWork.BeginTransactionAsync(cancellationToken);

        // 1. Borrador y líneas (FOR UPDATE).
        if (await borradorBloqueo.BloquearYObtenerEstadoAsync(request.FacturaVentaBorradorId, cancellationToken) is null)
        {
            return Fallo(FacturaVentaBorradorErrores.BorradorNoEncontrado());
        }

        var repositorio = unitOfWork.Repository<FacturaVentaBorrador>();
        var borrador = await repositorio.FirstOrDefaultAsync(
            x => x.Id == request.FacturaVentaBorradorId, asTracking: true, cancellationToken: cancellationToken);
        if (borrador is null)
        {
            // Imposible con la fila bloqueada.
            return Fallo(FacturaVentaBorradorErrores.BorradorNoEncontrado());
        }

        var lineas = await datos.BloquearLineasAsync(borrador.Id, cancellationToken);
        var lineasConImporte = lineas.Where(l => l.Tipo != TipoLineaFactura.Comentario).ToList();
        if (lineasConImporte.Count == 0)
        {
            return Fallo(new Error(
                "factura.sin_lineas", "El borrador no tiene líneas de producto ni de cuenta contable que postear.", "Id"));
        }

        // 2. Socios (compartido) y productos (orden único) antes de revalidarlos.
        await datos.BloquearSociosAsync([borrador.SocioNegocioId, borrador.SocioNegocioFacturarAId], cancellationToken);
        var lineasProducto = lineasConImporte.Where(l => l.Tipo == TipoLineaFactura.Producto).ToList();
        await registroInventario.BloquearProductosAsync(
            lineasProducto.Where(l => l.ProductoId is not null).Select(l => l.ProductoId!.Value), cancellationToken);

        // 3. Revalidación y derivación de TODAS las cuentas: nada se escribe hasta que no quede ningún error.
        var errores = new List<Error>();
        await ValidarCabeceraAsync(borrador, errores, cancellationToken);
        var lineasValidas = new List<LineaFacturaAPostear>();
        foreach (var linea in lineasConImporte)
        {
            var antes = errores.Count;
            await ValidarLineaAsync(linea, errores, cancellationToken);
            if (errores.Count == antes)
            {
                lineasValidas.Add(linea);
            }
        }

        ValidarIvaCoherente(lineasConImporte, errores);

        // Solo se derivan las cuentas de las líneas sin error (como mucho un error por línea). Si alguna línea falló, el
        // posteo termina aquí de todos modos, así que las cuentas parciales nunca se usan.
        var cuentas = await DerivarCuentasAsync(borrador, lineasValidas, errores, cancellationToken);
        if (errores.Count > 0)
        {
            return Fallo([.. errores]);
        }

        // 4. IVA agrupado por identificador (spec 6.3) y asiento, en memoria.
        var totales = CalculadoraIvaFactura.Calcular(
        [
            .. lineasConImporte.Select(l => new LineaCalculoIva(l.NumeroLinea, l.IdentificadorIva!, l.PorcentajeIva, l.ImporteLinea))
        ]);
        // Total 0 (regalo): sin movimiento de cliente ni asiento. Las líneas ya se revalidaron (precio > 0, importe 0 solo al 100 %).
        var conImporte = totales.ImporteTotal != 0m;

        // 5. Número de la factura (FOR UPDATE de la línea de la serie de facturas, sin huecos: se deshace con todo lo demás).
        var numeroResultado = await generadorNumero.SiguientePorTipoAsync(
            TipoDocumentoSerie.FacturaVenta, DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken);
        if (!numeroResultado.TryObtenerValor(out var generado))
        {
            return Result<ResultadoPosteoFactura>.Fallo(numeroResultado);
        }

        var numero = generado.Numero;

        if (numero.Length > LongitudNumero)
        {
            return Fallo(new Error(
                "factura.serie_invalida", $"El número de factura generado supera los {LongitudNumero} caracteres."));
        }

        var descripcion = Truncar(borrador.Descripcion ?? $"Factura de venta {numero}");
        var asiento = conImporte ? AsientoFacturaVenta.Construir(borrador, lineasConImporte, totales, cuentas, numero, descripcion) : null;

        // 6. Salidas de inventario por línea de Producto (en orden de línea). Existencia insuficiente -> Lineas[n].Cantidad.
        var movimientos = new Dictionary<Guid, long>();
        foreach (var linea in lineasProducto)
        {
            var salida = await registroInventario.RegistrarAsync(new MovimientoInventarioSolicitud(
                linea.ProductoId!.Value,
                linea.AlmacenId!.Value,
                TipoMovimientoInventario.Venta,
                linea.Cantidad,
                EsEntrada: false,
                linea.UnidadMedidaId!.Value,
                CostoUnitario: null,
                borrador.FechaRegistro,
                borrador.FechaDocumento,
                TipoDocumentoInventario.FacturaVenta,
                numero,
                linea.NumeroLinea,
                TipoOrigenMovimiento.FacturaVenta,
                numero,
                borrador.SocioNegocioId,
                linea.ImporteLinea), cancellationToken);
            if (!salida.TryObtenerValor(out var registrado))
            {
                return Fallo(DeLinea(linea, salida.Errores[0]));
            }

            movimientos[linea.Id] = registrado.MovimientoProductoId;
        }

        // 7-8. Libro de clientes (la factura al facturar-a, con la CxC congelada) y asiento; nada de eso con total 0.
        AsientoRegistrado? asientoRegistrado = null;
        if (asiento is not null)
        {
            var registrado = await RegistrarClienteYAsientoAsync(borrador, numero, descripcion, totales, cuentas, asiento, cancellationToken);
            if (!registrado.TryObtenerValor(out asientoRegistrado))
            {
                return Result<ResultadoPosteoFactura>.Fallo(registrado);
            }
        }

        // 9. Documento legal, ya con el asiento si lo hay (se inserta el último: nunca se actualiza).
        var ahora = DateTimeOffset.UtcNow;
        var creadoPor = CreadoPor();
        var factura = CrearFactura(borrador, numero, totales, asientoRegistrado?.RegistroContableId, ahora, creadoPor, usuario.Id);
        await datos.InsertarFacturaAsync(
            factura,
            [.. lineas.Select(l => CrearLinea(numero, l, movimientos.TryGetValue(l.Id, out var m) ? m : null))],
            [.. totales.Grupos.Select(g => new LineaIvaFacturaVenta
            {
                FacturaVentaNumero = numero,
                IdentificadorIva = g.IdentificadorIva,
                PorcentajeIva = g.PorcentajeIva,
                BaseImponible = g.BaseImponible,
                ImporteIva = g.ImporteIva,
                CuentaIvaId = cuentas.CuentaIvaPorIdentificador[g.IdentificadorIva],
            })],
            cancellationToken);

        // 10. Borrado lógico del borrador y de TODAS sus líneas (incluidas las de comentario).
        var borradas = await borradorDatos.BorrarLineasAsync(borrador.Id, creadoPor, cancellationToken);
        if (borradas != lineas.Count)
        {
            // Imposible con las líneas bloqueadas FOR UPDATE: abortar antes que dejar líneas posteadas vivas.
            throw new InvalidOperationException(
                $"Se esperaba borrar {lineas.Count} líneas del borrador {borrador.Numero} y se borraron {borradas}.");
        }

        repositorio.Remove(borrador);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result<ResultadoPosteoFactura>.Exito(
            new ResultadoPosteoFactura(numero, totales.ImporteTotal, asientoRegistrado?.NumeroRegistro, generado.Aviso));
    }

    /// <summary>Movimiento de cliente del facturar-a (con la CxC congelada) y asiento de la factura (pasos 7 y 8).</summary>
    private async Task<Result<AsientoRegistrado>> RegistrarClienteYAsientoAsync(
        FacturaVentaBorrador borrador,
        string numero,
        string descripcion,
        TotalesFactura totales,
        CuentasFactura cuentas,
        AsientoContable asiento,
        CancellationToken cancellationToken)
    {
        var movimientoCliente = await registroClientes.RegistrarAsync(new MovimientoClienteSolicitud(
            borrador.SocioNegocioFacturarAId,
            borrador.FechaRegistro,
            borrador.FechaDocumento,
            borrador.FechaVencimiento,
            TipoDocumentoCliente.Factura,
            numero,
            descripcion,
            totales.ImporteTotal,
            borrador.GrupoClienteContableId,
            cuentas.CuentaCxCId,
            TipoOrigenMovimiento.FacturaVenta,
            numero), cancellationToken);
        if (movimientoCliente.EsFallo)
        {
            return Result<AsientoRegistrado>.Fallo(movimientoCliente);
        }

        // IRegistroContable comprueba el cuadre antes y después de insertar.
        var registro = await registroContable.RegistrarAsync(asiento, cancellationToken);
        if (registro.EsExito)
        {
            return registro;
        }

        // Los campos "Lineas[i].X" de IRegistroContable indexan las PATAS del asiento (base 0), no las líneas de la factura: se
        // propagan sin ese campo para no confundirlos con Lineas[NumeroLinea].
        return Result<AsientoRegistrado>.Fallo([.. registro.Errores.Select(e =>
            e.Campo is { } campo && campo.StartsWith("Lineas[", StringComparison.Ordinal) ? e with { Campo = null } : e)]);
    }

    /// <summary>Socios existentes y no bloqueados para facturar (Facturacion ni Todo); término de pago existente.</summary>
    private async Task ValidarCabeceraAsync(FacturaVentaBorrador borrador, List<Error> errores, CancellationToken cancellationToken)
    {
        // Fechas de registro permitidas (Task 8.5): la de la cabecera, contra el rango del usuario o el general.
        var fecha = await validadorFecha.ValidarAsync(borrador.FechaRegistro, cancellationToken);
        errores.AddRange(fecha.Errores);

        var socios = unitOfWork.Repository<SocioNegocio>();
        var venderA = await socios.FirstOrDefaultAsync(x => x.Id == borrador.SocioNegocioId, cancellationToken: cancellationToken);
        if (venderA is null || venderA.Bloqueado != BloqueoSocioNegocio.Ninguno)
        {
            errores.Add(new Error(
                "factura.socio_invalido", "El socio vender-a no existe o está bloqueado para facturación.", "SocioNegocioId"));
        }

        // Un solo error si el facturar-a es el mismo socio.
        var facturarA = borrador.SocioNegocioFacturarAId == borrador.SocioNegocioId
            ? venderA
            : await socios.FirstOrDefaultAsync(x => x.Id == borrador.SocioNegocioFacturarAId, cancellationToken: cancellationToken);
        if (borrador.SocioNegocioFacturarAId != borrador.SocioNegocioId
            && (facturarA is null || facturarA.Bloqueado != BloqueoSocioNegocio.Ninguno))
        {
            errores.Add(new Error(
                "factura.socio_invalido", "El socio facturar-a no existe o está bloqueado para facturación.", "SocioNegocioFacturarAId"));
        }

        if (borrador.TerminoPagoId is { } terminoId
            && await unitOfWork.Repository<TerminoPago>().FirstOrDefaultAsync(x => x.Id == terminoId, cancellationToken: cancellationToken) is null)
        {
            errores.Add(new Error(
                "factura.termino_invalido", "El término de pago del borrador ya no existe: vuelva a asignar el socio.", "TerminoPagoId"));
        }
    }

    /// <summary>Reglas de una línea contra el estado ACTUAL de sus maestros; como mucho un error por línea.</summary>
    private async Task ValidarLineaAsync(LineaFacturaAPostear linea, List<Error> errores, CancellationToken cancellationToken)
    {
        if (linea.Cantidad <= 0m)
        {
            errores.Add(DeLinea(linea, new Error("factura.cantidad_invalida", "La cantidad debe ser mayor que cero.", "Cantidad")));
            return;
        }

        // Regla de importes (Task 8.4), la misma del alta: precio > 0 e importe 0 solo con 100 % de descuento.
        if ((LineaFacturaVentaBorradorReglas.ValidarPrecio(linea.PrecioUnitario)
             ?? LineaFacturaVentaBorradorReglas.ValidarImporte(linea.ImporteLinea, linea.PorcentajeDescuentoLinea)) is { } errorImporte)
        {
            errores.Add(DeLinea(linea, errorImporte));
            return;
        }

        if (linea.GrupoIvaProductoId is null || string.IsNullOrWhiteSpace(linea.IdentificadorIva))
        {
            errores.Add(DeLinea(linea, new Error(
                "factura.iva_inconsistente", "La línea no tiene el IVA congelado: vuelva a guardarla.", "GrupoIvaProductoId")));
            return;
        }

        if (linea.Tipo == TipoLineaFactura.CuentaContable)
        {
            var cuenta = linea.CuentaContableId is { } cuentaId
                ? await unitOfWork.Repository<CuentaContable>().FirstOrDefaultAsync(x => x.Id == cuentaId, cancellationToken: cancellationToken)
                : null;
            if (cuenta is null || cuenta.TipoCuenta != TipoCuentaContable.Posteo || cuenta.Bloqueada || !cuenta.PosteoDirecto)
            {
                errores.Add(DeLinea(linea, new Error(
                    "factura.cuenta_invalida",
                    "La cuenta contable no existe o no admite captura directa: debe ser de posteo, no bloqueada y de posteo directo.",
                    "CuentaContableId")));
            }

            return;
        }

        // Producto: existe (no borrado) y no bloqueado para la venta (Venta ni Todo).
        var producto = linea.ProductoId is { } productoId
            ? await unitOfWork.Repository<Producto>().FirstOrDefaultAsync(x => x.Id == productoId, cancellationToken: cancellationToken)
            : null;
        if (producto is null || producto.Bloqueado != BloqueoProducto.Ninguno)
        {
            errores.Add(DeLinea(linea, new Error(
                "factura.producto_invalido", "El producto no existe o está bloqueado para la venta.", "ProductoId")));
            return;
        }

        if (linea.GrupoProductoId is null || linea.GrupoInventarioId is null)
        {
            errores.Add(DeLinea(linea, new Error(
                "factura.grupo_faltante", "La línea no tiene los grupos del producto congelados: vuelva a guardarla.", "ProductoId")));
            return;
        }

        var almacen = linea.AlmacenId is { } almacenId
            ? await unitOfWork.Repository<Almacen>().FirstOrDefaultAsync(x => x.Id == almacenId, cancellationToken: cancellationToken)
            : null;
        if (almacen is null || almacen.Bloqueado)
        {
            errores.Add(DeLinea(linea, new Error("factura.almacen_invalido", "El almacén no existe o está bloqueado.", "AlmacenId")));
            return;
        }

        var unidad = linea.UnidadMedidaId is { } unidadId
            ? await unitOfWork.Repository<UnidadMedida>().FirstOrDefaultAsync(x => x.Id == unidadId, cancellationToken: cancellationToken)
            : null;
        if (unidad is null)
        {
            errores.Add(DeLinea(linea, new Error("factura.unidad_invalida", "La unidad de medida no existe.", "UnidadMedidaId")));
            return;
        }

        var conversionResultado = await conversion.ObtenerConversionAsync(producto.Id, unidad.Id, cancellationToken);
        if (!conversionResultado.TryObtenerValor(out var conversionUnidad))
        {
            var original = conversionResultado.Errores[0];
            errores.Add(DeLinea(linea, new Error(original.Codigo, original.Mensaje, "UnidadMedidaId")));
            return;
        }

        // El factor vigente ya viene redondeado a 6: el mismo con el que RegistrarAsync convertirá y congelará.
        var vigente = conversionUnidad.Factor;
        if (vigente != linea.CantidadPorUnidadMedida)
        {
            errores.Add(DeLinea(linea, new Error(
                "factura.factor_cambiado",
                $"El factor de conversión de la unidad cambió desde que se guardó la línea ({linea.CantidadPorUnidadMedida:0.######} " +
                $"-> {vigente:0.######}); vuelva a guardar la línea.",
                "UnidadMedidaId")));
            return;
        }

        // La cantidad en unidad base debe ser exacta con los decimales ACTUALES de la unidad base (pudieron cambiar desde que
        // se guardó la línea): nunca se redondea en el inventario.
        var cantidadBase = conversionUnidad.ConvertirExacta(linea.Cantidad);
        if (cantidadBase.EsFallo)
        {
            errores.Add(DeLinea(linea, cantidadBase.Errores[0] with { Campo = "Cantidad" }));
        }
    }

    /// <summary>Un mismo identificador de IVA con porcentajes distintos no se puede agrupar (ruling DB-5).</summary>
    private static void ValidarIvaCoherente(IReadOnlyList<LineaFacturaAPostear> lineas, List<Error> errores)
    {
        foreach (var grupo in lineas
                     .Where(l => !string.IsNullOrWhiteSpace(l.IdentificadorIva))
                     .GroupBy(l => l.IdentificadorIva!, StringComparer.Ordinal)
                     .OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            if (grupo.Select(l => l.PorcentajeIva).Distinct().Count() > 1)
            {
                errores.Add(new Error(
                    "factura.iva_inconsistente",
                    $"El identificador de IVA '{grupo.Key}' tiene porcentajes distintos en las líneas " +
                    $"{string.Join(", ", grupo.Select(l => $"{l.NumeroLinea} ({l.PorcentajeIva:0.#####} %)"))}: vuelva a guardar esas líneas.",
                    "Lineas"));
            }
        }
    }

    /// <summary>
    /// Deriva TODAS las cuentas del asiento (y la de inventario de cada línea de Producto, que el batch de costo necesitará) antes
    /// de escribir. Cada combinación se resuelve una vez; un fallo se informa en la primera línea que la usa.
    /// </summary>
    private async Task<CuentasFactura> DerivarCuentasAsync(
        FacturaVentaBorrador borrador, IReadOnlyList<LineaFacturaAPostear> lineas, List<Error> errores, CancellationToken cancellationToken)
    {
        var cxc = await derivador.CuentaCxCAsync(borrador.GrupoClienteContableId, cancellationToken);
        if (cxc.EsFallo)
        {
            errores.Add(cxc.Errores[0] with { Campo = "GrupoClienteContableId" });
        }

        var ventas = new Dictionary<Guid, Guid>();
        var ventasFallidas = new HashSet<Guid>();
        var inventarioResuelto = new HashSet<(Guid, Guid)>();
        var iva = new Dictionary<Guid, Guid>();
        var ivaFallido = new HashSet<Guid>();
        foreach (var linea in lineas)
        {
            // Como mucho un error por línea: tras el primero, las demás derivaciones de esa línea se omiten.
            var conError = false;
            if (linea.Tipo == TipoLineaFactura.Producto && linea.GrupoProductoId is { } grupoProducto && linea.GrupoInventarioId is { } grupoInventario
                && linea.AlmacenId is { } almacen)
            {
                if (!ventas.ContainsKey(grupoProducto) && ventasFallidas.Add(grupoProducto))
                {
                    var cuenta = await derivador.CuentaVentasAsync(borrador.GrupoNegocioId, grupoProducto, cancellationToken);
                    if (cuenta.TryObtenerValor(out var cuentaVentas))
                    {
                        ventas[grupoProducto] = cuentaVentas;
                        ventasFallidas.Remove(grupoProducto);
                    }
                    else
                    {
                        errores.Add(DeLinea(linea, cuenta.Errores[0] with { Campo = "GrupoProductoId" }));
                        conError = true;
                    }
                }

                if (!conError && inventarioResuelto.Add((almacen, grupoInventario)))
                {
                    var cuenta = await derivador.CuentaInventarioAsync(almacen, grupoInventario, cancellationToken);
                    if (cuenta.EsFallo)
                    {
                        errores.Add(DeLinea(linea, cuenta.Errores[0] with { Campo = "GrupoInventarioId" }));
                        conError = true;
                    }
                }
            }

            if (!conError && linea.GrupoIvaProductoId is { } grupoIva && !iva.ContainsKey(grupoIva) && ivaFallido.Add(grupoIva))
            {
                var setup = await derivador.IvaAsync(borrador.GrupoIvaNegocioId, grupoIva, cancellationToken);
                if (setup.TryObtenerValor(out var setupIva))
                {
                    iva[grupoIva] = setupIva.CuentaIvaVentasId;
                    ivaFallido.Remove(grupoIva);
                }
                else
                {
                    errores.Add(DeLinea(linea, setup.Errores[0] with { Campo = "GrupoIvaProductoId" }));
                }
            }
        }

        // Una línea de IVA del documento lleva UNA cuenta: todas las líneas de un identificador deben derivar la misma.
        var ivaPorIdentificador = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var grupo in lineas
                     .Where(l => l.IdentificadorIva is not null && l.GrupoIvaProductoId is { } g && iva.ContainsKey(g))
                     .GroupBy(l => l.IdentificadorIva!, StringComparer.Ordinal))
        {
            var cuentasGrupo = grupo.Select(l => iva[l.GrupoIvaProductoId!.Value]).Distinct().ToList();
            if (cuentasGrupo.Count > 1)
            {
                errores.Add(new Error(
                    "factura.iva_inconsistente",
                    $"Las líneas {string.Join(", ", grupo.Select(l => l.NumeroLinea))} comparten el identificador de IVA '{grupo.Key}' " +
                    "pero sus setups de IVA tienen cuentas de IVA distintas.",
                    "Lineas"));
            }
            else
            {
                ivaPorIdentificador[grupo.Key] = cuentasGrupo[0];
            }
        }

        return new CuentasFactura(cxc.EsExito ? cxc.Valor : Guid.Empty, ventas, ivaPorIdentificador);
    }

    private static FacturaVenta CrearFactura(
        FacturaVentaBorrador b, string numero, TotalesFactura totales, long? registroContableId, DateTimeOffset ahora, string creadoPor,
        Guid? usuarioId) => new()
    {
        Numero = numero,
        NumeroBorrador = b.Numero,
        SocioNegocioId = b.SocioNegocioId,
        SocioNegocioFacturarAId = b.SocioNegocioFacturarAId,
        NombreFacturacion = b.NombreFacturacion,
        RazonSocialFacturacion = b.RazonSocialFacturacion,
        TipoDocumentoFiscal = b.TipoDocumentoFiscal,
        NumeroDocumentoFiscal = b.NumeroDocumentoFiscal,
        DireccionFacturacionLinea1 = b.DireccionFacturacionLinea1,
        DireccionFacturacionLinea2 = b.DireccionFacturacionLinea2,
        CiudadFacturacion = b.CiudadFacturacion,
        PaisCodigoFacturacion = b.PaisCodigoFacturacion,
        FechaRegistro = b.FechaRegistro,
        FechaDocumento = b.FechaDocumento,
        FechaVencimiento = b.FechaVencimiento,
        TerminoPagoId = b.TerminoPagoId,
        GrupoNegocioId = b.GrupoNegocioId,
        GrupoIvaNegocioId = b.GrupoIvaNegocioId,
        GrupoClienteContableId = b.GrupoClienteContableId,
        AlmacenId = b.AlmacenId,
        Moneda = b.Moneda,
        Descripcion = b.Descripcion,
        ImporteSinIva = totales.ImporteSinIva,
        ImporteIva = totales.ImporteIva,
        ImporteTotal = totales.ImporteTotal,
        RegistroContableId = registroContableId,
        CreatedAtUtc = ahora,
        CreatedBy = creadoPor,
        UsuarioId = usuarioId,
    };

    private static LineaFacturaVenta CrearLinea(string numero, LineaFacturaAPostear l, long? movimientoProductoId) => new()
    {
        FacturaVentaNumero = numero,
        NumeroLinea = l.NumeroLinea,
        Tipo = l.Tipo,
        ProductoId = l.ProductoId,
        CuentaContableId = l.CuentaContableId,
        Descripcion = l.Descripcion,
        AlmacenId = l.AlmacenId,
        UnidadMedidaId = l.UnidadMedidaId,
        CantidadPorUnidadMedida = l.CantidadPorUnidadMedida,
        Cantidad = l.Cantidad,
        PrecioUnitario = l.PrecioUnitario,
        PorcentajeDescuentoLinea = l.PorcentajeDescuentoLinea,
        ImporteDescuentoLinea = l.ImporteDescuentoLinea,
        ImporteLinea = l.ImporteLinea,
        GrupoProductoId = l.GrupoProductoId,
        GrupoIvaProductoId = l.GrupoIvaProductoId,
        GrupoInventarioId = l.GrupoInventarioId,
        IdentificadorIva = l.IdentificadorIva,
        PorcentajeIva = l.PorcentajeIva,
        MovimientoProductoId = movimientoProductoId,
    };

    private static Error DeLinea(LineaFacturaAPostear linea, Error original) => new(
        original.Codigo,
        $"Línea {linea.NumeroLinea}: {original.Mensaje}",
        $"Lineas[{linea.NumeroLinea}].{original.Campo}");

    private static string Truncar(string texto) => texto.Length <= LongitudDescripcion ? texto : texto[..LongitudDescripcion];

    private string CreadoPor()
    {
        var nombre = string.IsNullOrWhiteSpace(usuario.Nombre) ? "system" : usuario.Nombre;
        return nombre.Length <= LongitudCreadoPor ? nombre : nombre[..LongitudCreadoPor];
    }

    private static Result<ResultadoPosteoFactura> Fallo(params Error[] errores) => Result<ResultadoPosteoFactura>.Fallo(errores);
}

/// <summary>Cuentas ya derivadas del documento: CxC, Ventas por grupo de producto e IVA por identificador.</summary>
internal sealed record CuentasFactura(
    Guid CuentaCxCId, IReadOnlyDictionary<Guid, Guid> VentasPorGrupoProducto, IReadOnlyDictionary<string, Guid> CuentaIvaPorIdentificador);
