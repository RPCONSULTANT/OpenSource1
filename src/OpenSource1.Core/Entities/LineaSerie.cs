namespace OpenSource1.Core.Entities;

/// <summary>
/// Rango de una <see cref="Serie"/> vigente desde <see cref="FechaInicial"/>. Números con prefijo: <see cref="NumeroInicial"/>,
/// <see cref="NumeroFinal"/> y <see cref="NumeroAviso"/> comparten prefijo y ancho; <see cref="UltimoNumeroUsado"/> guarda el
/// último número emitido con su formato completo (vacío o menor que el inicial = línea sin usar).
/// </summary>
public sealed class LineaSerie : BaseEntity
{
    public Guid SerieId { get; set; }
    public required string NumeroInicial { get; set; }
    public required string NumeroFinal { get; set; }

    /// <summary>Opcional, mismo prefijo y ancho que el rango: al alcanzarlo, el número emitido lleva una advertencia.</summary>
    public string? NumeroAviso { get; set; }

    public required string UltimoNumeroUsado { get; set; }
    public DateOnly FechaInicial { get; set; }
    public int Incremento { get; set; } = 1;
    public bool Bloqueada { get; set; }
}
