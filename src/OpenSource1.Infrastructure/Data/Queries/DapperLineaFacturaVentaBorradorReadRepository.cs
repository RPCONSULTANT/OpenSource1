using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.FacturasVenta.Borradores;
using OpenSource1.Application.Features.FacturasVenta.Borradores.Dtos;

namespace OpenSource1.Infrastructure.Data.Queries;

/// <summary>Sin filtros ni orden configurable (como <see cref="DapperLineaDiarioReadRepository"/>): JOIN calificados por alias.</summary>
public sealed class DapperLineaFacturaVentaBorradorReadRepository(IDbSession session) : ILineaFacturaVentaBorradorReadRepository
{
    private const string Columnas = """
        l."Id", l."FacturaVentaBorradorId", l."NumeroLinea", l."Tipo",
        l."ProductoId", p."Codigo" AS "ProductoCodigo", l."CuentaContableId", c."Numero" AS "CuentaContableNumero",
        l."Descripcion", l."AlmacenId", a."Codigo" AS "AlmacenCodigo", l."UnidadMedidaId", u."Codigo" AS "UnidadMedidaCodigo",
        l."CantidadPorUnidadMedida", l."Cantidad", l."PrecioUnitario", l."PorcentajeDescuentoLinea",
        l."ImporteDescuentoLinea", l."ImporteLinea", l."GrupoProductoId", l."GrupoIvaProductoId", l."GrupoInventarioId",
        l."IdentificadorIva", l."PorcentajeIva",
        l."CreatedAtUtc", l."UpdatedAtUtc", l."CreatedBy", l."UpdatedBy", l.xmin::text::bigint AS "Xmin"
        """;

    // LEFT JOIN: la línea no desaparece si el maestro al que apunta se borró lógicamente después.
    private const string DesdeYJoins = """
        FROM "LineasFacturaVentaBorrador" l
        LEFT JOIN "Productos" p ON p."Id" = l."ProductoId"
        LEFT JOIN "CuentasContables" c ON c."Id" = l."CuentaContableId"
        LEFT JOIN "Almacenes" a ON a."Id" = l."AlmacenId"
        LEFT JOIN "UnidadesMedida" u ON u."Id" = l."UnidadMedidaId"
        """;

    public async Task<LineaFacturaVentaBorradorResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT {Columnas}
            {DesdeYJoins}
            WHERE l."Id" = @Id AND l."IsDeleted" = false
            """;

        await session.EnsureOpenAsync(cancellationToken);
        return await session.Connection.QuerySingleOrDefaultAsync<LineaFacturaVentaBorradorResponse>(
            new CommandDefinition(sql, new { Id = id }, session.CurrentTransaction, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<LineaFacturaVentaBorradorResponse>> ListByBorradorAsync(
        Guid facturaVentaBorradorId, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT {Columnas}
            {DesdeYJoins}
            WHERE l."FacturaVentaBorradorId" = @Id AND l."IsDeleted" = false
            ORDER BY l."NumeroLinea" ASC
            """;

        await session.EnsureOpenAsync(cancellationToken);
        var items = await session.Connection.QueryAsync<LineaFacturaVentaBorradorResponse>(
            new CommandDefinition(sql, new { Id = facturaVentaBorradorId }, session.CurrentTransaction, cancellationToken: cancellationToken));
        return items.AsList();
    }
}
