using Microsoft.AspNetCore.Http;

namespace OpenSource1.Blazor.Services;

public interface IFileStorageService
{
    /// <summary>
    /// Guarda la imagen subida (si la hay) y devuelve su ruta. NO borra la imagen anterior: quien llama la
    /// borra con <see cref="DeleteIfExistsAsync"/> solo cuando la API confirmó el cambio (y borra la nueva si la API lo rechazó),
    /// para no dejar el registro apuntando a un fichero ya borrado. Sin fichero subido devuelve la ruta vigente si es válida
    /// (una ruta vigente que no cumple la regla se descarta: devuelve <c>null</c>).
    /// </summary>
    Task<string?> SaveClientImageAsync(HttpContext httpContext, string fieldName, string? currentRelativePath, CancellationToken cancellationToken = default);
    Task<string?> SaveProductImageAsync(HttpContext httpContext, string fieldName, string? currentRelativePath, CancellationToken cancellationToken = default);
    Task<string?> SaveProfileImageAsync(HttpContext httpContext, string fieldName, string? currentRelativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Borra la imagen <paramref name="relativePath"/> SOLO si es <c>/uploads/&lt;carpeta&gt;/&lt;fichero&gt;</c> de la
    /// <paramref name="carpeta"/> de la entidad (<c>RutaImagen.Carpeta*</c>) y el fichero resuelto es hijo directo de esa
    /// carpeta y no es un enlace simbólico. Cualquier otra ruta se ignora (aviso en el log, sin excepción).
    /// </summary>
    Task DeleteIfExistsAsync(string? relativePath, string carpeta, CancellationToken cancellationToken = default);
}
