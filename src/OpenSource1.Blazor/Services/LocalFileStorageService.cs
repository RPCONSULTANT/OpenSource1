namespace OpenSource1.Blazor.Services;

public sealed class LocalFileStorageService(IWebHostEnvironment environment, ILogger<LocalFileStorageService> logger) : IFileStorageService
{
    private static readonly HashSet<string> AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private const long MaxFileSizeBytes = 2 * 1024 * 1024;

    public async Task<string?> SaveClientImageAsync(HttpContext httpContext, string fieldName, string? currentRelativePath, CancellationToken cancellationToken = default)
        => await SaveImageAsync(httpContext, fieldName, currentRelativePath, "clientes", "cliente", cancellationToken);

    public async Task<string?> SaveProductImageAsync(HttpContext httpContext, string fieldName, string? currentRelativePath, CancellationToken cancellationToken = default)
        => await SaveImageAsync(httpContext, fieldName, currentRelativePath, "productos", "producto", cancellationToken);

    public async Task<string?> SaveProfileImageAsync(HttpContext httpContext, string fieldName, string? currentRelativePath, CancellationToken cancellationToken = default)
        => await SaveImageAsync(httpContext, fieldName, currentRelativePath, "users", "perfil", cancellationToken);

    private async Task<string?> SaveImageAsync(HttpContext httpContext, string fieldName, string? currentRelativePath, string folder, string prefix, CancellationToken cancellationToken)
    {
        var file = httpContext.Request.Form.Files.GetFile(fieldName);
        if (file is null || file.Length == 0)
        {
            return currentRelativePath;
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("La imagen debe ser JPG, JPEG, PNG o WEBP.");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            throw new InvalidOperationException("La imagen no puede superar 2 MB.");
        }

        var uploadsRoot = Path.Combine(environment.ContentRootPath, "storage", "uploads", folder);
        Directory.CreateDirectory(uploadsRoot);

        var fileName = $"{prefix}-{Guid.NewGuid():N}{extension}";
        var physicalPath = Path.Combine(uploadsRoot, fileName);

        await using (var stream = File.Create(physicalPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(currentRelativePath) && !string.Equals(currentRelativePath, $"/uploads/{folder}/{fileName}", StringComparison.OrdinalIgnoreCase))
        {
            await DeleteIfExistsAsync(currentRelativePath, cancellationToken);
        }

        logger.LogInformation("Stored client image at {FileName}", fileName);
        return $"/uploads/{folder}/{fileName}";
    }

    public Task DeleteIfExistsAsync(string? relativePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return Task.CompletedTask;
        }

        var trimmed = relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var relativeStoragePath = trimmed.StartsWith("uploads") ? Path.Combine("storage", trimmed) : trimmed;
        var physicalPath = Path.Combine(environment.ContentRootPath, relativeStoragePath);

        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }

        return Task.CompletedTask;
    }
}
