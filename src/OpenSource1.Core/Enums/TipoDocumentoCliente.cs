namespace OpenSource1.Core.Enums;

/// <summary>Tipo de documento de un <c>MovimientoCliente</c> (spec 6.4). Se guarda como <c>smallint</c>.</summary>
public enum TipoDocumentoCliente : short
{
    Factura = 1,
    NotaCredito = 2,
    Pago = 3,
    Ajuste = 4
}
