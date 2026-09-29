using OpenSource1.Application.Features.MovimientosCliente.Dtos;
using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Blazor.Services;

namespace OpenSource1.Blazor.Components;

/// <summary>
/// Apoyo común de las vistas de clientes (Task 7.3): etiquetas de los tramos de antigüedad, días vencidos de un documento, el
/// socio vigente del select y la lectura tolerante de casillas/booleanos de la query string.
/// </summary>
public static class EstadoCuentaOpciones
{
    /// <summary>Tramos en el orden de la tabla (sin el total).</summary>
    public static IReadOnlyList<(string Etiqueta, decimal Valor)> Tramos(EstadoCuentaTramos t) =>
    [
        ("Corriente", t.Corriente),
        ("1-30 días", t.Dias1a30),
        ("31-60 días", t.Dias31a60),
        ("61-90 días", t.Dias61a90),
        ("Más de 90", t.Mas90),
        ("Sin aplicar", t.SinAplicar),
    ];

    /// <summary>
    /// Días vencidos de un documento con restante positivo respecto a <paramref name="referencia"/> (la fecha de corte; 0 si aún
    /// no vence). Documentos cerrados o con restante negativo (pagos sin aplicar) no vencen: null.
    /// </summary>
    public static int? DiasVencido(MovimientoClienteResponse movimiento, DateOnly referencia) =>
        movimiento.ImporteRestante > 0 ? Math.Max(referencia.DayNumber - movimiento.FechaVencimiento.DayNumber, 0) : null;

    /// <summary>"true"/"false" (sin distinguir mayúsculas) de la query string; vacío o no interpretable = null.</summary>
    public static bool? ParseBool(string? texto) => bool.TryParse(texto?.Trim(), out var valor) ? valor : null;

    /// <summary>Aviso si <paramref name="texto"/> no está vacío y no es un booleano (ver <see cref="InventarioVistasOpciones.FiltroInvalido"/>).</summary>
    public static string? BoolInvalido(string nombre, string? texto) =>
        string.IsNullOrWhiteSpace(texto) || ParseBool(texto) is not null ? null : $"El filtro «{nombre}» de la dirección no es válido ('{texto}').";

    /// <summary>El socio elegido, aunque no esté entre las opciones de la búsqueda (el select muestra siempre el valor vigente).</summary>
    public static async Task<SocioNegocioResponse?> SocioVigenteAsync(
        ISocioNegocioApiClient client, Guid? socioId, IReadOnlyList<SocioNegocioResponse> opciones, ILogger logger)
    {
        if (socioId is not { } id)
        {
            return null;
        }

        var enLista = opciones.FirstOrDefault(s => s.Id == id);
        if (enLista is not null)
        {
            return enLista;
        }

        try
        {
            return await client.GetByIdAsync(id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load socio {Id}.", id);
            return null;
        }
    }
}
