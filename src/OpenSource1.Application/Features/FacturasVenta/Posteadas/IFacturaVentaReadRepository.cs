using OpenSource1.Application.Features.FacturasVenta.Posteadas.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.FacturasVenta.Posteadas;

/// <summary>Consultas de las facturas de venta posteadas (Task 6.3). Las escribe solo el motor de posteo (Task 6.4).</summary>
public interface IFacturaVentaReadRepository
{
    /// <summary>Cabecera, líneas (por <c>NumeroLinea</c>) y líneas de IVA (por identificador), o <c>null</c> si no existe.</summary>
    Task<FacturaVentaDetalleResponse?> GetByNumeroAsync(string numero, CancellationToken cancellationToken = default);

    Task<PagedResult<FacturaVentaResponse>> ListAsync(
        FacturaVentaSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default);
}
