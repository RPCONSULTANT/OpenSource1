namespace OpenSource1.Core.Enums;

/// <summary>
/// Tipo de una fila de <c>MovimientosClienteDetalle</c> (spec 6.4, equivalente a <c>Detailed Cust. Ledg. Entry</c>). Se guarda
/// como <c>smallint</c>. Solo <see cref="Aplicacion"/> apunta a la contraparte (<c>MovimientoClienteAplicadoId</c>).
/// </summary>
public enum TipoDetalleCliente : short
{
    ImporteInicial = 1,
    Pago = 2,
    Aplicacion = 3,
    Descuento = 4,
    Redondeo = 5
}
