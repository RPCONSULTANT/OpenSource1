using Dapper;
using OpenSource1.Core.Common;

namespace OpenSource1.Infrastructure.Data.Queries;

/// <summary>
/// Parses a raw filter expression ("juan||maria&amp;&amp;perez", "*car*") into parameterized SQL clauses.
/// Groups are split on "&amp;&amp;" (AND); each group is split on "||" (OR).
/// Column names are validated against a <see cref="ColumnasPermitidas"/> allow-list before
/// being interpolated into SQL, since only the filter values are parameterized.
/// </summary>
internal static class FilterExpressionBuilder
{
    public static void AddTextFilter(
        List<string> filters, DynamicParameters parameters, ColumnasPermitidas permitidas, string column, string? rawValue)
    {
        var groups = ParseGroups(rawValue);
        if (groups.Count == 0)
        {
            return;
        }

        var columnaCitada = permitidas.Citar(column);

        var index = 0;
        var groupClauses = new List<string>();
        foreach (var group in groups)
        {
            var orClauses = new List<string>();
            foreach (var term in group)
            {
                var paramName = $"{column}Text{index++}";
                // Escapa los metacaracteres de LIKE/ILIKE (\, %, _) del término del usuario ANTES
                // de aplicar el "*" -> "%" de nuestra sintaxis de glob, para que un término como
                // "50%" busque el literal "50%" en vez de tratar el "%" como comodín. "*" no es
                // metacaracter de LIKE, así que escapar antes no le afecta.
                var termEscapado = EscaparMetacaracteresLike(term);
                var likeValue = termEscapado.Contains('*') ? termEscapado.Replace('*', '%') : $"%{termEscapado}%";
                orClauses.Add($"{columnaCitada} ILIKE @{paramName} ESCAPE '\\'");
                parameters.Add(paramName, likeValue);
            }

            groupClauses.Add(Wrap(orClauses, "OR"));
        }

        filters.Add(Wrap(groupClauses, "AND"));
    }

    public static Result AddExactFilter<T>(
        List<string> filters, DynamicParameters parameters, ColumnasPermitidas permitidas, string column, string? rawValue,
        Func<string, (bool Ok, T Value)> tryParse)
        where T : struct
    {
        var columnaCitada = permitidas.Citar(column);

        var groups = ParseGroups(rawValue);
        if (groups.Count == 0)
        {
            return Result.Exito();
        }

        var index = 0;
        var groupClauses = new List<string>();
        foreach (var group in groups)
        {
            var orClauses = new List<string>();
            foreach (var term in group)
            {
                var (ok, value) = tryParse(term.Trim('*'));
                if (!ok)
                {
                    return Result.Fallo(new Error(
                        "filtro.valor_invalido",
                        $"El valor '{term}' no es válido para el filtro '{column}'.",
                        column));
                }

                var paramName = $"{column}Val{index++}";
                orClauses.Add($"{columnaCitada} = @{paramName}");
                parameters.Add(paramName, value);
            }

            groupClauses.Add(Wrap(orClauses, "OR"));
        }

        filters.Add(Wrap(groupClauses, "AND"));
        return Result.Exito();
    }

    /// <summary>
    /// Escapa <c>\</c>, <c>%</c> y <c>_</c> con <c>\</c> para que ILIKE los trate como literales
    /// en vez de metacaracteres del patrón. El backslash se escapa primero para no escapar dos
    /// veces los backslashes que introducen los reemplazos de <c>%</c> y <c>_</c>.
    /// </summary>
    private static string EscaparMetacaracteresLike(string valor) =>
        valor.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static List<List<string>> ParseGroups(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return [];
        }

        var groups = new List<List<string>>();
        foreach (var groupText in rawValue.Split("&&", StringSplitOptions.RemoveEmptyEntries))
        {
            var terms = groupText
                .Split("||", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(t => t.Length > 0)
                .ToList();

            if (terms.Count > 0)
            {
                groups.Add(terms);
            }
        }

        return groups;
    }

    private static string Wrap(List<string> clauses, string op) =>
        clauses.Count == 1 ? clauses[0] : $"({string.Join($" {op} ", clauses)})";
}
