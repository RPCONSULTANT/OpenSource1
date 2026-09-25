using OpenSource1.Application.Features.Clientes.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Blazor.Services;

public interface IClienteApiClient
{
    Task<PagedResult<ClienteResponse>> ListAsync(ClienteSearchFilter? filter = null, PageRequest? paginacion = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recupera todos los clientes que coincidan con el filtro, paginando internamente contra la
    /// API con el tamaño de página máximo. Uso reservado a consumidores que necesitan el conjunto
    /// completo (reportes crudos, paneles con agregados) y no a los listados paginados.
    /// </summary>
    Task<IReadOnlyList<ClienteResponse>> ListAllAsync(ClienteSearchFilter? filter = null, CancellationToken cancellationToken = default);

    Task<ClienteResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ClienteOperationResult> CreateAsync(ClienteInput input, CancellationToken cancellationToken = default);
    Task<ClienteOperationResult> UpdateAsync(Guid id, ClienteInput input, CancellationToken cancellationToken = default);
    Task<ClienteOperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record ClienteSearchFilter(
    string? Nombre,
    string? Apellido,
    string? Email,
    string? Telefono,
    string? Direccion,
    string? Sector,
    string? Pais);

public sealed record ClienteInput(
    string Nombre,
    string Apellido,
    string Email,
    string? Telefono,
    string? DireccionLinea1,
    string? DireccionLinea2,
    string? Sector,
    string? PaisCodigo,
    string? ImagePath = null);

public sealed record ClienteOperationResult(bool Succeeded, string Message, Guid? EntityId = null);
