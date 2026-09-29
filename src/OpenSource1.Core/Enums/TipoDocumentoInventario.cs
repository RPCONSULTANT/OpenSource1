namespace OpenSource1.Core.Enums;

/// <summary>
/// Documento de negocio que originó el movimiento de inventario. Se guarda como <c>smallint</c>; valores nuevos se añaden al
/// final, nunca se renumeran.
/// </summary>
public enum TipoDocumentoInventario : short
{
    Ninguno = 0,
    RegistroDiario = 1,
    FacturaVenta = 2,

    /// <summary>Devolución de una nota de crédito de venta (Task 8.6): entrada <c>Venta</c> al costo de la salida original.</summary>
    NotaCreditoVenta = 3
}
