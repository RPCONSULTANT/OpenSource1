using MediatR;
using OpenSource1.Application.Features.Entradas.Dtos;

namespace OpenSource1.Application.Features.Entradas;

public interface IEntradaReadRepository
{
    Task<EntradaResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EntradaResponse>> ListAsync(CancellationToken cancellationToken = default);
}
