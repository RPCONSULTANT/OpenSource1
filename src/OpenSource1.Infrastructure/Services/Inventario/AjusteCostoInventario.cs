using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Infrastructure.Services.Inventario;

/// <summary>
/// Rutina idempotente "Ajustar costo movimientos" (Task 3.5). Por producto, en su propia transacción y bajo el MISMO
/// advisory lock que el registro (<see cref="BloqueoInventarioProducto"/>), recorre los días del libro en orden y
/// revalora cada salida (y cada movimiento de transferencia, en ambos sentidos) al costo promedio móvil del día,
/// insertando la diferencia como un movimiento de valor de ajuste; nunca actualiza ni borra filas del libro.
/// </summary>
/// <remarks>
/// <para>
/// <b>Misma fórmula que el posteo.</b> Para el día <c>d</c>, el costo es exactamente el de
/// <see cref="CostoPromedioCalculadora"/>: <c>V / Q</c> con todos los movimientos de valor con fecha &lt; <c>d</c> más los
/// de fecha <c>d</c> con <c>CantidadValorada &gt; 0</c> y <c>TipoMovimiento &lt;&gt; Transferencia</c> (el mismo filtro de
/// <see cref="CostoPromedioCalculadora.SumasSql"/>, aplicado en memoria a los movimientos de VALOR, no a los de producto),
/// con la misma división decimal sin redondear y el mismo <see cref="CostoPromedioCalculadora.Importe"/> (4 decimales,
/// <see cref="MidpointRounding.AwayFromZero"/>). Así, tras el ajuste, una salida posteada hoy en cualquier fecha recibe el
/// mismo costo que la rutina le daría y la siguiente pasada no inserta nada.
/// </para>
/// <para>
/// <b>Transferencias.</b> La salida y la entrada de una transferencia se valoran ambas al costo del día con signo opuesto:
/// el promedio no cambia (la entrada de transferencia tampoco entra en el promedio de su propio día, como en la
/// calculadora). Una entrada de transferencia registrada con costo explícito también se revalora al promedio del día.
/// </para>
/// <para>
/// <b>Costo no calculable (<c>Q &lt;= 0</c> en el día, p. ej. Stock negativo migrado).</b> Las salidas y transferencias de ese
/// día se valoran al costo del pool de entradas (mismo filtro: <c>CantidadValorada &gt; 0</c>, no transferencia;
/// <c>Σ ImporteCosto / Σ CantidadValorada</c> del día) del PRIMER día posterior que tenga entradas. Es determinista porque
/// las entradas nunca se revaloran (los pools se precalculan en una primera pasada). Si no hay ningún día posterior con
/// entradas se usa el último promedio con <c>Q &gt; 0</c>; si tampoco lo hay, la salida conserva el importe registrado y el
/// producto queda con <c>CostoAjustado = false</c> (pendiente: lo resolverá la siguiente ejecución cuando llegue una
/// entrada). No se usa <c>Producto.CostoUnitario</c>: la rutina lo reescribe al final y la segunda pasada ya no sería
/// idempotente.
/// </para>
/// <para>
/// <b>Divergencia residual con el posteo.</b> <see cref="CostoPromedioCalculadora"/> no conoce el futuro: una salida
/// posteada en un día con <c>Q &lt;= 0</c> se valora a <c>Producto.CostoUnitario</c>. Como <c>RegistrarAsync</c> marca
/// <c>CostoAjustado = false</c> en toda salida, la siguiente pasada de esta rutina la corrige.
/// </para>
/// <para>
/// <b>Redondeo.</b> Si al cierre de un día la cantidad valorada acumulada es 0 y queda un valor residual menor que 0.01 en
/// valor absoluto, se inserta un movimiento <see cref="TipoValor.Redondeo"/> por <c>−V</c> sobre la última salida del día.
/// Los movimientos de redondeo NO cuentan como costo de su salida al compararla con el esperado (si contaran, la segunda
/// pasada los desharía con un ajuste).
/// </para>
/// </remarks>
public sealed class AjusteCostoInventario(IDbSession session, IUsuarioActual usuario)
    : IAjusteCostoInventario
{
    private const int LongitudCreatedBy = 100;

    /// <summary>Por debajo de este valor absoluto, el residuo con cantidad 0 se da por redondeo.</summary>
    private const decimal UmbralRedondeo = 0.01m;

    public async Task<Result<ResultadoAjusteCosto>> AjustarAsync(Guid? productoId, CancellationToken ct = default)
    {
        if (session.HayTransaccionActiva)
        {
            // Una transacción externa uniría todos los productos en una sola (y retendría todos sus bloqueos a la vez).
            return Result<ResultadoAjusteCosto>.Fallo(new Error(
                "inventario.ajuste_en_transaccion",
                "El ajuste de costo abre una transacción por producto y no puede ejecutarse dentro de otra transacción."));
        }

        await session.EnsureOpenAsync(ct);

        List<Guid> productos;
        if (productoId is { } id)
        {
            // Sin filtro de borrado lógico: la historia de un producto borrado sigue contando.
            var existe = await session.Connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                """SELECT EXISTS (SELECT 1 FROM "Productos" WHERE "Id" = @id)""", new { id }, cancellationToken: ct));
            if (!existe)
            {
                return Result<ResultadoAjusteCosto>.Fallo(new Error(
                    "inventario.producto_invalido", "El producto no existe.", "ProductoId"));
            }

            productos = [id];
        }
        else
        {
            productos = (await session.Connection.QueryAsync<Guid>(new CommandDefinition(
                """SELECT "Id" FROM "Productos" WHERE "CostoAjustado" = false ORDER BY "Id" """, cancellationToken: ct))).ToList();
        }

        var ajustados = 0;
        var creados = 0;
        foreach (var producto in productos)
        {
            // Transacción de la sesión (solo Dapper): no se enrola en el DbContext, que de otro modo conservaría desde el
            // segundo producto una transacción ya confirmada.
            await using var ambito = await session.BeginTransactionAsync(ct);
            await BloqueoInventarioProducto.AdquirirAsync(session, producto, ct);

            var resultado = await AjustarProductoAsync(producto, soloSiPendiente: productoId is null, ct);
            await session.CommitAsync(ct);

            if (resultado is { } r)
            {
                creados += r.Insertados;
                if (r.Ajustado)
                {
                    ajustados++;
                }
            }
        }

        return Result<ResultadoAjusteCosto>.Exito(new ResultadoAjusteCosto(ajustados, creados));
    }

    /// <summary>
    /// Movimientos de valor insertados y si el producto quedó ajustado (false: alguna salida sin costo determinable sigue
    /// pendiente); null si el producto ya no estaba pendiente (lo ajustó otra ejecución).
    /// </summary>
    private async Task<(int Insertados, bool Ajustado)?> AjustarProductoAsync(Guid productoId, bool soloSiPendiente, CancellationToken ct)
    {
        var tx = session.CurrentTransaction;

        // Releído bajo el bloqueo: otra ejecución concurrente pudo ajustarlo mientras se esperaba.
        var producto = await session.Connection.QuerySingleOrDefaultAsync<ProductoFila>(new CommandDefinition(
            """SELECT "CostoUnitario", "CostoAjustado" FROM "Productos" WHERE "Id" = @productoId""",
            new { productoId }, tx, cancellationToken: ct));
        if (producto is null || (soloSiPendiente && producto.CostoAjustado))
        {
            return null;
        }

        var movimientos = (await session.Connection.QueryAsync<MovimientoFila>(new CommandDefinition(
            """
            SELECT "Id", "AlmacenId", "TipoMovimiento", "TipoDocumento", "NumeroDocumento", "NumeroLineaDocumento",
                   "FechaRegistro", "Cantidad"
            FROM "MovimientosProducto" WHERE "ProductoId" = @productoId
            ORDER BY "FechaRegistro", "Id"
            """,
            new { productoId }, tx, cancellationToken: ct))).ToList();

        var valores = (await session.Connection.QueryAsync<ValorFila>(new CommandDefinition(
            """
            SELECT "Id", "MovimientoProductoId", "FechaRegistro", "TipoValor", "TipoMovimiento", "CantidadValorada",
                   "ImporteCosto", "CostoPorUnidad", "GrupoInventarioId", "GrupoNegocioId", "GrupoProductoId"
            FROM "MovimientosValor" WHERE "ProductoId" = @productoId
            ORDER BY "Id"
            """,
            new { productoId }, tx, cancellationToken: ct))).ToList();

        var valoresPorFecha = valores.ToLookup(v => v.FechaRegistro);
        var valoresPorMovimiento = valores
            .Where(v => v.MovimientoProductoId is not null)
            .ToLookup(v => v.MovimientoProductoId!.Value);
        var movimientosPorFecha = movimientos.ToLookup(m => m.FechaRegistro);
        var fechas = valores.Select(v => v.FechaRegistro)
            .Concat(movimientos.Select(m => m.FechaRegistro))
            .Distinct()
            .Order()
            .ToList();

        // Primera pasada: pool de entradas de cada día (las entradas nunca se revaloran, así que es fijo). Para cada día,
        // el costo del pool del PRIMER día posterior con entradas (lo usan los días con costo no calculable).
        var costoPoolPosterior = new Dictionary<DateOnly, decimal?>();
        decimal? siguientePool = null;
        for (var i = fechas.Count - 1; i >= 0; i--)
        {
            costoPoolPosterior[fechas[i]] = siguientePool;
            var pool = valoresPorFecha[fechas[i]].Where(EntraEnPromedioDelDia).ToList();
            var cantidadPool = pool.Sum(v => v.CantidadValorada);
            if (cantidadPool > 0)
            {
                siguientePool = pool.Sum(v => v.ImporteCosto) / cantidadPool;
            }
        }

        var contexto = new ContextoInsercion(productoId, DateTimeOffset.UtcNow, CreadoPor(), usuario.Id);
        decimal valor = 0m, cantidad = 0m;
        decimal? ultimoCosto = null;
        var insertados = 0;
        var pendiente = false;

        foreach (var fecha in fechas)
        {
            var delDia = valoresPorFecha[fecha];

            // Costo del día: idéntico a CostoPromedioCalculadora.SumasSql para una salida con esta fecha.
            var valorPromedio = valor + delDia.Where(EntraEnPromedioDelDia).Sum(v => v.ImporteCosto);
            var cantidadPromedio = cantidad + delDia.Where(EntraEnPromedioDelDia).Sum(v => v.CantidadValorada);
            var costo = CostoPromedioCalculadora.Calcular(valorPromedio, cantidadPromedio);
            if (costo is not null)
            {
                ultimoCosto = costo;
            }
            else
            {
                // No calculable: pool del primer día posterior con entradas; si no lo hay, el último promedio.
                costo = costoPoolPosterior[fecha] ?? ultimoCosto;
            }

            var importeNuevoDelDia = 0m;
            MovimientoFila? ultimaSalida = null;
            foreach (var movimiento in movimientosPorFecha[fecha]
                         .Where(m => m.Cantidad < 0 || m.TipoMovimiento == TipoMovimientoInventario.Transferencia)
                         .OrderBy(m => m.Id))
            {
                if (movimiento.Cantidad < 0)
                {
                    ultimaSalida = movimiento;
                }

                if (costo is null)
                {
                    pendiente = true; // Sin costo determinable: conserva el importe registrado y queda pendiente.
                    continue;
                }

                var importe = CostoPromedioCalculadora.Importe(Math.Abs(movimiento.Cantidad), costo.Value);
                var esperado = movimiento.Cantidad < 0 ? -importe : importe;
                var actual = valoresPorMovimiento[movimiento.Id]
                    .Where(v => v.TipoValor != TipoValor.Redondeo)
                    .Sum(v => v.ImporteCosto);

                if (esperado != actual)
                {
                    await InsertarAsync(
                        contexto, movimiento, Original(valoresPorMovimiento[movimiento.Id]), TipoValor.CostoDirecto,
                        esperado - actual, $"AJUSTE-{movimiento.Id}", ct);
                    importeNuevoDelDia += esperado - actual;
                    insertados++;
                }
            }

            valor += delDia.Sum(v => v.ImporteCosto) + importeNuevoDelDia;
            cantidad += delDia.Sum(v => v.CantidadValorada);

            if (cantidad == 0 && valor != 0 && Math.Abs(valor) < UmbralRedondeo && ultimaSalida is not null)
            {
                await InsertarAsync(
                    contexto, ultimaSalida, Original(valoresPorMovimiento[ultimaSalida.Id]), TipoValor.Redondeo,
                    -valor, $"REDONDEO-{ultimaSalida.Id}", ct);
                valor = 0m;
                insertados++;
            }
        }

        // Dos columnas por SQL y solo si cambian: no se pisa el resto del maestro ni se toca su xmin sin necesidad.
        var ajustado = !pendiente;
        var costoFinal = CostoPromedioCalculadora.CostoPorUnidad(ultimoCosto ?? producto.CostoUnitario);
        await session.Connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE "Productos" SET "CostoAjustado" = @ajustado, "CostoUnitario" = @costoFinal
            WHERE "Id" = @productoId AND ("CostoAjustado" <> @ajustado OR "CostoUnitario" <> @costoFinal)
            """,
            new { productoId, ajustado, costoFinal }, tx, cancellationToken: ct));

        return (insertados, ajustado);
    }

    /// <summary>Filtro de <see cref="CostoPromedioCalculadora.SumasSql"/> para las filas del mismo día.</summary>
    private static bool EntraEnPromedioDelDia(ValorFila v) =>
        v.CantidadValorada > 0 && v.TipoMovimiento != TipoMovimientoInventario.Transferencia;

    /// <summary>El movimiento de valor original (el primero) del que se copian costo por unidad y grupos contables.</summary>
    private static ValorFila? Original(IEnumerable<ValorFila> filas) => filas.MinBy(v => v.Id);

    /// <summary>
    /// Inserta un movimiento de valor sin cantidad (<c>CantidadValorada = 0</c>, <c>Ajuste = true</c>,
    /// <c>TipoOrigen = AjusteCosto</c>) sobre <paramref name="movimiento"/>. Almacén, tipo de movimiento, fecha y documento
    /// salen del movimiento de producto (los mismos que su movimiento de valor original); costo por unidad y grupos, del
    /// original. <c>ImporteVenta</c>, <c>CantidadFacturada</c> e <c>ImporteCostoPosteadoContabilidad</c> van a 0: copiarlos
    /// duplicaría la venta y daría por contabilizado un importe que no lo está.
    /// </summary>
    private async Task InsertarAsync(
        ContextoInsercion contexto, MovimientoFila movimiento, ValorFila? original, TipoValor tipoValor,
        decimal importeCosto, string claveOrigen, CancellationToken ct) =>
        await session.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO "MovimientosValor" (
                "MovimientoProductoId", "ProductoId", "AlmacenId", "TipoValor", "TipoMovimiento", "FechaRegistro",
                "CantidadValorada", "CantidadFacturada", "ImporteCosto", "CostoPorUnidad", "ImporteVenta",
                "ImporteCostoPosteadoContabilidad", "Ajuste", "TipoDocumento", "NumeroDocumento", "NumeroLineaDocumento",
                "GrupoInventarioId", "GrupoNegocioId", "GrupoProductoId",
                "TipoOrigen", "ClaveOrigen", "CreatedAtUtc", "CreatedBy", "UsuarioId")
            VALUES (
                @MovimientoProductoId, @ProductoId, @AlmacenId, @TipoValor, @TipoMovimiento, @FechaRegistro,
                0, 0, @ImporteCosto, @CostoPorUnidad, 0,
                0, true, @TipoDocumento, @NumeroDocumento, @NumeroLineaDocumento,
                @GrupoInventarioId, @GrupoNegocioId, @GrupoProductoId,
                @TipoOrigen, @ClaveOrigen, @CreatedAtUtc, @CreatedBy, @UsuarioId)
            """,
            new
            {
                MovimientoProductoId = movimiento.Id,
                contexto.ProductoId,
                movimiento.AlmacenId,
                TipoValor = tipoValor,
                movimiento.TipoMovimiento,
                movimiento.FechaRegistro,
                ImporteCosto = importeCosto,
                CostoPorUnidad = original?.CostoPorUnidad ?? 0m,
                movimiento.TipoDocumento,
                movimiento.NumeroDocumento,
                movimiento.NumeroLineaDocumento,
                original?.GrupoInventarioId,
                original?.GrupoNegocioId,
                original?.GrupoProductoId,
                TipoOrigen = TipoOrigenMovimiento.AjusteCosto,
                ClaveOrigen = claveOrigen,
                contexto.CreatedAtUtc,
                contexto.CreatedBy,
                contexto.UsuarioId,
            },
            session.CurrentTransaction, cancellationToken: ct));

    private string CreadoPor()
    {
        var nombre = string.IsNullOrWhiteSpace(usuario.Nombre) ? "system" : usuario.Nombre;
        return nombre.Length <= LongitudCreatedBy ? nombre : nombre[..LongitudCreatedBy];
    }

    private sealed record ContextoInsercion(Guid ProductoId, DateTimeOffset CreatedAtUtc, string CreatedBy, Guid? UsuarioId);

    private sealed record ProductoFila(decimal CostoUnitario, bool CostoAjustado);

    private sealed record MovimientoFila(
        long Id, Guid AlmacenId, TipoMovimientoInventario TipoMovimiento, TipoDocumentoInventario TipoDocumento,
        string? NumeroDocumento, int NumeroLineaDocumento, DateOnly FechaRegistro, decimal Cantidad);

    private sealed record ValorFila(
        long Id, long? MovimientoProductoId, DateOnly FechaRegistro, TipoValor TipoValor,
        TipoMovimientoInventario TipoMovimiento, decimal CantidadValorada, decimal ImporteCosto, decimal CostoPorUnidad,
        Guid? GrupoInventarioId, Guid? GrupoNegocioId, Guid? GrupoProductoId);
}
