using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.DiariosInventario.Registros;
using OpenSource1.Core.Entities.Inventario;
using OpenSource1.Core.Enums;

namespace OpenSource1.Infrastructure.Data.Queries;

/// <summary>
/// Acceso a datos del registro de un lote (Task 4.3) con Dapper sobre la transacción de <see cref="IDbSession"/>, la misma
/// que enrola EF y que usa <c>IRegistroMovimientosInventario</c>.
/// </summary>
public sealed class RegistroLoteDiarioDatos(IDbSession session) : IRegistroLoteDiarioDatos
{
    public async Task<IReadOnlyList<LineaDiarioARegistrar>> BloquearLineasAsync(Guid loteDiarioId, CancellationToken cancellationToken = default)
    {
        await AsegurarTransaccionAsync(cancellationToken);

        // FOR UPDATE: en READ COMMITTED, un segundo registro del mismo lote que llegue aquí esperando vuelve a evaluar el
        // filtro tras el commit del primero y ya no ve las líneas (IsDeleted = true) -> lote vacío, nunca duplicados.
        const string sql = """
            SELECT "Id", "NumeroLinea", "FechaRegistro", "FechaDocumento", "NumeroDocumento", "TipoMovimiento",
                   "ProductoId", "AlmacenId", "AlmacenDestinoId", "UnidadMedidaId", "CantidadPorUnidadMedida",
                   "Cantidad", "CostoUnitario", "Descripcion"
            FROM "LineasDiario"
            WHERE "LoteDiarioId" = @LoteDiarioId AND "IsDeleted" = false
            ORDER BY "NumeroLinea"
            FOR UPDATE
            """;

        var filas = await session.Connection.QueryAsync<LineaFila>(new CommandDefinition(
            sql, new { LoteDiarioId = loteDiarioId }, session.CurrentTransaction, cancellationToken: cancellationToken));

        return [.. filas.Select(f => new LineaDiarioARegistrar(
            f.Id, f.NumeroLinea, f.FechaRegistro, f.FechaDocumento, f.NumeroDocumento, f.TipoMovimiento, f.ProductoId,
            f.AlmacenId, f.AlmacenDestinoId, f.UnidadMedidaId, f.CantidadPorUnidadMedida, f.Cantidad, f.CostoUnitario,
            f.Descripcion))];
    }

    public async Task<int> BorrarLineasAsync(IReadOnlyCollection<Guid> lineaIds, string usuario, CancellationToken cancellationToken = default)
    {
        await AsegurarTransaccionAsync(cancellationToken);

        // Mismas columnas que el borrado lógico de UnitOfWork (IsDeleted/DeletedAtUtc/DeletedBy); xmin cambia solo, así
        // que un PUT concurrente con el xmin anterior recibe 409 (o 404, porque la línea ya no es visible).
        const string sql = """
            UPDATE "LineasDiario" SET "IsDeleted" = true, "DeletedAtUtc" = @Ahora, "DeletedBy" = @Usuario
            WHERE "Id" = ANY(@Ids) AND "IsDeleted" = false
            """;

        return await session.Connection.ExecuteAsync(new CommandDefinition(
            sql, new { Ids = lineaIds.ToArray(), Ahora = DateTimeOffset.UtcNow, Usuario = usuario },
            session.CurrentTransaction, cancellationToken: cancellationToken));
    }

    public async Task InsertarRegistroAsync(RegistroDiario registro, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registro);
        await AsegurarTransaccionAsync(cancellationToken);

        const string sql = """
            INSERT INTO "RegistrosDiario" (
                "NumeroRegistro", "LoteDiarioId", "DesdeMovimientoProducto", "HastaMovimientoProducto", "Lineas",
                "FechaCreacion", "CreadoPor", "UsuarioId")
            VALUES (
                @NumeroRegistro, @LoteDiarioId, @DesdeMovimientoProducto, @HastaMovimientoProducto, @Lineas,
                @FechaCreacion, @CreadoPor, @UsuarioId)
            RETURNING "Id"
            """;

        registro.Id = await session.Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            sql, registro, session.CurrentTransaction, cancellationToken: cancellationToken));
    }

    private async Task AsegurarTransaccionAsync(CancellationToken cancellationToken)
    {
        if (session.CurrentTransaction is null)
        {
            throw new InvalidOperationException("El registro de un lote de diario requiere una transacción activa.");
        }

        await session.EnsureOpenAsync(cancellationToken);
    }

    private sealed class LineaFila
    {
        public Guid Id { get; init; }
        public int NumeroLinea { get; init; }
        public DateOnly FechaRegistro { get; init; }
        public DateOnly FechaDocumento { get; init; }
        public string? NumeroDocumento { get; init; }
        public TipoMovimientoInventario TipoMovimiento { get; init; }
        public Guid ProductoId { get; init; }
        public Guid AlmacenId { get; init; }
        public Guid? AlmacenDestinoId { get; init; }
        public Guid UnidadMedidaId { get; init; }
        public decimal CantidadPorUnidadMedida { get; init; }
        public decimal Cantidad { get; init; }
        public decimal? CostoUnitario { get; init; }
        public string? Descripcion { get; init; }
    }
}
