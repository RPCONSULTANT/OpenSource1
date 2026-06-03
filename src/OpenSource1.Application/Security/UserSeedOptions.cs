namespace OpenSource1.Application.Security;

public sealed class UserSeedOptions
{
    public const string SectionName = "UserSeed";

    public bool Enabled { get; init; }
    public string? DefaultPassword { get; init; }
}
