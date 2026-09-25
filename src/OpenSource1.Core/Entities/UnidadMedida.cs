namespace OpenSource1.Core.Entities;

/// <summary>
/// Unidad de medida administrable (catálogo). <see cref="Producto"/> la referencia por
/// <c>UnidadMedidaBaseId</c>.
/// </summary>
public sealed class UnidadMedida : BaseEntity
{
    public required string Codigo { get; set; }
    public required string Nombre { get; set; }

    /// <summary>Cantidad de decimales con que se expresan las cantidades en esta unidad (0-6).</summary>
    public short Decimales { get; set; }
}
