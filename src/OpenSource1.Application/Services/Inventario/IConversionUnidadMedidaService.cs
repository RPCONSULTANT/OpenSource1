using OpenSource1.Core.Common;

namespace OpenSource1.Application.Services.Inventario;

/// <summary>
/// Convierte una cantidad expresada en una unidad de medida cualquiera asociada a un producto
/// a la unidad base de ese producto. Consumida por la Task 2.9 y por las Fases 3/4/6.
/// </summary>
/// <remarks>
/// Códigos de error: <c>conversion.unidad_no_asociada</c> (el producto no tiene equivalencia
/// para esa unidad), <c>conversion.producto_no_encontrado</c> y
/// <c>conversion.unidad_base_no_encontrada</c> (la unidad base del producto no existe en el
/// catálogo, por lo que no se conocen sus decimales).
/// </remarks>
public interface IConversionUnidadMedidaService
{
    Task<Result<decimal>> ConvertirABaseAsync(
        Guid productoId, Guid unidadMedidaId, decimal cantidad, CancellationToken cancellationToken = default);
}
