using OpenSource1.Application.Features.TerminosPago.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Blazor.Services;

public interface ITerminoPagoApiClient
{
    Task<PagedResult<TerminoPagoResponse>> ListAsync(TerminoPagoSearchFilter? filter = null, PageRequest? paginacion = null, CancellationToken cancellationToken = default);
    Task<TerminoPagoOperationResult> CreateAsync(TerminoPagoInput input, CancellationToken cancellationToken = default);
    Task<TerminoPagoOperationResult> UpdateAsync(Guid id, TerminoPagoInput input, CancellationToken cancellationToken = default);
    Task<TerminoPagoOperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record TerminoPagoSearchFilter(string? Codigo, string? Descripcion);

public sealed record TerminoPagoInput(
    string Codigo, string Descripcion, int DiasVencimiento, int DiasDescuento, decimal PorcentajeDescuento);

public sealed record TerminoPagoOperationResult(bool Succeeded, string Message, Guid? EntityId = null);
