using Dapper;

namespace OpenSource1.Infrastructure.Data.Queries;

/// <summary>
/// Parses a raw filter expression ("juan||maria&amp;&amp;perez", "*car*") into parameterized SQL clauses.
/// Groups are split on "&amp;&amp;" (AND); each group is split on "||" (OR).
/// </summary>
internal static class FilterExpressionBuilder
{
    public static void AddTextFilter(List<string> filters, DynamicParameters parameters, string column, string? rawValue)
    {
        var groups = ParseGroups(rawValue);
        if (groups.Count == 0)
        {
            return;
        }

        var index = 0;
        var groupClauses = new List<string>();
        foreach (var group in groups)
        {
            var orClauses = new List<string>();
            foreach (var term in group)
            {
                var paramName = $"{column}Text{index++}";
                var likeValue = term.Contains('*') ? term.Replace('*', '%') : $"%{term}%";
                orClauses.Add($"\"{column}\" ILIKE @{paramName}");
                parameters.Add(paramName, likeValue);
            }

            groupClauses.Add(Wrap(orClauses, "OR"));
        }

        filters.Add(Wrap(groupClauses, "AND"));
    }

    public static void AddExactFilter<T>(
        List<string> filters, DynamicParameters parameters, string column, string? rawValue, Func<string, (bool Ok, T Value)> tryParse)
        where T : struct
    {
        var groups = ParseGroups(rawValue);
        if (groups.Count == 0)
        {
            return;
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
                    continue;
                }

                var paramName = $"{column}Val{index++}";
                orClauses.Add($"\"{column}\" = @{paramName}");
                parameters.Add(paramName, value);
            }

            if (orClauses.Count > 0)
            {
                groupClauses.Add(Wrap(orClauses, "OR"));
            }
        }

        if (groupClauses.Count > 0)
        {
            filters.Add(Wrap(groupClauses, "AND"));
        }
    }

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
