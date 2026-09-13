namespace OpenSource1.Core.Entities;

/// <summary>
/// Rango numérico vigente de una <see cref="Serie"/> a partir de una fecha determinada.
/// <see cref="UltimoNumeroUsado"/> guarda el último entero emitido como texto (para permitir
/// prefijos alfabéticos en <see cref="NumeroInicial"/>/<see cref="NumeroFinal"/> si se
/// necesitan en el futuro, aunque hoy solo se emite el componente numérico).
/// </summary>
public sealed class LineaSerie : BaseEntity
{
    public Guid SerieId { get; set; }
    public required string NumeroInicial { get; set; }
    public required string NumeroFinal { get; set; }
    public required string UltimoNumeroUsado { get; set; }
    public DateOnly FechaInicial { get; set; }
    public int Incremento { get; set; } = 1;
    public bool Bloqueada { get; set; }
}
