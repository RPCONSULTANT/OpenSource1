using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.SetupsContables;
using OpenSource1.Application.Features.SetupsContables.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Infrastructure.Data.Queries;

/// <summary>
/// Lectura de los tres setups contables con los códigos de sus ejes y el número/nombre de sus cuentas. Mismo patrón que
/// <see cref="DapperGrupoClienteContableReadRepository"/>: el JOIN va envuelto en una subconsulta aplanada con alias únicos y el
/// filtro/orden se aplica sobre ella; LEFT JOIN para que una fila no desaparezca si un grupo o una cuenta se borran lógicamente.
/// Orden fijo: código del eje principal y luego el secundario con el comodín (NULL) primero. Tablas y columnas interpoladas son
/// literales de esta clase, nunca texto del usuario.
/// </summary>
public sealed class DapperSetupContableReadRepository(IDbSession session) : ISetupContableReadRepository
{
    private const string Auditoria = """
        s.xmin::text::bigint AS "Xmin", s."CreatedAtUtc", s."UpdatedAtUtc", s."CreatedBy", s."UpdatedBy"
        """;

    private const string GeneralAplanado = $"""
        SELECT s."Id",
               s."GrupoNegocioId", gn."Codigo" AS "GrupoNegocioCodigo",
               s."GrupoProductoId", COALESCE(gp."Codigo", '') AS "GrupoProductoCodigo",
               s."CuentaVentasId", COALESCE(cv."Numero", '') AS "CuentaVentasNumero", COALESCE(cv."Nombre", '') AS "CuentaVentasNombre",
               s."CuentaCostoVentasId", COALESCE(cc."Numero", '') AS "CuentaCostoVentasNumero", COALESCE(cc."Nombre", '') AS "CuentaCostoVentasNombre",
               s."CuentaDescuentoVentasId", COALESCE(cd."Numero", '') AS "CuentaDescuentoVentasNumero", COALESCE(cd."Nombre", '') AS "CuentaDescuentoVentasNombre",
               s."CuentaAjusteInventarioId", COALESCE(ca."Numero", '') AS "CuentaAjusteInventarioNumero", COALESCE(ca."Nombre", '') AS "CuentaAjusteInventarioNombre",
               {Auditoria}
        FROM "SetupsContableGeneral" s
        LEFT JOIN "GruposNegocio" gn ON gn."Id" = s."GrupoNegocioId"
        LEFT JOIN "GruposProducto" gp ON gp."Id" = s."GrupoProductoId"
        LEFT JOIN "CuentasContables" cv ON cv."Id" = s."CuentaVentasId"
        LEFT JOIN "CuentasContables" cc ON cc."Id" = s."CuentaCostoVentasId"
        LEFT JOIN "CuentasContables" cd ON cd."Id" = s."CuentaDescuentoVentasId"
        LEFT JOIN "CuentasContables" ca ON ca."Id" = s."CuentaAjusteInventarioId"
        WHERE s."IsDeleted" = false
        """;

    private const string IvaAplanado = $"""
        SELECT s."Id",
               s."GrupoIvaNegocioId", gn."Codigo" AS "GrupoIvaNegocioCodigo",
               s."GrupoIvaProductoId", COALESCE(gp."Codigo", '') AS "GrupoIvaProductoCodigo",
               s."PorcentajeIva", s."IdentificadorIva", s."TipoCalculoIva",
               s."CuentaIvaVentasId", COALESCE(cv."Numero", '') AS "CuentaIvaVentasNumero", COALESCE(cv."Nombre", '') AS "CuentaIvaVentasNombre",
               s."CuentaIvaComprasId", cc."Numero" AS "CuentaIvaComprasNumero", cc."Nombre" AS "CuentaIvaComprasNombre",
               {Auditoria}
        FROM "SetupsIva" s
        LEFT JOIN "GruposIvaNegocio" gn ON gn."Id" = s."GrupoIvaNegocioId"
        LEFT JOIN "GruposIvaProducto" gp ON gp."Id" = s."GrupoIvaProductoId"
        LEFT JOIN "CuentasContables" cv ON cv."Id" = s."CuentaIvaVentasId"
        LEFT JOIN "CuentasContables" cc ON cc."Id" = s."CuentaIvaComprasId"
        WHERE s."IsDeleted" = false
        """;

