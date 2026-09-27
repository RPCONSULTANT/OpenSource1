using OpenSource1.Core.Common;

namespace OpenSource1.Application.Common;

/// <summary>
/// Guarda común de las vistas paginadas (Fase 7, Review Focus 5): una página cuyo desplazamiento no cabe en un entero
/// (<c>(Pagina - 1) * TamanoPagina</c> tras <see cref="PageRequest.Normalizar"/>) haría un OFFSET negativo y un 500 en Postgres;
/// es 400 con <c>Campo = "Pagina"</c>. Página &lt; 1 y tamaño fuera de [1, 200] no son error: se normalizan.
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
