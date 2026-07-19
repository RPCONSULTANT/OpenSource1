using OpenSource1.Application.Features.Clientes.Dtos;

namespace OpenSource1.Blazor.Services;

public interface IClienteApiClient
{
    Task<IReadOnlyList<ClienteResponse>> ListAsync(ClienteSearchFilter? filter = null, CancellationToken cancellationToken = default);
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
