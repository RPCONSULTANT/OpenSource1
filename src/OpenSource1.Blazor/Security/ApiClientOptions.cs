namespace OpenSource1.Blazor.Security;

public sealed class ApiClientOptions
{
    public const string SectionName = "Api";

    public Uri BaseAddress { get; init; } = new("http://localhost:8081/");
}
