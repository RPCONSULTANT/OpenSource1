namespace OpenSource1.Core.Enums;

/// <summary>Tipo de movimiento del libro de inventario (Fase 3). Se guarda como <c>smallint</c>.</summary>
public enum TipoMovimientoInventario : short
{
    Compra = 1,
    Venta = 2,
    AjustePositivo = 3,
    AjusteNegativo = 4,
    Transferencia = 5
}
