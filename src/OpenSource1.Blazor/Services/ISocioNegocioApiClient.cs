using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Blazor.Services;

public interface ISocioNegocioApiClient
{
    Task<PagedResult<SocioNegocioResponse>> ListAsync(SocioNegocioSearchFilter? filter = null, PageRequest? paginacion = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recupera todos los clientes que coincidan con el filtro, paginando internamente contra la
    /// API con el tamaño de página máximo. Uso reservado a consumidores que necesitan el conjunto
    /// completo (reportes crudos, paneles con agregados) y no a los listados paginados.
    /// </summary>
    Task<IReadOnlyList<SocioNegocioResponse>> ListAllAsync(SocioNegocioSearchFilter? filter = null, CancellationToken cancellationToken = default);

    Task<SocioNegocioResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SocioNegocioOperationResult> CreateAsync(SocioNegocioInput input, CancellationToken cancellationToken = default);
    Task<SocioNegocioOperationResult> UpdateAsync(Guid id, SocioNegocioInput input, CancellationToken cancellationToken = default);
    Task<SocioNegocioOperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record SocioNegocioSearchFilter(
    string? Nombre,
    string? Apellido,
    string? Email,
    string? Telefono,
    string? Direccion,
    string? Sector,
    string? Pais);

public sealed record SocioNegocioInput(
    string Nombre,
    string Apellido,
    string Email,
    string? Telefono,
    string? DireccionLinea1,
    string? DireccionLinea2,
    string? Sector,
    string? PaisCodigo,
    string? ImagePath = null);

public sealed record SocioNegocioOperationResult(bool Succeeded, string Message, Guid? EntityId = null);
