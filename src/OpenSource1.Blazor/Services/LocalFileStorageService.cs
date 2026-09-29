using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenSource1.Application.Storage;

namespace OpenSource1.Blazor.Services;

public sealed class LocalFileStorageService(IWebHostEnvironment environment, ILogger<LocalFileStorageService> logger) : IFileStorageService
{
    private static readonly HashSet<string> AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private const long MaxFileSizeBytes = 2 * 1024 * 1024;

    private static readonly StringComparison ComparacionDeRutas =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    // Prefijo del nombre de fichero que genera este servicio en cada carpeta ("<prefijo>-<guid hex>.<ext>").
    private static readonly Dictionary<string, string> PrefijoPorCarpeta = new(StringComparer.Ordinal)
    {
        [RutaImagen.CarpetaClientes] = "cliente",
        [RutaImagen.CarpetaProductos] = "producto",
        [RutaImagen.CarpetaUsuarios] = "perfil",
    };

    public async Task<string?> SaveClientImageAsync(HttpContext httpContext, string fieldName, string? currentRelativePath, CancellationToken cancellationToken = default)
        => await SaveImageAsync(httpContext, fieldName, currentRelativePath, RutaImagen.CarpetaClientes, cancellationToken);

    public async Task<string?> SaveProductImageAsync(HttpContext httpContext, string fieldName, string? currentRelativePath, CancellationToken cancellationToken = default)
        => await SaveImageAsync(httpContext, fieldName, currentRelativePath, RutaImagen.CarpetaProductos, cancellationToken);

    public async Task<string?> SaveProfileImageAsync(HttpContext httpContext, string fieldName, string? currentRelativePath, CancellationToken cancellationToken = default)
        => await SaveImageAsync(httpContext, fieldName, currentRelativePath, RutaImagen.CarpetaUsuarios, cancellationToken);

    /// <summary>¿Tiene el nombre la forma que genera este servicio? (<c>cliente-&lt;32 hex&gt;.png</c>, etc.)</summary>
    private static bool EsNombreGenerado(string carpeta, string nombre) =>
        PrefijoPorCarpeta.TryGetValue(carpeta, out var prefijo)
        && System.Text.RegularExpressions.Regex.IsMatch(nombre, $"^{prefijo}-[0-9a-f]{{32}}\\.(jpg|jpeg|png|webp)$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private async Task<string?> SaveImageAsync(HttpContext httpContext, string fieldName, string? currentRelativePath, string folder, CancellationToken cancellationToken)
    {
        var file = httpContext.Request.Form.Files.GetFile(fieldName);
        if (file is null || file.Length == 0)
        {
            if (!string.IsNullOrEmpty(currentRelativePath) && !RutaImagen.EsValida(currentRelativePath, folder))
            {
                logger.LogWarning("Se descarta la ruta de imagen vigente '{Ruta}': no cumple /uploads/{Carpeta}/<nombre>.", currentRelativePath, folder);
                return null;
            }

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

        var fileName = $"{PrefijoPorCarpeta[folder]}-{Guid.NewGuid():N}{extension}";
        var physicalPath = Path.Combine(uploadsRoot, fileName);

        await using (var stream = File.Create(physicalPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        logger.LogInformation("Stored image at {Folder}/{FileName}", folder, fileName);
        return $"{RutaImagen.Prefijo}{folder}/{fileName}";
    }

    public Task DeleteIfExistsAsync(string? relativePath, string carpeta, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            return Task.CompletedTask;
        }

        try
        {
            var carpetaFisica = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "storage", "uploads", carpeta));
            var prefijoCarpeta = carpetaFisica.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            var prefijoUrl = $"{RutaImagen.Prefijo}{carpeta}/";
            if (!relativePath.StartsWith(prefijoUrl, StringComparison.Ordinal))
            {
                return Rechazar(relativePath, carpeta, "no está en la carpeta de la entidad");
            }

            var fichero = Path.GetFullPath(Path.Combine(carpetaFisica, relativePath[prefijoUrl.Length..]));

            // Contención: el resultado ya normalizado debe quedar DENTRO de la carpeta de la entidad (prefijo + separador final,
            // comparación ordinal, no por cultura) y ser hijo DIRECTO de ella (sin subdirectorios).
            if (!fichero.StartsWith(prefijoCarpeta, ComparacionDeRutas)
                || !string.Equals(Path.GetDirectoryName(fichero) + Path.DirectorySeparatorChar, prefijoCarpeta, ComparacionDeRutas))
            {
                return Rechazar(relativePath, carpeta, "sale de la carpeta de la entidad");
            }

            // Solo se borran ficheros con la forma que genera este servicio: una imagen que la UI nunca creó (otro fichero de la
            // carpeta) no se toca aunque la API hubiera aceptado su nombre.
            if (!EsNombreGenerado(carpeta, Path.GetFileName(fichero)))
            {
                return Rechazar(relativePath, carpeta, "el nombre no es el de una imagen generada por la aplicación");
            }

            var info = new FileInfo(fichero);
            if (!info.Exists)
            {
                return Task.CompletedTask;
            }

            // Un enlace simbólico dentro de la carpeta podría apuntar fuera: no se sigue ni se borra.
            if (info.Attributes.HasFlag(FileAttributes.ReparsePoint) || info.LinkTarget is not null)
            {
                return Rechazar(relativePath, carpeta, "es un enlace simbólico");
            }

            info.Delete();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            logger.LogWarning(ex, "No se pudo borrar la imagen '{Ruta}'.", relativePath);
        }

        return Task.CompletedTask;
    }

    private Task Rechazar(string ruta, string carpeta, string motivo)
    {
        logger.LogWarning("Borrado de imagen rechazado para '{Ruta}' (carpeta {Carpeta}): {Motivo}.", ruta, carpeta, motivo);
        return Task.CompletedTask;
    }
}
