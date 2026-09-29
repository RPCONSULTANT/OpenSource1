namespace OpenSource1.Core.Entities.Ventas;

/// <summary>
/// Línea de IVA de una <see cref="NotaCreditoVenta"/> posteada: un grupo de <c>CalculadoraIvaFactura</c> por
/// <see cref="IdentificadorIva"/> sobre las líneas de la nota (base e IVA redondeados a 2 por grupo), con la cuenta de IVA
/// CONGELADA en la línea de IVA del mismo identificador de la factura original. Append-only.
/// </summary>
public sealed class LineaIvaNotaCreditoVenta
{
    public long Id { get; set; }

    /// <summary>varchar(20), FK a <see cref="NotaCreditoVenta.Numero"/>.</summary>
    public required string NotaCreditoVentaNumero { get; set; }

    /// <summary>varchar(20), único por nota.</summary>
    public required string IdentificadorIva { get; set; }

    public decimal PorcentajeIva { get; set; }
    public decimal BaseImponible { get; set; }
    public decimal ImporteIva { get; set; }

    /// <summary>Cuenta de IVA de ventas de la factura original (FK a <c>CuentasContables</c>).</summary>
    public Guid CuentaIvaId { get; set; }
}
