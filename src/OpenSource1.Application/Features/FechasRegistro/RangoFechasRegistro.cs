using System.Globalization;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.FechasRegistro;

/// <summary>
/// Rango de fechas de registro permitidas (Task 8.5): una fecha está permitida si (<see cref="Desde"/> es null o fecha ≥
/// <see cref="Desde"/>) y (<see cref="Hasta"/> es null o fecha ≤ <see cref="Hasta"/>). Los dos límites son inclusivos; un límite
/// null es "sin límite" por ese lado.
/// </summary>
public readonly record struct RangoFechasRegistro(DateOnly? Desde, DateOnly? Hasta)
{
    public bool Permite(DateOnly fecha) => (Desde is null || fecha >= Desde) && (Hasta is null || fecha <= Hasta);

    /// <summary>Texto del rango para los mensajes: "del 01/09/2026 al 30/09/2026", "desde el …", "hasta el …" o "sin límites".</summary>
    public string Describir() => (Desde, Hasta) switch
    {
        ({ } desde, { } hasta) => $"del {Formatear(desde)} al {Formatear(hasta)}",
        ({ } desde, null) => $"desde el {Formatear(desde)}",
        (null, { } hasta) => $"hasta el {Formatear(hasta)}",
        _ => "sin límites",
    };

    public static string Formatear(DateOnly fecha) => fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>Regla de alta/modificación de un rango: "desde" no puede ser posterior a "hasta".</summary>
    public static Error? ValidarLimites(DateOnly? desde, DateOnly? hasta) =>
        desde is { } d && hasta is { } h && d > h
            ? new Error(
                "registro.rango_invalido",
                $"La fecha desde ({Formatear(d)}) no puede ser posterior a la fecha hasta ({Formatear(h)}).",
                "PermitirRegistroHasta")
            : null;
}
