namespace OpenSource1.Application.Security;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";
    public const string PolicyName = "LocalDevelopmentCors";

    public string[] AllowedOrigins { get; init; } = [];
}
