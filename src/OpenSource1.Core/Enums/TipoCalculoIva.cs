namespace OpenSource1.Core.Enums;

/// <summary>
/// Cómo se calcula el IVA de una combinación del setup de IVA (Fase 5, Task 5.4). <see cref="Exento"/> exige
/// <c>PorcentajeIva = 0</c>. Se guarda como <c>smallint</c>.
/// </summary>
public enum TipoCalculoIva : short
{
    Normal = 1,
    Exento = 2
}
