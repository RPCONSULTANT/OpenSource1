using OpenSource1.Application.Features.NotasCreditoVenta.Borradores.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.NotasCreditoVenta.Borradores;

public interface INotaCreditoVentaBorradorReadRepository
{
    Task<NotaCreditoVentaBorradorResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<NotaCreditoVentaBorradorResponse>> ListAsync(
        NotaCreditoVentaBorradorSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default);

    Task<LineaNotaCreditoVentaBorradorResponse?> GetLineaByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Todas las líneas vivas del borrador, por <c>NumeroLinea</c>.</summary>
    Task<IReadOnlyList<LineaNotaCreditoVentaBorradorResponse>> ListLineasAsync(
        Guid notaCreditoVentaBorradorId, CancellationToken cancellationToken = default);

    /// <summary>Líneas de Producto/CuentaContable de la factura del borrador con lo facturado, acreditado y pendiente.</summary>
    Task<IReadOnlyList<LineaFacturaAcreditableResponse>> ListLineasAcreditablesAsync(
        Guid notaCreditoVentaBorradorId, CancellationToken cancellationToken = default);
}
