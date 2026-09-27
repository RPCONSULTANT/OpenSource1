using OpenSource1.Core.Common;

namespace OpenSource1.Application.Common;

/// <summary>
/// Guarda común de las vistas paginadas (Fase 7, Review Focus 5): una página cuyo desplazamiento no cabe en un entero
/// (<c>(Pagina - 1) * TamanoPagina</c> tras <see cref="PageRequest.Normalizar"/>) es 400 con <c>Campo = "Pagina"</c>. Página
/// &lt; 1 y tamaño fuera de [1, 200] no son error: se normalizan. Los listados anteriores a la Fase 7 no validan: se apoyan
/// en <see cref="PageRequest.Offset"/>, acotado a <c>int.MaxValue</c> (200 con página vacía, nunca un OFFSET negativo).
/// </summary>
internal static class PaginacionValidacion
{
    public static Error? Validar(PageRequest paginacion, string prefijoCodigo)
    {
        var normalizada = paginacion.Normalizar();
        return (long)(normalizada.Pagina - 1) * normalizada.TamanoPagina > int.MaxValue
            ? new Error($"{prefijoCodigo}.pagina_invalida", "El número de página está fuera de rango.", "Pagina")
            : null;
    }
}
