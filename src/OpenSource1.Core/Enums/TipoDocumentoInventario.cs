namespace OpenSource1.Core.Enums;

/// <summary>Documento de negocio que originó el movimiento de inventario. Se guarda como <c>smallint</c>.</summary>
public enum TipoDocumentoInventario : short
{
    Ninguno = 0,
    RegistroDiario = 1,
    FacturaVenta = 2
}
