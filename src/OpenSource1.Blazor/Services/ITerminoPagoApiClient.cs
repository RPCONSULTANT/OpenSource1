using OpenSource1.Application.Features.TerminosPago.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Blazor.Services;

public interface ITerminoPagoApiClient
{
    Task<PagedResult<TerminoPagoResponse>> ListAsync(TerminoPagoSearchFilter? filter = null, PageRequest? paginacion = null, CancellationToken cancellationToken = default);
    /// <summary>Recorre todas las páginas del listado (tamaño de página máximo); alimenta el selector de término de pago.</summary>
    Task<IReadOnlyList<TerminoPagoResponse>> ListAllAsync(CancellationToken cancellationToken = default);

    Task<TerminoPagoResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TerminoPagoOperationResult> CreateAsync(TerminoPagoInput input, CancellationToken cancellationToken = default);
    Task<TerminoPagoOperationResult> UpdateAsync(Guid id, TerminoPagoInput input, CancellationToken cancellationToken = default);
    Task<TerminoPagoOperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record TerminoPagoSearchFilter(string? Codigo, string? Descripcion);

public sealed record TerminoPagoInput(
    string Codigo, string Descripcion, int DiasVencimiento, int DiasDescuento, decimal PorcentajeDescuento);

public sealed record TerminoPagoOperationResult(bool Succeeded, string Message, Guid? EntityId = null);
