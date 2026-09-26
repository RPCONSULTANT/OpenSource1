using OpenSource1.Core.Enums;

namespace OpenSource1.Core.Entities.Contabilidad;

/// <summary>
/// Intersección grupo de IVA de negocio × grupo de IVA de producto (spec 5.3, <c>SetupsIva</c>): tasa, cuentas de impuesto e
/// <see cref="IdentificadorIva"/> (clave de agrupación del cálculo de IVA, spec 6.3). <see cref="GrupoIvaProductoId"/> es el eje
/// principal (obligatorio); <see cref="GrupoIvaNegocioId"/> <see langword="null"/> es el comodín.
/// </summary>
public sealed class SetupIva : BaseEntity
{
    public Guid? GrupoIvaNegocioId { get; set; }

    public Guid GrupoIvaProductoId { get; set; }

    /// <summary>0-100 con hasta 5 decimales (<c>numeric(9,5)</c>); 0 si <see cref="TipoCalculoIva"/> es Exento.</summary>
    public decimal PorcentajeIva { get; set; }

    public Guid CuentaIvaVentasId { get; set; }

    public Guid? CuentaIvaComprasId { get; set; }

    public string IdentificadorIva { get; set; } = string.Empty;

    public TipoCalculoIva TipoCalculoIva { get; set; } = TipoCalculoIva.Normal;
}