    private const string InventarioAplanado = $"""
        SELECT s."Id",
               s."AlmacenId", a."Codigo" AS "AlmacenCodigo",
               s."GrupoInventarioId", COALESCE(gi."Codigo", '') AS "GrupoInventarioCodigo",
               s."CuentaInventarioId", COALESCE(ci."Numero", '') AS "CuentaInventarioNumero", COALESCE(ci."Nombre", '') AS "CuentaInventarioNombre",
               s."CuentaAjusteInventarioId", COALESCE(ca."Numero", '') AS "CuentaAjusteInventarioNumero", COALESCE(ca."Nombre", '') AS "CuentaAjusteInventarioNombre",
               s."CuentaVariacionCostoId", COALESCE(cv."Numero", '') AS "CuentaVariacionCostoNumero", COALESCE(cv."Nombre", '') AS "CuentaVariacionCostoNombre",
               {Auditoria}
        FROM "SetupsInventario" s
        LEFT JOIN "Almacenes" a ON a."Id" = s."AlmacenId"
        LEFT JOIN "GruposInventario" gi ON gi."Id" = s."GrupoInventarioId"
        LEFT JOIN "CuentasContables" ci ON ci."Id" = s."CuentaInventarioId"
        LEFT JOIN "CuentasContables" ca ON ca."Id" = s."CuentaAjusteInventarioId"
        LEFT JOIN "CuentasContables" cv ON cv."Id" = s."CuentaVariacionCostoId"
        WHERE s."IsDeleted" = false
        """;

    private sealed record Forma(string Aplanado, string ColumnaSecundaria, string CodigoSecundario, string ColumnaPrincipal, string CodigoPrincipal);

    private static readonly Forma General = new(GeneralAplanado, "GrupoNegocioId", "GrupoNegocioCodigo", "GrupoProductoId", "GrupoProductoCodigo");
    private static readonly Forma Iva = new(IvaAplanado, "GrupoIvaNegocioId", "GrupoIvaNegocioCodigo", "GrupoIvaProductoId", "GrupoIvaProductoCodigo");
    private static readonly Forma Inventario = new(InventarioAplanado, "AlmacenId", "AlmacenCodigo", "GrupoInventarioId", "GrupoInventarioCodigo");

    public Task<SetupGeneralResponse?> GetGeneralAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync<SetupGeneralResponse>(General, id, cancellationToken);

    public Task<Result<PagedResult<SetupGeneralResponse>>> ListGeneralAsync(
        SetupContableSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default) =>
        ListAsync<SetupGeneralResponse>(General, search, paginacion, cancellationToken);

    public Task<SetupIvaResponse?> GetIvaAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync<SetupIvaResponse>(Iva, id, cancellationToken);

    public Task<Result<PagedResult<SetupIvaResponse>>> ListIvaAsync(
        SetupContableSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default) =>
        ListAsync<SetupIvaResponse>(Iva, search, paginacion, cancellationToken);

    public Task<SetupInventarioResponse?> GetInventarioAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync<SetupInventarioResponse>(Inventario, id, cancellationToken);

    public Task<Result<PagedResult<SetupInventarioResponse>>> ListInventarioAsync(
        SetupContableSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default) =>
        ListAsync<SetupInventarioResponse>(Inventario, search, paginacion, cancellationToken);

    private async Task<T?> GetAsync<T>(Forma forma, Guid id, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT * FROM (
            {forma.Aplanado}
            ) x
            WHERE x."Id" = @Id
            """;

        await session.EnsureOpenAsync(cancellationToken);
        return await session.Connection.QuerySingleOrDefaultAsync<T>(
            new CommandDefinition(sql, new { Id = id }, session.CurrentTransaction, cancellationToken: cancellationToken));
    }

    private async Task<Result<PagedResult<T>>> ListAsync<T>(
        Forma forma, SetupContableSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken)
    {
        var pagina = paginacion.Normalizar();
        var filtros = new List<string>();
        var parameters = new DynamicParameters();

        if (search.SecundarioId is { } secundario)
        {
            filtros.Add($"""x."{forma.ColumnaSecundaria}" = @Secundario""");
            parameters.Add("Secundario", secundario);
        }

        if (search.PrincipalId is { } principal)
        {
            filtros.Add($"""x."{forma.ColumnaPrincipal}" = @Principal""");
            parameters.Add("Principal", principal);
        }

        var whereSql = filtros.Count == 0 ? string.Empty : Environment.NewLine + "WHERE " + string.Join(" AND ", filtros);
        parameters.Add("TamanoPagina", pagina.TamanoPagina);
        parameters.Add("Offset", pagina.Offset);

        var countSql = $"""
            SELECT COUNT(*) FROM (
            {forma.Aplanado}
            ) x
            {whereSql}
            """;

        var pageSql = $"""
            SELECT * FROM (
            {forma.Aplanado}
            ) x
            {whereSql}
            ORDER BY x."{forma.CodigoPrincipal}" ASC, x."{forma.CodigoSecundario}" ASC NULLS FIRST, x."Id" ASC
            LIMIT @TamanoPagina OFFSET @Offset
            """;

        await session.EnsureOpenAsync(cancellationToken);
        var total = await session.Connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));
        var items = await session.Connection.QueryAsync<T>(
            new CommandDefinition(pageSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));

        return Result<PagedResult<T>>.Exito(new PagedResult<T>(items.AsList(), pagina.Pagina, pagina.TamanoPagina, total));
    }
}
