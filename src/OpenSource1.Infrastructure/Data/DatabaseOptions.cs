namespace OpenSource1.Infrastructure.Data;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public bool ApplyMigrationsOnStartup { get; init; }
}
