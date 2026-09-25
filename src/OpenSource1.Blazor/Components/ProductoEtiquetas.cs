using OpenSource1.Core.Enums;

namespace OpenSource1.Blazor.Components;

/// <summary>Rótulos en español de los enums del producto, compartidos por la UI y las exportaciones.</summary>
public static class ProductoEtiquetas
{
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
