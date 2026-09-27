using MediatR;
using OpenSource1.Application.Data;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.FacturasVenta.Calculo;
using OpenSource1.Application.Features.FacturasVenta.Posteo;
using OpenSource1.Application.Services.Clientes;
using OpenSource1.Application.Services.Contabilidad;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Application.Services.Registro;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Entities.Ventas;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.NotasCreditoVenta.Posteo;

/// <summary>
/// Motor de posteo de notas de crédito de venta (Task 8.6), mismo patrón que <c>PostearFacturaVentaCommandHandler</c>: UNA
/// transacción de <see cref="IUnitOfWork"/> (un fallo o una excepción deshacen todo, incluido el número de la serie <c>NC</c>).
/// <list type="number">
/// <item>Bloqueos, en el orden global: borrador y sus líneas (<c>FOR UPDATE</c>) → FACTURA (<c>FOR UPDATE</c> de su fila, el
/// punto de serialización de todas sus notas, también si es de total 0 y no tiene movimiento de cliente) → movimiento de cliente de
/// la factura (<c>FOR UPDATE</c>, con su restante leído después del bloqueo) → socios (<c>FOR SHARE</c>) → productos con devolución
/// (ordenados) → serie <c>NC</c> → almacenes (dentro de <see cref="IRegistroMovimientosInventario"/>) → cuentas y serie
/// <c>CONTAB</c> (dentro de <see cref="IRegistroContable"/>).</item>
/// <item>Revalidación contra el estado actual y BAJO EL BLOQUEO de la factura (Review Focus 1): fecha de registro permitida
/// (Task 8.5) y no anterior a la factura; socios; por línea, cantidad ≤ facturada − acreditada por notas POSTEADAS (dos notas
/// concurrentes sobre la misma línea se serializan en la factura y la segunda ve lo que acreditó la primera), exactitud de la
/// devolución, producto, almacén y costo de la salida original; cuentas (Ventas derivada; IVA y CxC CONGELADAS de la factura;
/// inventario derivada para el batch de costo). Nada se escribe hasta que no queda ningún error.</item>
/// <item>IVA agrupado de la nota (<see cref="CalculadoraIvaFactura"/>), número <c>NC</c>, asiento en memoria.</item>
/// <item>Devolución (Review Focus 2): por línea con <c>DevolverInventario</c>, entrada <c>Venta</c> con cantidad POSITIVA en el
/// almacén de la línea original, en unidad base (convertida con el factor congelado de la factura), al costo unitario EXACTO de la
/// salida original: <c>−ImporteCosto / CantidadBase</c> de su movimiento de producto, con <c>ImporteCosto</c> = Σ de sus movimientos
/// de valor CostoDirecto (el original MÁS los ajustes de costo posteriores: el costo "de la venta" es el vigente de la salida).</item>
/// <item>Cliente: <c>MovimientoCliente</c> NotaCredito del facturar-a por <c>−total</c> (detalle ImporteInicial) con la CxC y el
/// grupo CONGELADOS del movimiento de la factura, y aplicación automática contra ese movimiento por min(total, restante de la
/// factura); si la factura ya estaba pagada del todo, la nota queda sin aplicar (saldo a favor).</item>
/// <item>Asiento inverso (<see cref="AsientoFacturaVenta"/> con <see cref="AsientoFacturaVenta.Signo.Inverso"/>): débito Ventas por
/// grupo con el mismo ajuste de redondeo, débito de las cuentas de las líneas CuentaContable, débito IVA por grupo, crédito CxC.</item>
/// <item>Documento posteado y borrado lógico del borrador.</item>
/// <item>Total 0 (Ruling FE): solo si TODAS las líneas devuelven inventario (devolución de un obsequio): documento y entradas,
/// sin movimiento de cliente, sin aplicación y sin asiento; en otro caso <c>nota_credito.importe_cero</c>.</item>
/// </list>
/// </summary>
public sealed class PostearNotaCreditoVentaCommandHandler(
    IUnitOfWork unitOfWork,
    INotaCreditoVentaDatos datos,
    IConversionUnidadMedidaService conversion,
    IDerivadorCuentas derivador,
    IRegistroMovimientosInventario registroInventario,
    IRegistroMovimientosCliente registroClientes,
    IRegistroContable registroContable,
    IGeneradorNumeroDocumento generadorNumero,
    IUsuarioActual usuario,
    IValidadorFechaRegistro validadorFecha)
    : IRequestHandler<PostearNotaCreditoVentaCommand, Result<ResultadoPosteoNotaCredito>>
{
    private const int LongitudNumero = 20;
    private const int LongitudCreadoPor = 100;
    private const int LongitudDescripcion = 200;

    public async Task<Result<ResultadoPosteoNotaCredito>> Handle(PostearNotaCreditoVentaCommand request, CancellationToken cancellationToken)
    {
        if (unitOfWork.HayTransaccionActiva)
        {
            throw new InvalidOperationException(
                "PostearNotaCreditoVenta debe ejecutarse fuera de otra transacción: garantiza su propia atomicidad.");
        }

        await using var transaccion = await unitOfWork.BeginTransactionAsync(cancellationToken);

        // 1. Borrador y líneas (FOR UPDATE).
        if (!await datos.BloquearBorradorAsync(request.NotaCreditoVentaBorradorId, cancellationToken))
        {
            return Fallo(NotaCreditoVentaErrores.BorradorNoEncontrado());
        }

        var repositorio = unitOfWork.Repository<NotaCreditoVentaBorrador>();
        var borrador = (await repositorio.FirstOrDefaultAsync(
            x => x.Id == request.NotaCreditoVentaBorradorId, asTracking: true, cancellationToken: cancellationToken))!;

        var lineas = await datos.BloquearLineasAsync(borrador.Id, cancellationToken);
        if (lineas.Count == 0)
        {
            return Fallo(new Error("nota_credito.sin_lineas", "El borrador no tiene líneas que postear.", "Id"));
        }

        // 2. Factura (FOR UPDATE): serializa todas las notas de la factura. Después, su movimiento de cliente (FOR UPDATE) con el
        //    restante leído tras el bloqueo.
        var factura = await datos.ObtenerFacturaAsync(borrador.FacturaVentaNumero, bloquear: true, cancellationToken);
        if (factura is null)
        {
            // Imposible: la FK del borrador exige la factura y el documento posteado no se borra.
            return Fallo(NotaCreditoVentaErrores.FacturaInvalida());
        }

        var movimientoFactura = await datos.MovimientoClienteFacturaAsync(factura.Numero, cancellationToken);
        decimal restanteFactura = 0m;
        if (movimientoFactura is not null)
        {
            var bloqueado = await registroClientes.BloquearMovimientosAsync([movimientoFactura.Id], cancellationToken);
            restanteFactura = bloqueado.Single().ImporteRestante;
        }

        // 3. Socios (compartido) y productos con devolución (orden único) antes de revalidarlos.
        await datos.BloquearSociosAsync([borrador.SocioNegocioId, borrador.SocioNegocioFacturarAId], cancellationToken);
        await registroInventario.BloquearProductosAsync(
            lineas.Where(l => l.DevolverInventario && l.ProductoId is not null).Select(l => l.ProductoId!.Value), cancellationToken);

        // 4. Revalidación (bajo los bloqueos) y derivación de todas las cuentas: nada se escribe hasta que no quede ningún error.
        var errores = new List<Error>();
        await ValidarCabeceraAsync(borrador, factura, errores, cancellationToken);

        var lineasFactura = (await datos.LineasFacturaAsync(factura.Numero, cancellationToken)).ToDictionary(l => l.Id);
        var acreditadas = await datos.CantidadesAcreditadasAsync(factura.Numero, cancellationToken);
        var devoluciones = new Dictionary<Guid, Devolucion>();
        var lineasValidas = new List<LineaNotaAPostear>();
        foreach (var linea in lineas)
        {
            var antes = errores.Count;
            if (await ValidarLineaAsync(linea, lineasFactura, acreditadas, errores, cancellationToken) is { } devolucion)
            {
                devoluciones[linea.Id] = devolucion;
            }

            if (errores.Count == antes)
            {
                lineasValidas.Add(linea);
            }
        }

        ValidarIvaCoherente(lineas, errores);
        var cuentasIvaFactura = await datos.CuentasIvaFacturaAsync(factura.Numero, cancellationToken);
        var cuentas = await DerivarCuentasAsync(borrador, lineasValidas, devoluciones, cuentasIvaFactura, errores, cancellationToken);
        if (errores.Count > 0)
        {
            return Fallo([.. errores]);
        }

        // 5. IVA agrupado. Total 0 solo si todas las líneas devuelven inventario (Ruling FE).
        var totales = CalculadoraIvaFactura.Calcular(
            [.. lineas.Select(l => new LineaCalculoIva(l.NumeroLinea, l.IdentificadorIva!, l.PorcentajeIva, l.ImporteLinea))]);
        var conImporte = totales.ImporteTotal != 0m;
        if (!conImporte && !lineas.All(l => l.DevolverInventario))
        {
            return Fallo(new Error(
                "nota_credito.importe_cero",
                "La nota de crédito es de total 0: solo se admite si todas sus líneas devuelven inventario (devolución de un obsequio).",
                "Lineas"));
        }

        CuentasFactura? cuentasAsiento = null;
        if (conImporte)
        {
            // La CxC CONGELADA del movimiento de cliente de la factura (no la vigente del grupo).
            if (movimientoFactura is null)
            {
                return Fallo(new Error(
                    "nota_credito.sin_movimiento_cliente",
                    $"La factura {factura.Numero} no tiene movimiento de cliente (es de total 0): no admite una nota con importe.",
                    "FacturaVentaNumero"));
            }

            if (await ValidarCuentaCongeladaAsync(movimientoFactura.CuentaCxCId, "CuentaCxCId", "la cuenta por cobrar", cancellationToken)
                is { } errorCxC)
            {
                return Fallo(errorCxC);
            }

            cuentasAsiento = cuentas with { CuentaCxCId = movimientoFactura.CuentaCxCId };
        }

        // 6. Número de la nota (FOR UPDATE de la línea de la serie NC, sin huecos: se deshace con todo lo demás).
        var numeroResultado = await generadorNumero.SiguienteAsync(
            SerieNotaCreditoVentaIds.CodigoPosteada, DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken);
        if (!numeroResultado.TryObtenerValor(out var numero))
        {
            return Result<ResultadoPosteoNotaCredito>.Fallo(numeroResultado);
        }

        if (numero.Length > LongitudNumero)
        {
            return Fallo(new Error("nota_credito.serie_invalida", $"El número de nota generado supera los {LongitudNumero} caracteres."));
        }

        var descripcion = Truncar(borrador.Descripcion ?? $"Nota de crédito {numero} de la factura {factura.Numero}");
        var asiento = cuentasAsiento is null
            ? null
            : AsientoFacturaVenta.Construir(
                new CabeceraAsientoVenta(
                    borrador.SocioNegocioId, borrador.SocioNegocioFacturarAId, borrador.GrupoNegocioId, borrador.GrupoIvaNegocioId,
                    borrador.FechaRegistro, borrador.FechaDocumento),
                [.. lineas.Select(l => new LineaAsientoVenta(
                    l.Tipo, l.GrupoProductoId, l.CuentaContableId, l.ImporteLinea, l.IdentificadorIva, l.GrupoIvaProductoId))],
                totales, cuentasAsiento, numero, descripcion,
                TipoDocumentoContable.NotaCreditoVenta, TipoOrigenMovimiento.NotaCreditoVenta, AsientoFacturaVenta.Signo.Inverso);

        // 7. Devoluciones: entrada Venta positiva al costo exacto de la salida original (en orden de línea).
        var movimientos = new Dictionary<Guid, long>();
        foreach (var linea in lineas.Where(l => l.DevolverInventario))
        {
            var devolucion = devoluciones[linea.Id];
            var entrada = await registroInventario.RegistrarAsync(new MovimientoInventarioSolicitud(
                linea.ProductoId!.Value,
                linea.AlmacenId!.Value,
                TipoMovimientoInventario.Venta,
                devolucion.CantidadBase,
                EsEntrada: true,
                devolucion.UnidadBaseId,
                devolucion.CostoUnitario,
                borrador.FechaRegistro,
                borrador.FechaDocumento,
                TipoDocumentoInventario.NotaCreditoVenta,
                numero,
                linea.NumeroLinea,
                TipoOrigenMovimiento.NotaCreditoVenta,
                numero,
                borrador.SocioNegocioId), cancellationToken);
            if (!entrada.TryObtenerValor(out var registrada))
            {
                return Fallo(NotaCreditoVentaErrores.DeLinea(linea.NumeroLinea, entrada.Errores[0]));
            }

            movimientos[linea.Id] = registrada.MovimientoProductoId;
        }

        // 8-9. Libro de clientes (con la aplicación automática) y asiento; nada de eso con total 0.
        var aplicado = 0m;
        AsientoRegistrado? asientoRegistrado = null;
        if (asiento is not null)
        {
            var movimientoNota = await registroClientes.RegistrarAsync(new MovimientoClienteSolicitud(
                borrador.SocioNegocioFacturarAId,
                borrador.FechaRegistro,
                borrador.FechaDocumento,
                borrador.FechaDocumento,
                TipoDocumentoCliente.NotaCredito,
                numero,
                descripcion,
                -totales.ImporteTotal,
                movimientoFactura!.GrupoClienteContableId,
                movimientoFactura.CuentaCxCId,
                TipoOrigenMovimiento.NotaCreditoVenta,
                numero), cancellationToken);
            if (!movimientoNota.TryObtenerValor(out var registradoCliente))
            {
                return Result<ResultadoPosteoNotaCredito>.Fallo(movimientoNota);
            }

            if (restanteFactura > 0m)
            {
                aplicado = Math.Min(totales.ImporteTotal, restanteFactura);
                var aplicacion = await registroClientes.AplicarAsync(new AplicacionClienteSolicitud(
                    movimientoFactura.Id, registradoCliente.MovimientoClienteId, aplicado, borrador.FechaRegistro,
                    TipoOrigenMovimiento.NotaCreditoVenta, numero), cancellationToken);
                if (aplicacion.EsFallo)
                {
                    return Result<ResultadoPosteoNotaCredito>.Fallo(aplicacion);
                }
            }

            var registro = await registroContable.RegistrarAsync(asiento, cancellationToken);
            if (!registro.TryObtenerValor(out asientoRegistrado))
            {
                // Los campos "Lineas[i].X" de IRegistroContable indexan las PATAS del asiento: se propagan sin ese campo.
                return Fallo([.. registro.Errores.Select(e =>
                    e.Campo is { } campo && campo.StartsWith("Lineas[", StringComparison.Ordinal) ? e with { Campo = null } : e)]);
            }
        }

        // 10. Documento legal (el último: nunca se actualiza).
        var ahora = DateTimeOffset.UtcNow;
        var creadoPor = CreadoPor();
        await datos.InsertarNotaAsync(
            CrearNota(borrador, numero, totales, conImporte ? movimientoFactura!.CuentaCxCId : null, asientoRegistrado?.RegistroContableId, ahora, creadoPor, usuario.Id),
            [.. lineas.Select(l => CrearLinea(numero, l, movimientos.TryGetValue(l.Id, out var m) ? m : null))],
            [.. totales.Grupos.Select(g => new LineaIvaNotaCreditoVenta
            {
                NotaCreditoVentaNumero = numero,
                IdentificadorIva = g.IdentificadorIva,
                PorcentajeIva = g.PorcentajeIva,
                BaseImponible = g.BaseImponible,
                ImporteIva = g.ImporteIva,
                CuentaIvaId = cuentas.CuentaIvaPorIdentificador[g.IdentificadorIva],
            })],
            cancellationToken);

        // 11. Borrado lógico del borrador y de todas sus líneas.
        var borradas = await datos.BorrarLineasAsync(borrador.Id, creadoPor, cancellationToken);
        if (borradas != lineas.Count)
        {
            throw new InvalidOperationException(
                $"Se esperaba borrar {lineas.Count} líneas del borrador {borrador.Numero} y se borraron {borradas}.");
        }

        repositorio.Remove(borrador);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result<ResultadoPosteoNotaCredito>.Exito(
            new ResultadoPosteoNotaCredito(numero, totales.ImporteTotal, aplicado, asientoRegistrado?.NumeroRegistro));
    }

    /// <summary>Fecha permitida y no anterior a la factura; socios existentes y no bloqueados (mismo criterio que la factura).</summary>
    private async Task ValidarCabeceraAsync(
        NotaCreditoVentaBorrador borrador, FacturaVenta factura, List<Error> errores, CancellationToken cancellationToken)
    {
        var fecha = await validadorFecha.ValidarAsync(borrador.FechaRegistro, cancellationToken);
        errores.AddRange(fecha.Errores);
        if (borrador.FechaRegistro < factura.FechaRegistro)
        {
            errores.Add(NotaCreditoVentaErrores.FechaAnteriorAFactura(factura.FechaRegistro));
        }

        var socios = unitOfWork.Repository<SocioNegocio>();
        var venderA = await socios.FirstOrDefaultAsync(x => x.Id == borrador.SocioNegocioId, cancellationToken: cancellationToken);
        if (venderA is null || venderA.Bloqueado != BloqueoSocioNegocio.Ninguno)
        {
            errores.Add(new Error(
                "nota_credito.socio_invalido", "El socio vender-a no existe o está bloqueado para facturación.", "SocioNegocioId"));
        }

        if (borrador.SocioNegocioFacturarAId != borrador.SocioNegocioId)
        {
            var facturarA = await socios.FirstOrDefaultAsync(x => x.Id == borrador.SocioNegocioFacturarAId, cancellationToken: cancellationToken);
            if (facturarA is null || facturarA.Bloqueado != BloqueoSocioNegocio.Ninguno)
            {
                errores.Add(new Error(
                    "nota_credito.socio_invalido", "El socio facturar-a no existe o está bloqueado para facturación.", "SocioNegocioFacturarAId"));
            }
        }
    }

    /// <summary>
    /// Reglas de una línea contra la línea de la factura, lo acreditado por notas posteadas (bajo el bloqueo de la factura) y el estado
    /// ACTUAL de sus maestros; como mucho un error por línea. Con devolución, devuelve lo necesario para registrar la entrada.
    /// </summary>
    private async Task<Devolucion?> ValidarLineaAsync(
        LineaNotaAPostear linea,
        IReadOnlyDictionary<long, LineaFacturaVenta> lineasFactura,
        IReadOnlyDictionary<long, decimal> acreditadas,
        List<Error> errores,
        CancellationToken cancellationToken)
    {
        if (!lineasFactura.TryGetValue(linea.LineaFacturaVentaId, out var original))
        {
            errores.Add(DeLinea(linea, new Error(
                "nota_credito.linea_factura_invalida", "La línea de factura acreditada no pertenece a la factura de la nota.", "LineaFacturaVentaId")));
            return null;
        }

        // Review Focus 1: la cantidad pendiente se recalcula aquí, con la factura bloqueada.
        var calculo = await LineaNotaCreditoVentaReglas.ValidarAsync(
            unitOfWork, conversion, original, acreditadas.GetValueOrDefault(original.Id), linea.Cantidad, linea.DevolverInventario,
            cancellationToken);
        if (!calculo.TryObtenerValor(out var valores))
        {
            errores.Add(DeLinea(linea, calculo.Errores[0]));
            return null;
        }

        if (linea.GrupoIvaProductoId is null || string.IsNullOrWhiteSpace(linea.IdentificadorIva))
        {
            errores.Add(DeLinea(linea, new Error("nota_credito.iva_inconsistente", "La línea no tiene el IVA de la factura.", "GrupoIvaProductoId")));
            return null;
        }

        if (linea.Tipo == TipoLineaFactura.CuentaContable)
        {
            var cuenta = linea.CuentaContableId is { } cuentaId
                ? await unitOfWork.Repository<CuentaContable>().FirstOrDefaultAsync(x => x.Id == cuentaId, cancellationToken: cancellationToken)
                : null;
            if (cuenta is null || cuenta.TipoCuenta != TipoCuentaContable.Posteo || cuenta.Bloqueada || !cuenta.PosteoDirecto)
            {
                errores.Add(DeLinea(linea, new Error(
                    "nota_credito.cuenta_invalida",
                    "La cuenta contable no existe o no admite captura directa: debe ser de posteo, no bloqueada y de posteo directo.",
                    "CuentaContableId")));
            }

            return null;
        }

        if (linea.GrupoProductoId is null || linea.GrupoInventarioId is null)
        {
            errores.Add(DeLinea(linea, new Error(
                "nota_credito.grupo_faltante", "La línea no tiene los grupos del producto de la factura.", "ProductoId")));
            return null;
        }

        if (!linea.DevolverInventario)
        {
            return null;
        }

        // Devolución: producto no bloqueado para todo movimiento, almacén de la línea original vivo y no bloqueado.
        var producto = await unitOfWork.Repository<Producto>().FirstOrDefaultAsync(x => x.Id == linea.ProductoId, cancellationToken: cancellationToken);
        if (producto is null || producto.Bloqueado == BloqueoProducto.Todo)
        {
            errores.Add(DeLinea(linea, new Error(
                "nota_credito.producto_invalido", "El producto no existe o está bloqueado para todo movimiento: no se puede devolver.", "ProductoId")));
            return null;
        }

        var almacen = linea.AlmacenId is { } almacenId
            ? await unitOfWork.Repository<Almacen>().FirstOrDefaultAsync(x => x.Id == almacenId, cancellationToken: cancellationToken)
            : null;
        if (almacen is null || almacen.Bloqueado)
        {
            errores.Add(DeLinea(linea, new Error(
                "nota_credito.almacen_invalido", "El almacén de la línea de la factura no existe o está bloqueado.", "AlmacenId")));
            return null;
        }

        // Review Focus 2: costo VIGENTE de la salida original (con sus ajustes de costo posteriores).
        var costo = original.MovimientoProductoId is { } salidaId ? await datos.CostoSalidaAsync(salidaId, cancellationToken) : null;
        if (costo is null || costo.CantidadBase <= 0m || costo.CostoUnitario < 0m)
        {
            errores.Add(DeLinea(linea, new Error(
                "nota_credito.costo_invalido", "No se puede determinar el costo de la salida original de la línea.", "Cantidad")));
            return null;
        }

        return new Devolucion(valores.CantidadBase!.Value, valores.UnidadBaseId!.Value, costo.CostoUnitario);
    }

    /// <summary>Un mismo identificador de IVA con porcentajes distintos no se puede agrupar.</summary>
    private static void ValidarIvaCoherente(IReadOnlyList<LineaNotaAPostear> lineas, List<Error> errores)
    {
        foreach (var grupo in lineas
                     .Where(l => !string.IsNullOrWhiteSpace(l.IdentificadorIva))
                     .GroupBy(l => l.IdentificadorIva!, StringComparer.Ordinal)
                     .Where(g => g.Select(l => l.PorcentajeIva).Distinct().Count() > 1))
        {
            errores.Add(new Error(
                "nota_credito.iva_inconsistente",
                $"El identificador de IVA '{grupo.Key}' tiene porcentajes distintos en las líneas {string.Join(", ", grupo.Select(l => l.NumeroLinea))}.",
                "Lineas"));
        }
    }

    /// <summary>
    /// Cuentas del asiento antes de escribir: Ventas DERIVADA por (GrupoNegocio × GrupoProducto), como la factura; IVA CONGELADA en
    /// la línea de IVA del mismo identificador de la factura original (validada: existe, de posteo y no bloqueada); inventario
    /// derivada por (almacén × grupo de inventario) en las líneas con devolución (el batch de costo la necesitará). La CxC la pone el
    /// llamador (congelada del movimiento de la factura).
    /// </summary>
    private async Task<CuentasFactura> DerivarCuentasAsync(
        NotaCreditoVentaBorrador borrador,
        IReadOnlyList<LineaNotaAPostear> lineas,
        IReadOnlyDictionary<Guid, Devolucion> devoluciones,
        IReadOnlyDictionary<string, Guid> cuentasIvaFactura,
        List<Error> errores,
        CancellationToken cancellationToken)
    {
        var ventas = new Dictionary<Guid, Guid>();
        var ventasFallidas = new HashSet<Guid>();
        var inventarioResuelto = new HashSet<(Guid, Guid)>();
        var iva = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var ivaFallido = new HashSet<string>(StringComparer.Ordinal);
        foreach (var linea in lineas)
        {
            var conError = false;
            if (linea.Tipo == TipoLineaFactura.Producto && linea.GrupoProductoId is { } grupoProducto)
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

                if (!conError && devoluciones.ContainsKey(linea.Id) && linea.AlmacenId is { } almacen && linea.GrupoInventarioId is { } grupoInventario
                    && inventarioResuelto.Add((almacen, grupoInventario)))
                {
                    var cuenta = await derivador.CuentaInventarioAsync(almacen, grupoInventario, cancellationToken);
                    if (cuenta.EsFallo)
                    {
                        errores.Add(DeLinea(linea, cuenta.Errores[0] with { Campo = "GrupoInventarioId" }));
                        conError = true;
                    }
                }
            }

            if (!conError && linea.IdentificadorIva is { } identificador && !iva.ContainsKey(identificador) && ivaFallido.Add(identificador))
            {
                if (!cuentasIvaFactura.TryGetValue(identificador, out var cuentaIva))
                {
                    errores.Add(DeLinea(linea, new Error(
                        "nota_credito.iva_inconsistente",
                        $"La factura no tiene línea de IVA '{identificador}': no se puede tomar su cuenta de IVA.",
                        "IdentificadorIva")));
                }
                else if (await ValidarCuentaCongeladaAsync(cuentaIva, "IdentificadorIva", $"la cuenta de IVA '{identificador}'", cancellationToken) is { } errorIva)
                {
                    errores.Add(DeLinea(linea, errorIva));
                }
                else
                {
                    iva[identificador] = cuentaIva;
                    ivaFallido.Remove(identificador);
                }
            }
        }

        return new CuentasFactura(Guid.Empty, ventas, iva);
    }

    /// <summary>Una cuenta congelada de la factura sigue siendo utilizable (existe, de posteo y no bloqueada).</summary>
    private async Task<Error?> ValidarCuentaCongeladaAsync(Guid cuentaId, string campo, string que, CancellationToken cancellationToken)
    {
        var cuenta = await unitOfWork.Repository<CuentaContable>().FirstOrDefaultAsync(x => x.Id == cuentaId, cancellationToken: cancellationToken);
        return cuenta is null || cuenta.TipoCuenta != TipoCuentaContable.Posteo || cuenta.Bloqueada
            ? new Error(
                "nota_credito.cuenta_invalida",
                $"No se puede usar {que} de la factura (congelada al postearla): no existe, no es de posteo o está bloqueada.",
                campo)
            : null;
    }

    private static NotaCreditoVenta CrearNota(
        NotaCreditoVentaBorrador b, string numero, TotalesFactura totales, Guid? cuentaCxCId, long? registroContableId,
        DateTimeOffset ahora, string creadoPor, Guid? usuarioId) => new()
    {
        Numero = numero,
        NumeroBorrador = b.Numero,
        FacturaVentaNumero = b.FacturaVentaNumero,
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
        GrupoNegocioId = b.GrupoNegocioId,
        GrupoIvaNegocioId = b.GrupoIvaNegocioId,
        GrupoClienteContableId = b.GrupoClienteContableId,
        CuentaCxCId = cuentaCxCId,
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

    private static LineaNotaCreditoVenta CrearLinea(string numero, LineaNotaAPostear l, long? movimientoProductoId) => new()
    {
        NotaCreditoVentaNumero = numero,
        NumeroLinea = l.NumeroLinea,
        LineaFacturaVentaId = l.LineaFacturaVentaId,
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
        DevolverInventario = l.DevolverInventario,
        MovimientoProductoId = movimientoProductoId,
    };

    private static Error DeLinea(LineaNotaAPostear linea, Error original) => NotaCreditoVentaErrores.DeLinea(linea.NumeroLinea, original);

    private static string Truncar(string texto) => texto.Length <= LongitudDescripcion ? texto : texto[..LongitudDescripcion];

    private string CreadoPor()
    {
        var nombre = string.IsNullOrWhiteSpace(usuario.Nombre) ? "system" : usuario.Nombre;
        return nombre.Length <= LongitudCreadoPor ? nombre : nombre[..LongitudCreadoPor];
    }

    private static Result<ResultadoPosteoNotaCredito> Fallo(params Error[] errores) => Result<ResultadoPosteoNotaCredito>.Fallo(errores);

    /// <summary>Entrada de devolución ya calculada: cantidad y unidad base, costo unitario exacto de la salida original.</summary>
    private sealed record Devolucion(decimal CantidadBase, Guid UnidadBaseId, decimal CostoUnitario);
}
