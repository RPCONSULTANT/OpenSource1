using System.Globalization;
using OpenSource1.Core.Enums;

namespace OpenSource1.Blazor.Components;

/// <summary>Rótulos en español de los enums del producto, compartidos por la UI y las exportaciones.</summary>
public static class ProductoEtiquetas
{
    /// <summary>
    /// Cultura de la app para cifras (Task 3.6: formato de <c>Existencia</c>). Compartida con el resto de la UI
    /// (paneles de productos y clientes usan la misma para moneda).
    /// </summary>
    public static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-DO");

    /// <summary>
    /// Formatea la existencia con los decimales de la unidad base del producto (0-6, ver <c>UnidadMedida.Decimales</c>),
    /// con separador de miles y sin notación científica (formato numérico fijo "N", nunca "E"/"G").
    /// </summary>
    public static string Existencia(decimal existencia, short unidadMedidaBaseDecimales)
    {
        var decimales = Math.Clamp(unidadMedidaBaseDecimales, (short)0, (short)6);
        return existencia.ToString($"N{decimales}", Cultura);
    }

    public static readonly IReadOnlyList<MetodoCosteo> Metodos = [MetodoCosteo.Promedio];

    public static readonly IReadOnlyList<BloqueoProducto> Bloqueos =
        [BloqueoProducto.Ninguno, BloqueoProducto.Venta, BloqueoProducto.Todo];

    public static string Metodo(MetodoCosteo valor) => valor switch
    {
        MetodoCosteo.Promedio => "Costo promedio",
        _ => $"Desconocido ({(short)valor})"
    };

    public static string Bloqueo(BloqueoProducto valor) => valor switch
    {
        BloqueoProducto.Ninguno => "Sin bloqueo",
        BloqueoProducto.Venta => "Bloqueado para la venta",
        BloqueoProducto.Todo => "Bloqueado totalmente",
        _ => $"Desconocido ({(short)valor})"
    };
}
