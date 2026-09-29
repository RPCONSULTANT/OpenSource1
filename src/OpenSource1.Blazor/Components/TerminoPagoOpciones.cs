using OpenSource1.Application.Features.TerminosPago.Dtos;
using OpenSource1.Blazor.Services;

namespace OpenSource1.Blazor.Components;

/// <summary>Términos de pago elegibles para el <c>&lt;select&gt;</c> del formulario y si su carga fue fiable.</summary>
public sealed record TerminoPagoOpciones(IReadOnlyList<TerminoPagoResponse> Items, bool CargaFallida)
{
    /// <summary>Tiempo máximo de la carga: un listado colgado no debe bloquear la página los 100 s del HttpClient.</summary>
    public static readonly TimeSpan TiempoMaximo = TimeSpan.FromSeconds(5);

    public static readonly TerminoPagoOpciones SinCargar = new([], false);

    public const string MensajeNoDisponibles =
        "No fue posible cargar los términos de pago. Por seguridad no se puede guardar la modificación hasta que carguen (el término de pago actual no se tocaría); recargue la página.";

    public const string MensajeNoDisponiblesAlta =
        "No fue posible cargar los términos de pago: puede guardar el cliente sin término de pago y asignarlo después.";

    /// <summary>
    /// Carga todos los términos (todas las páginas) con un tiempo máximo. Cualquier fallo (excepción,
    /// error HTTP, cuerpo cortado, tiempo agotado) marca <see cref="CargaFallida"/> en vez de propagarse.
    /// </summary>
    public static async Task<TerminoPagoOpciones> CargarAsync(ITerminoPagoApiClient client, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TiempoMaximo);

        try
        {
            return new TerminoPagoOpciones(await client.ListAllAsync(cts.Token), false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load términos de pago options from API.");
            return new TerminoPagoOpciones([], true);
        }
    }

    /// <summary>
    /// ¿Se puede guardar una MODIFICACIÓN? No si la carga falló, ni si vino vacía pese a que el socio tiene
    /// un término asignado (respuesta incoherente: el selector no reflejaría la realidad). Un catálogo vacío de un
    /// socio sin término es legítimo (el catálogo no trae datos de fábrica).
    /// </summary>
    public bool ModificacionBloqueada(Guid? terminoActual) =>
        CargaFallida || (Items.Count == 0 && terminoActual.HasValue);

    /// <summary>Nombre del término vigente para rotular su opción cuando no está en <see cref="Items"/>.</summary>
    public static async Task<string?> NombreAsync(ITerminoPagoApiClient client, Guid id, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TiempoMaximo);

        try
        {
            var termino = await client.GetByIdAsync(id, cts.Token);
            return termino is null ? null : $"{termino.Codigo} — {termino.Descripcion}";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load término de pago {Id} name from API.", id);
            return null;
        }
    }
}
