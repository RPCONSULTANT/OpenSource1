using OpenSource1.Core.Common;

namespace OpenSource1.Application.Services.Inventario;

/// <summary>
/// Convierte una cantidad expresada en una unidad de medida cualquiera asociada a un producto
/// a la unidad base de ese producto. Consumida por la Task 2.9 y por las Fases 3/4/6.
/// </summary>
/// <remarks>
/// Si la unidad indicada es la unidad base del producto (<c>Producto.UnidadMedidaBaseId</c>) el
/// factor es 1 (identidad) y no se consulta <c>UnidadesMedidaProducto</c>: la fila de la unidad
/// base no se almacena, y si existiera una con otro factor gana la identidad. El resultado se
/// redondea con los decimales de la unidad base (away from zero).
/// <para>
/// Códigos de error: <c>conversion.producto_no_encontrado</c> (el producto no existe o está
/// borrado lógicamente), <c>conversion.unidad_no_asociada</c> (la unidad no es la base y el
/// producto no tiene equivalencia para ella) y
/// <c>conversion.unidad_base_no_encontrada</c> (la unidad base del producto no existe en el
/// catálogo, por lo que no se conocen sus decimales).
/// </para>
/// </remarks>
public interface IConversionUnidadMedidaService
{
    Task<Result<decimal>> ConvertirABaseAsync(
        Guid productoId, Guid unidadMedidaId, decimal cantidad, CancellationToken cancellationToken = default);

    /// <summary>
    /// Factor de conversión (cantidad base por unidad indicada) SIN redondear: 1 si la unidad es la base del producto
    /// (identidad, sin consultar <c>UnidadesMedidaProducto</c>); si no, el <c>CantidadPorUnidadMedida</c> de la fila
    /// asociada. Mismos códigos de error que <see cref="ConvertirABaseAsync"/>. Es el factor que el libro de
    /// inventario congela en el movimiento (nunca se deduce dividiendo un resultado ya redondeado).
    /// </summary>
    Task<Result<decimal>> ObtenerFactorAsync(Guid productoId, Guid unidadMedidaId, CancellationToken ct = default);
}
