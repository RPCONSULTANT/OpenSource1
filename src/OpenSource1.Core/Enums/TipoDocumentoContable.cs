namespace OpenSource1.Core.Enums;

/// <summary>
/// Documento de negocio que originó un asiento del libro contable (Task 5.5). Se guarda como <c>smallint</c>; valores nuevos
/// se añaden al final, nunca se renumeran.
/// </summary>
public enum TipoDocumentoContable : short
{
    Ninguno = 0,
    CostoInventario = 1,
    FacturaVenta = 2,
    Cobro = 3,

    /// <summary>Nota de crédito de venta (Task 8.6): asiento inverso al de la factura.</summary>
    NotaCreditoVenta = 4
}
