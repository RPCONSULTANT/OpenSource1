namespace OpenSource1.Core.Entities.Ventas;

/// <summary>
/// Línea de IVA de una <see cref="FacturaVenta"/> posteada (spec 6.2, equivalente a <c>VAT Amount Line</c>): un grupo de
/// <c>CalculadoraIvaFactura</c> por <see cref="IdentificadorIva"/>, con la base y el IVA redondeados a 2 POR GRUPO (spec 6.3) y la
/// cuenta de IVA derivada al postear (congelada). Append-only, sin <see cref="BaseEntity"/> ni <c>IAggregateRoot</c>.
/// </summary>
public sealed class LineaIvaFacturaVenta
{
    public long Id { get; set; }

    /// <summary>varchar(20), FK a <see cref="FacturaVenta.Numero"/>.</summary>
    public required string FacturaVentaNumero { get; set; }

    /// <summary>varchar(20), único por factura.</summary>
    public required string IdentificadorIva { get; set; }

    /// <summary>numeric(9,5).</summary>
    public decimal PorcentajeIva { get; set; }

    /// <summary>numeric(18,4) redondeado a 2.</summary>
    public decimal BaseImponible { get; set; }

    /// <summary>numeric(18,4) redondeado a 2.</summary>
    public decimal ImporteIva { get; set; }

    /// <summary>Cuenta de IVA de ventas del setup de IVA (FK a <c>CuentasContables</c>), congelada.</summary>
    public Guid CuentaIvaId { get; set; }
}
