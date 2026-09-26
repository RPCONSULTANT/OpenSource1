using OpenSource1.Application.Features.FacturasVenta.Borradores.Dtos;

namespace OpenSource1.Application.Features.FacturasVenta.Borradores;

public interface ILineaFacturaVentaBorradorReadRepository
{
    Task<LineaFacturaVentaBorradorResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Todas las líneas vivas del borrador, ordenadas por <c>NumeroLinea</c>. Sin paginar (tope de líneas por borrador).</summary>
    Task<IReadOnlyList<LineaFacturaVentaBorradorResponse>> ListByBorradorAsync(
        Guid facturaVentaBorradorId, CancellationToken cancellationToken = default);
}
