using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.DiariosInventario.Lineas;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Dtos;

namespace OpenSource1.Infrastructure.Data.Queries;

/// <summary>
/// Sin filtros ni orden configurable (brief 4.2: "aquí no hay filtros en líneas"), así que los JOIN van
/// calificados por alias de tabla directamente, sin la subconsulta aplanada que usa
/// <c>DapperProductoReadRepository</c> (esa hace falta solo cuando un filtro/orden necesita nombrar una
/// columna sin calificar que existe en más de una tabla del JOIN).
/// </summary>
public sealed class DapperLineaDiarioReadRepository(IDbSession session) : ILineaDiarioReadRepository
{
    private const string Columnas = """
        ld."Id", ld."LoteDiarioId", ld."NumeroLinea", ld."FechaRegistro", ld."FechaDocumento", ld."NumeroDocumento",
        ld."TipoMovimiento", ld."ProductoId", p."Codigo" AS "ProductoCodigo", p."Nombre" AS "ProductoNombre",
        ld."AlmacenId", a."Codigo" AS "AlmacenCodigo",
        ld."AlmacenDestinoId", ad."Codigo" AS "AlmacenDestinoCodigo",
        ld."UnidadMedidaId", u."Codigo" AS "UnidadMedidaCodigo",
        ld."CantidadPorUnidadMedida", ld."Cantidad", ld."CostoUnitario", ld."ImporteCosto", ld."Descripcion",
        ld."CreatedAtUtc", ld."UpdatedAtUtc", ld."CreatedBy", ld."UpdatedBy", ld.xmin::text::bigint AS "Xmin"
        """;

    // LEFT JOIN en los cuatro: una línea no debe desaparecer del listado si el producto/almacén/unidad al que
    // apunta se borró lógicamente después (mismo criterio que DapperProductoReadRepository).
    private const string DesdeYJoins = """
        FROM "LineasDiario" ld
        LEFT JOIN "Productos" p ON p."Id" = ld."ProductoId"
        LEFT JOIN "Almacenes" a ON a."Id" = ld."AlmacenId"
        LEFT JOIN "Almacenes" ad ON ad."Id" = ld."AlmacenDestinoId"
        LEFT JOIN "UnidadesMedida" u ON u."Id" = ld."UnidadMedidaId"
        """;

    public async Task<LineaDiarioResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT {Columnas}
            {DesdeYJoins}
            WHERE ld."Id" = @Id AND ld."IsDeleted" = false
            """;

        await session.EnsureOpenAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { Id = id }, session.CurrentTransaction, cancellationToken: cancellationToken);
        return await session.Connection.QuerySingleOrDefaultAsync<LineaDiarioResponse>(command);
    }

    public async Task<IReadOnlyList<LineaDiarioResponse>> ListByLoteAsync(Guid loteDiarioId, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT {Columnas}
            {DesdeYJoins}
            WHERE ld."LoteDiarioId" = @LoteDiarioId AND ld."IsDeleted" = false
            ORDER BY ld."NumeroLinea" ASC
            """;

        await session.EnsureOpenAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { LoteDiarioId = loteDiarioId }, session.CurrentTransaction, cancellationToken: cancellationToken);
        var items = await session.Connection.QueryAsync<LineaDiarioResponse>(command);
        return items.AsList();
    }
}
