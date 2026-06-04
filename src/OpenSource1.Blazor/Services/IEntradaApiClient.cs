using OpenSource1.Application.Features.Entradas.Dtos;

namespace OpenSource1.Blazor.Services;

public interface IEntradaApiClient
{
    Task<IReadOnlyList<EntradaResponse>> ListAsync(CancellationToken cancellationToken = default);
    Task<EntradaOperationResult> CreateAsync(EntradaInput input, CancellationToken cancellationToken = default);
    Task<EntradaOperationResult> UpdateAsync(Guid id, EntradaInput input, CancellationToken cancellationToken = default);
    Task<EntradaOperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record EntradaInput(string Titulo, string? Descripcion, string Tipo, string Estado);
public sealed record EntradaOperationResult(bool Succeeded, string Message);
