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
    /// <summary>
    /// Conversión con REDONDEO a los decimales de la unidad base (away from zero). Solo para mostrar/estimar: ningún camino que
    /// escribe inventario la usa (una cantidad no exacta se rechaza; ver <see cref="ObtenerConversionAsync"/>).
    /// </summary>
    Task<Result<decimal>> ConvertirABaseAsync(
        Guid productoId, Guid unidadMedidaId, decimal cantidad, CancellationToken cancellationToken = default);

    /// <summary>
    /// Factor de conversión (cantidad base por unidad indicada) SIN redondear: 1 si la unidad es la base del producto
    /// (identidad, sin consultar <c>UnidadesMedidaProducto</c>); si no, el <c>CantidadPorUnidadMedida</c> de la fila
    /// asociada. Mismos códigos de error que <see cref="ConvertirABaseAsync"/>. El factor que se congela en líneas y
    /// movimientos es el de <see cref="ObtenerConversionAsync"/> (este mismo, redondeado a 6 decimales).
    /// </summary>
    Task<Result<decimal>> ObtenerFactorAsync(Guid productoId, Guid unidadMedidaId, CancellationToken ct = default);

    /// <summary>
    /// Conversión ESTRICTA: el factor ya redondeado a 6 decimales (exactamente el que se congela en las líneas y en el
    /// movimiento del libro) y los decimales de la unidad base, para convertir sin redondear con
    /// <see cref="ConversionUnidadMedida.ConvertirExacta"/>. Mismos códigos de error de resolución que
    /// <see cref="ObtenerFactorAsync"/>. Toda captura, revalidación y registro que mueve inventario usa esta variante: una
    /// cantidad cuya equivalencia en la unidad base tiene más decimales de los que admite la base se RECHAZA
    /// (<c>conversion.cantidad_no_exacta</c>), nunca se redondea (el inventario diría otra cosa que el documento).
    /// </summary>
    Task<Result<ConversionUnidadMedida>> ObtenerConversionAsync(
        Guid productoId, Guid unidadMedidaId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Factor congelable (redondeado a 6 decimales) y precisión de la unidad base de un producto para una unidad concreta.
/// </summary>
public sealed record ConversionUnidadMedida(decimal Factor, short DecimalesBase, string CodigoUnidadBase)
{
    /// <summary>Máximo de <c>numeric(18,6)</c>: límite de la cantidad en unidad base.</summary>
    public const decimal CantidadBaseMaxima = 999_999_999_999.999999m;

    /// <summary>
    /// <c>cantidad × Factor</c> SIN redondear. Falla con <c>conversion.cantidad_no_exacta</c> (Campo <c>Cantidad</c>) si
    /// el resultado tiene más decimales que <see cref="DecimalesBase"/>, y con <c>conversion.cantidad_invalida</c> si no
    /// cabe en <c>numeric(18,6)</c>.
    /// </summary>
    public Result<decimal> ConvertirExacta(decimal cantidad)
    {
        decimal cantidadBase;
        try
        {
            cantidadBase = cantidad * Factor;
        }
        catch (OverflowException)
        {
            return Result<decimal>.Fallo(CantidadDemasiadoGrande());
        }

        if (Math.Abs(cantidadBase) > CantidadBaseMaxima)
        {
            return Result<decimal>.Fallo(CantidadDemasiadoGrande());
        }

        if (decimal.Round(cantidadBase, DecimalesBase) != cantidadBase)
        {
            return Result<decimal>.Fallo(new Error(
                "conversion.cantidad_no_exacta",
                $"La cantidad {cantidad:0.######} equivale a {cantidadBase:0.############} {CodigoUnidadBase} (unidad base del " +
                $"producto), que {DecimalesAdmitidos()}: indique una cantidad cuya equivalencia en la unidad base sea exacta.",
                "Cantidad"));
        }

        return Result<decimal>.Exito(cantidadBase);
    }

    /// <summary>Texto con los decimales que admite la unidad base (para los mensajes de cantidad no exacta).</summary>
    public string DecimalesAdmitidos() => DecimalesBase == 0
        ? "no admite decimales"
        : $"admite como máximo {DecimalesBase} decimal{(DecimalesBase == 1 ? "" : "es")}";

    private static Error CantidadDemasiadoGrande() => new(
        "conversion.cantidad_invalida", "La cantidad, convertida a la unidad base del producto, es demasiado grande.", "Cantidad");
}
