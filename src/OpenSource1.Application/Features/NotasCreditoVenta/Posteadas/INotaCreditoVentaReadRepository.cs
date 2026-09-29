using OpenSource1.Application.Features.NotasCreditoVenta.Posteadas.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.NotasCreditoVenta.Posteadas;

/// <summary>
/// Filtros de <c>GET api/notas-credito-venta</c>: número, factura y nombre de facturación (texto con la sintaxis de
/// <c>FilterExpressionBuilder</c>), socio (vender-a o facturar-a) y rango de <c>FechaRegistro</c> (ambos incluidos).
/// </summary>
public sealed record NotaCreditoVentaSearchCriteria(
    string? Numero = null,
    string? FacturaVentaNumero = null,
    string? NombreFacturacion = null,
    Guid? SocioNegocioId = null,
    DateOnly? Desde = null,
    DateOnly? Hasta = null);

/// <summary>Consultas de las notas de crédito posteadas (las escribe solo el motor de posteo).</summary>
public interface INotaCreditoVentaReadRepository
{
    Task<NotaCreditoVentaDetalleResponse?> GetByNumeroAsync(string numero, CancellationToken cancellationToken = default);

    Task<PagedResult<NotaCreditoVentaResponse>> ListAsync(
        NotaCreditoVentaSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default);
}
