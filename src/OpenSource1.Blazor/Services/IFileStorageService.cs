namespace OpenSource1.Blazor.Services;

public interface IFileStorageService
{
    Task<string?> SaveClientImageAsync(HttpContext httpContext, string fieldName, string? currentRelativePath, CancellationToken cancellationToken = default);
    Task<string?> SaveProductImageAsync(HttpContext httpContext, string fieldName, string? currentRelativePath, CancellationToken cancellationToken = default);
    Task<string?> SaveProfileImageAsync(HttpContext httpContext, string fieldName, string? currentRelativePath, CancellationToken cancellationToken = default);
    Task DeleteIfExistsAsync(string? relativePath, CancellationToken cancellationToken = default);
}
