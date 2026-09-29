namespace OpenSource1.Core.Enums;

/// <summary>Tipo de línea de una factura de venta (Fase 6). Se guarda como <c>smallint</c>.</summary>
public enum TipoLineaFactura : short
{
    /// <summary>Venta de un producto: mueve inventario al postear y acredita Ventas por (GrupoNegocio × GrupoProducto).</summary>
    Producto = 1,

    /// <summary>Importe contra una cuenta contable (Posteo, no bloqueada, <c>PosteoDirecto</c>); no mueve inventario.</summary>
    CuentaContable = 2,

    /// <summary>Solo texto: sin importes.</summary>
    Comentario = 3
}
