using OpenSource1.Application.Features.UnidadesMedida.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Blazor.Services;

public interface IUnidadMedidaApiClient
{
    Task<PagedResult<UnidadMedidaResponse>> ListAsync(UnidadMedidaSearchFilter? filter = null, PageRequest? paginacion = null, CancellationToken cancellationToken = default);

    /// <summary>Recorre todas las páginas del listado; alimenta el selector de unidad base del producto.</summary>
    Task<IReadOnlyList<UnidadMedidaResponse>> ListAllAsync(CancellationToken cancellationToken = default);

    Task<UnidadMedidaResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UnidadMedidaOperationResult> CreateAsync(UnidadMedidaInput input, CancellationToken cancellationToken = default);
    Task<UnidadMedidaOperationResult> UpdateAsync(Guid id, UnidadMedidaInput input, CancellationToken cancellationToken = default);
    Task<UnidadMedidaOperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record UnidadMedidaSearchFilter(string? Codigo, string? Nombre);

public sealed record UnidadMedidaInput(
    string Codigo, string Nombre, short Decimales);

public sealed record UnidadMedidaOperationResult(bool Succeeded, string Message, Guid? EntityId = null);
