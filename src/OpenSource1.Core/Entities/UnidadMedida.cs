namespace OpenSource1.Core.Entities;

/// <summary>
/// Unidad de medida administrable (catálogo). Reemplaza al catálogo estático hardcodeado que
/// vivía en <c>UnidadMedidaLegado</c>, que sigue existiendo hasta la Task 2.9 porque
/// <see cref="Producto"/> todavía lo usa.
/// </summary>
public sealed class UnidadMedida : BaseEntity
{
    public required string Codigo { get; set; }
    public required string Nombre { get; set; }

    /// <summary>Cantidad de decimales con que se expresan las cantidades en esta unidad (0-6).</summary>
    public short Decimales { get; set; }
}
