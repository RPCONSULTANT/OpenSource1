using System.Globalization;
using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Services.Contabilidad;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Infrastructure.Services.Contabilidad;

/// <summary>
/// Batch idempotente "Postear costo de inventario a contabilidad" (spec 5.6, Task 5.6). Solo Dapper sobre la sesión del scope
/// (<see cref="IDbSession"/>), como <c>AjusteCostoInventario</c>: el derivador y el registro contable usan la misma conexión y
/// transacción.
/// </summary>
/// <remarks>
/// <para>
/// <b>Unidad de trabajo: (producto, fecha de registro)</b>, cada una en su propia transacción y con UN asiento como máximo
/// (<c>FechaRegistro = FechaDocumento</c> = la del movimiento). Un producto sin setup no bloquea a los demás.
/// </para>
/// <list type="number">
/// <item><c>pg_advisory_xact_lock(hashtextextended('contab-costo', 0))</c>: lock GLOBAL del batch; dos ejecuciones en paralelo se
/// serializan grupo a grupo.</item>
/// <item><c>SELECT ... FOR UPDATE</c> de los movimientos de valor pendientes del grupo (<c>ImporteCosto &lt;&gt;
/// ImporteCostoPosteadoContabilidad</c>, índice parcial <c>IX_MovimientosValor_PendientePosteoContabilidad</c>), releídos bajo el
/// lock: lo que otra ejecución ya contabilizó no aparece.</item>
/// <item>Por movimiento, <c>delta = ImporteCosto − ImporteCostoPosteadoContabilidad</c>: <c>CuentaInventario(almacén, grupo de
/// inventario congelado)</c> por <c>+delta</c> y la contrapartida por <c>−delta</c>: <c>CuentaCostoVentas(grupo de negocio, grupo
/// de producto congelados)</c> si es Venta; <c>CuentaAjusteInventario(almacén, grupo de inventario)</c> en compras, ajustes y la
/// apertura migrada. Las transferencias solo llevan su línea de inventario: la salida y la entrada (y sus ajustes de costo) se
/// compensan entre sí, inventario contra inventario.</item>
/// <item>Un movimiento cuya derivación falla se excluye ENTERO (ninguna de sus dos líneas) y se informa en los pendientes con el
/// error del derivador (<c>setup_contable.inexistente</c> con la combinación, <c>grupo_faltante</c>, <c>cuenta_invalida</c>). Las
/// transferencias del grupo van todas o ninguna: si una no se puede derivar (o las que sí no suman 0), todas quedan pendientes
/// (<c>contabilidad.transferencia_incompleta</c>), porque sin su contraparte el asiento no cuadraría.</item>
/// <item>Líneas agrupadas por cuenta y sin las de total 0 (una transferencia entre almacenes con la misma cuenta netea). Si no
/// queda ninguna, no se crea registro pero los movimientos se marcan igual (su efecto contable neto es 0).
/// <c>TipoOrigen = CostoInventario</c>; <c>ClaveOrigen = COSTO-aaaammdd-producto</c>.</item>
/// <item>Actualización de <c>ImporteCostoPosteadoContabilidad</c> a <c>ImporteCosto</c> en los contabilizados (la única
/// columna que el trigger append-only deja cambiar) y commit. Si el registro contable falla, rollback y todo el grupo queda
/// pendiente.</item>
/// </list>
/// </remarks>
public sealed class PosteoCostoInventario(
    IDbSession session,
    IDerivadorCuentas derivador,
    IRegistroContable registroContable) : IPosteoCostoInventario
{
    public async Task<ResultadoPosteoCostoInventario> PostearAsync(Guid? productoId, CancellationToken ct = default)
    {
        if (session.HayTransaccionActiva)
        {
            // Una transacción externa uniría todos los grupos en una sola: ni la atomicidad por grupo ni el aislamiento de
            // un producto sin setup se cumplirían (mismo criterio que PostearLoteDiario).
            throw new InvalidOperationException(
                "PostearCostoInventario debe ejecutarse fuera de otra transacción: abre y confirma una por producto y fecha.");
        }

        await session.EnsureOpenAsync(ct);

        const string gruposSql = """
            SELECT g."ProductoId", g."FechaRegistro", p."Codigo" AS "CodigoProducto"
            FROM (SELECT DISTINCT "ProductoId", "FechaRegistro" FROM "MovimientosValor"
                  WHERE "ImporteCosto" <> "ImporteCostoPosteadoContabilidad" {0}) g
            JOIN "Productos" p ON p."Id" = g."ProductoId"
            ORDER BY g."ProductoId", g."FechaRegistro"
            """;
        var grupos = (await session.Connection.QueryAsync<Grupo>(new CommandDefinition(
            string.Format(CultureInfo.InvariantCulture, gruposSql, productoId is null ? string.Empty : """AND "ProductoId" = @productoId"""),
            new { productoId }, cancellationToken: ct))).ToList();

        var asientos = 0;
        var contabilizados = 0;
        var pendientes = new List<PendientePosteoCosto>();
        foreach (var grupo in grupos)
        {
            var (asiento, marcados) = await PostearGrupoAsync(grupo, pendientes, ct);
            asientos += asiento ? 1 : 0;
            contabilizados += marcados;
        }

        return new ResultadoPosteoCostoInventario(asientos, contabilizados, pendientes);
    }

    /// <summary>Un (producto, fecha) en su propia transacción. Devuelve si creó asiento y cuántos movimientos marcó.</summary>
    private async Task<(bool Asiento, int Marcados)> PostearGrupoAsync(Grupo grupo, List<PendientePosteoCosto> pendientes, CancellationToken ct)
    {
        // Sin CommitAsync, salir del "await using" deshace la transacción (y suelta el lock y las filas).
        await using var ambito = await session.BeginTransactionAsync(ct);
        var tx = session.CurrentTransaction;

        await session.Connection.ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_xact_lock(hashtextextended('contab-costo', 0))", transaction: tx, cancellationToken: ct));

        var movimientos = (await session.Connection.QueryAsync<Movimiento>(new CommandDefinition(
            """
            SELECT "Id", "AlmacenId", "TipoMovimiento", "ImporteCosto", "ImporteCostoPosteadoContabilidad",
                   "GrupoInventarioId", "GrupoNegocioId", "GrupoProductoId"
            FROM "MovimientosValor"
            WHERE "ProductoId" = @ProductoId AND "FechaRegistro" = @FechaRegistro
              AND "ImporteCosto" <> "ImporteCostoPosteadoContabilidad"
            ORDER BY "Id"
            FOR UPDATE
            """,
            new { grupo.ProductoId, grupo.FechaRegistro }, tx, cancellationToken: ct))).ToList();
        if (movimientos.Count == 0)
        {
            return (false, 0); // Lo contabilizó otra ejecución mientras se esperaba el lock.
        }

        // 1. Derivación por movimiento: cada uno entra entero (sus dos líneas) o queda pendiente.
        var derivados = new List<(Movimiento Movimiento, Guid Inventario, Guid? Contrapartida)>();
        var pendientesGrupo = new List<PendientePosteoCosto>();
        foreach (var m in movimientos)
        {
            var inventario = await derivador.CuentaInventarioAsync(m.AlmacenId, m.GrupoInventarioId, ct);
            if (!inventario.TryObtenerValor(out var cuentaInventario))
            {
                pendientesGrupo.Add(Pendiente(m, inventario.Errores[0]));
                continue;
            }

            if (m.TipoMovimiento == TipoMovimientoInventario.Transferencia)
            {
                derivados.Add((m, cuentaInventario, null));
                continue;
            }

            var contrapartida = m.TipoMovimiento == TipoMovimientoInventario.Venta
                ? await derivador.CuentaCostoVentasAsync(m.GrupoNegocioId, m.GrupoProductoId, ct)
                : await derivador.CuentaAjusteInventarioAsync(m.AlmacenId, m.GrupoInventarioId, ct);
            if (!contrapartida.TryObtenerValor(out var cuentaContrapartida))
            {
                pendientesGrupo.Add(Pendiente(m, contrapartida.Errores[0]));
                continue;
            }

            derivados.Add((m, cuentaInventario, cuentaContrapartida));
        }

        // 2. Transferencias: todas o ninguna (se compensan entre sí; sin la contraparte el asiento no cuadraría).
        var transferencias = derivados.Where(d => d.Contrapartida is null).ToList();
        var transferenciaFallida = pendientesGrupo.Any(p =>
            movimientos.First(m => m.Id == p.MovimientoValorId).TipoMovimiento == TipoMovimientoInventario.Transferencia);
        if (transferencias.Count > 0 && (transferenciaFallida || transferencias.Sum(d => d.Movimiento.Delta) != 0m))
        {
            var mensaje = transferenciaFallida
                ? $"Otra parte de la transferencia del {Fecha(grupo.FechaRegistro)} no se puede contabilizar; ninguna parte se contabiliza hasta resolverlo."
                : $"Las partes pendientes de la transferencia del {Fecha(grupo.FechaRegistro)} no suman 0; ninguna se contabiliza.";
            foreach (var d in transferencias)
            {
                derivados.Remove(d);
                pendientesGrupo.Add(Pendiente(d.Movimiento, new Error("contabilidad.transferencia_incompleta", mensaje)));
            }
        }

        // 3. Líneas por cuenta (sin las de total 0).
        var lineas = derivados
            .SelectMany(d => d.Contrapartida is { } c
                ? new[] { (Cuenta: d.Inventario, Importe: d.Movimiento.Delta, d.Movimiento), (Cuenta: c, Importe: -d.Movimiento.Delta, d.Movimiento) }
                : [(Cuenta: d.Inventario, Importe: d.Movimiento.Delta, d.Movimiento)])
            .GroupBy(x => x.Cuenta)
            .Select(g => new LineaAsiento(
                g.Key, g.Sum(x => x.Importe), Descripcion: null, ProductoId: grupo.ProductoId,
                GrupoNegocioId: Unico(g.Select(x => x.Movimiento.GrupoNegocioId)),
                GrupoProductoId: Unico(g.Select(x => x.Movimiento.GrupoProductoId))))
            .Where(l => l.Importe != 0m)
            .ToList();

        var asiento = false;
        if (lineas.Count > 0)
        {
            var resultado = await registroContable.RegistrarAsync(new AsientoContable(
                grupo.FechaRegistro, grupo.FechaRegistro, TipoDocumentoContable.CostoInventario, NumeroDocumento: null,
                $"Costo de inventario {grupo.CodigoProducto} {Fecha(grupo.FechaRegistro)}",
                TipoOrigenMovimiento.CostoInventario, $"COSTO-{grupo.FechaRegistro:yyyyMMdd}-{grupo.ProductoId:N}", lineas), ct);
            if (!resultado.EsExito)
            {
                // Rollback (al salir sin commit): el grupo entero queda pendiente.
                pendientes.AddRange(pendientesGrupo);
                pendientes.AddRange(derivados.Select(d => Pendiente(d.Movimiento, resultado.Errores[0])));
                return (false, 0);
            }

            asiento = true;
        }

        // 4. Marcar los contabilizados (la única columna actualizable del libro de valor) y confirmar.
        var ids = derivados.Select(d => d.Movimiento.Id).ToArray();
        if (ids.Length > 0)
        {
            var marcados = await session.Connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE "MovimientosValor" SET "ImporteCostoPosteadoContabilidad" = "ImporteCosto"
                WHERE "Id" = ANY(@Ids) AND "ImporteCosto" <> "ImporteCostoPosteadoContabilidad"
                """,
                new { Ids = ids }, tx, cancellationToken: ct));
            if (marcados != ids.Length)
            {
                // Imposible con las filas bloqueadas FOR UPDATE: abortar antes que confirmar un delta contabilizado dos veces.
                throw new InvalidOperationException(
                    $"Se esperaba marcar {ids.Length} movimientos de valor como contabilizados y se marcaron {marcados}.");
            }

            await session.CommitAsync(ct);
        }

        pendientes.AddRange(pendientesGrupo);
        return (asiento, ids.Length);
    }

    private static Guid? Unico(IEnumerable<Guid?> valores)
    {
        var distintos = valores.Distinct().Take(2).ToList();
        return distintos.Count == 1 ? distintos[0] : null;
    }

    private static PendientePosteoCosto Pendiente(Movimiento m, Error error) => new(m.Id, error.Codigo, error.Mensaje);

    private static string Fecha(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private sealed class Grupo
    {
        public Guid ProductoId { get; init; }
        public DateOnly FechaRegistro { get; init; }
        public string CodigoProducto { get; init; } = string.Empty;
    }

    private sealed class Movimiento
    {
        public long Id { get; init; }
        public Guid AlmacenId { get; init; }
        public TipoMovimientoInventario TipoMovimiento { get; init; }
        public decimal ImporteCosto { get; init; }
        public decimal ImporteCostoPosteadoContabilidad { get; init; }
        public Guid? GrupoInventarioId { get; init; }
        public Guid? GrupoNegocioId { get; init; }
        public Guid? GrupoProductoId { get; init; }

        public decimal Delta => ImporteCosto - ImporteCostoPosteadoContabilidad;
    }
}
