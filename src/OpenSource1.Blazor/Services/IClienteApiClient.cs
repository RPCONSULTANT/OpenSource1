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
    Guid? Id,
    string? DocumentoIdentidad,
    string? Nombre,
    bool? Activo);

public sealed record ClienteInput(
    string NombreCompleto,
    string DocumentoIdentidad,
    string Email,
    string? Telefono,
    string? Direccion,
    bool Activo);

public sealed record ClienteOperationResult(bool Succeeded, string Message, Guid? EntityId = null);
