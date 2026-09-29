namespace OpenSource1.Core.Entities;

public sealed class TerminoPago : BaseEntity
{
    public required string Codigo { get; set; }
    public required string Descripcion { get; set; }
    public int DiasVencimiento { get; set; }
    public int DiasDescuento { get; set; }
    public decimal PorcentajeDescuento { get; set; }
}
