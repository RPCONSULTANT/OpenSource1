using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.SociosNegocio;
using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Infrastructure.Data.Queries;

public sealed class DapperSocioNegocioReadRepository(IDbSession session) : ISocioNegocioReadRepository
{
    private static readonly ColumnasPermitidas ColumnasPermitidas = new(
        "Codigo", "Tipo", "NombreComercial", "NumeroDocumentoFiscal", "Email", "Telefono", "DireccionLinea1", "Sector", "PaisNombre", "CreatedAtUtc");

    // Grupos contables (Task 5.3): Id y Código de cada grupo. Subconsultas escalares correlacionadas (por PK) en vez de JOIN: este
    // repositorio filtra y ordena con nombres de columna SIN alias de tabla ("Codigo", "Id", "CreatedAtUtc"...), que un JOIN a
    // las tablas de grupos (que también los tienen) volvería ambiguos. Dentro de cada subconsulta todo va calificado.
    private const string ColumnasGrupos = """
        "GrupoNegocioId", (SELECT g."Codigo" FROM "GruposNegocio" g WHERE g."Id" = "SociosNegocio"."GrupoNegocioId") AS "GrupoNegocioCodigo",
        "GrupoIvaNegocioId", (SELECT g."Codigo" FROM "GruposIvaNegocio" g WHERE g."Id" = "SociosNegocio"."GrupoIvaNegocioId") AS "GrupoIvaNegocioCodigo",
        "GrupoClienteContableId", (SELECT g."Codigo" FROM "GruposClienteContable" g WHERE g."Id" = "SociosNegocio"."GrupoClienteContableId") AS "GrupoClienteContableCodigo"
        """;

    public async Task<SocioNegocioResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT "Id", "Codigo", "Tipo", "NombreComercial", "RazonSocial", "TipoDocumentoFiscal", "NumeroDocumentoFiscal", "Email", "Telefono", "DireccionLinea1", "DireccionLinea2", "Ciudad", "Sector", "PaisCodigo", "PaisNombre", "TerminoPagoId", "LimiteCredito", "Bloqueado", "ImagePath", {ColumnasGrupos}, "CreatedAtUtc", "UpdatedAtUtc", "CreatedBy", "UpdatedBy"
            FROM "SociosNegocio"
            WHERE "Id" = @Id AND "IsDeleted" = false
            """;
        await session.EnsureOpenAsync(cancellationToken);
        return await session.Connection.QuerySingleOrDefaultAsync<SocioNegocioResponse>(new CommandDefinition(sql, new { Id = id }, session.CurrentTransaction, cancellationToken: cancellationToken));
    }

    public async Task<Result<PagedResult<SocioNegocioResponse>>> ListAsync(
        SocioNegocioSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default)
    {
        var pagina = paginacion.Normalizar();

        var filters = new List<string>();
        var parameters = new DynamicParameters();

        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "Codigo", search.Codigo);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "NumeroDocumentoFiscal", search.NumeroDocumentoFiscal);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "NombreComercial", search.NombreComercial);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "Email", search.Email);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "Telefono", search.Telefono);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "DireccionLinea1", search.DireccionLinea1);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "Sector", search.Sector);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "PaisNombre", search.PaisNombre);

        if (search.Tipo is { } tipo)
        {
            // Igualdad exacta sobre el smallint (no ILIKE). Un valor fuera del enum es un filtro mal formado.
            if (!Enum.IsDefined(tipo))
            {
                return Result<PagedResult<SocioNegocioResponse>>.Fallo(new Error(
                    "filtro.valor_invalido",
                    $"El valor '{(short)tipo}' no es válido para el filtro 'Tipo' (1=Cliente, 2=Proveedor, 3=Ambos).",
                    "Tipo"));
            }

            filters.Add(ColumnasPermitidas.Citar("Tipo") + " = @Tipo");
            parameters.Add("Tipo", (short)tipo);
        }

        filters.Insert(0, "\"IsDeleted\" = false");
        var whereSql = Environment.NewLine + "WHERE " + string.Join(" AND ", filters);

        var ordenColumna = ColumnasPermitidas.EsValida(pagina.OrdenarPor) ? pagina.OrdenarPor! : "CreatedAtUtc";
        var ordenSql = ColumnasPermitidas.Citar(ordenColumna);
        var direccionSql = pagina.Descendente ? "DESC" : "ASC";

        parameters.Add("TamanoPagina", pagina.TamanoPagina);
        parameters.Add("Offset", pagina.Offset);

        var countSql = $"""
            SELECT COUNT(*) FROM "SociosNegocio"
            {whereSql}
            """;

        var pageSql = $"""
            SELECT "Id", "Codigo", "Tipo", "NombreComercial", "RazonSocial", "TipoDocumentoFiscal", "NumeroDocumentoFiscal", "Email", "Telefono", "DireccionLinea1", "DireccionLinea2", "Ciudad", "Sector", "PaisCodigo", "PaisNombre", "TerminoPagoId", "LimiteCredito", "Bloqueado", "ImagePath", {ColumnasGrupos}, "CreatedAtUtc", "UpdatedAtUtc", "CreatedBy", "UpdatedBy"
            FROM "SociosNegocio"
            {whereSql}
            ORDER BY {ordenSql} {direccionSql}, "Id" ASC
            LIMIT @TamanoPagina OFFSET @Offset
            """;

        await session.EnsureOpenAsync(cancellationToken);

        var total = await session.Connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));

        var items = await session.Connection.QueryAsync<SocioNegocioResponse>(
            new CommandDefinition(pageSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));

        return Result<PagedResult<SocioNegocioResponse>>.Exito(
            new PagedResult<SocioNegocioResponse>(items.AsList(), pagina.Pagina, pagina.TamanoPagina, total));
    }
}
