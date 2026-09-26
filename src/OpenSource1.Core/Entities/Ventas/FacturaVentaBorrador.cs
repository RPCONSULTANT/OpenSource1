using OpenSource1.Core.Enums;

namespace OpenSource1.Core.Entities.Ventas;

/// <summary>
/// Cabecera de un borrador de factura de venta (spec 6.1). Maestro con soft delete y concurrencia optimista (<c>xmin</c>).
/// Los datos del cliente facturar-a son un SNAPSHOT tomado al asignar el socio (no se refrescan solos), y los grupos
/// contables quedan CONGELADOS en ese momento (D8): cambiar después la clasificación del socio no altera el borrador.
/// Los totales NO se almacenan (D1): se derivan de las líneas (<c>GET borradores/{id}/totales</c>).
/// </summary>
public sealed class FacturaVentaBorrador : BaseEntity
{
    /// <summary>Número de la serie con huecos <c>FV-BORR</c> (D9), inmutable.</summary>
    public required string Numero { get; set; }

    /// <summary>Socio vender-a: da <see cref="GrupoNegocioId"/> y <see cref="GrupoIvaNegocioId"/>.</summary>
    public Guid SocioNegocioId { get; set; }

    /// <summary>Socio facturar-a (por defecto el vender-a): da el snapshot, el término de pago y <see cref="GrupoClienteContableId"/>.</summary>
    public Guid SocioNegocioFacturarAId { get; set; }

    public required string NombreFacturacion { get; set; }
    public string? RazonSocialFacturacion { get; set; }
    public TipoDocumentoFiscal TipoDocumentoFiscal { get; set; }
    public string? NumeroDocumentoFiscal { get; set; }
    public string? DireccionFacturacionLinea1 { get; set; }
    public string? DireccionFacturacionLinea2 { get; set; }
    public string? CiudadFacturacion { get; set; }
    public string? PaisCodigoFacturacion { get; set; }

    public DateOnly FechaRegistro { get; set; }
    public DateOnly FechaDocumento { get; set; }

    /// <summary>Por defecto <see cref="FechaDocumento"/> + días del término de pago (o <see cref="FechaDocumento"/> si no hay término).</summary>
    public DateOnly FechaVencimiento { get; set; }

    /// <summary>Término de pago del facturar-a, congelado al asignar el socio.</summary>
    public Guid? TerminoPagoId { get; set; }

    public Guid GrupoNegocioId { get; set; }
    public Guid GrupoIvaNegocioId { get; set; }
    public Guid GrupoClienteContableId { get; set; }

    /// <summary>Almacén por defecto de las líneas de producto (al crear, el almacén predeterminado).</summary>
    public Guid AlmacenId { get; set; }

    public EstadoFacturaBorrador Estado { get; set; } = EstadoFacturaBorrador.Abierta;

    public string Moneda { get; set; } = SerieFacturaVentaIds.MonedaPorDefecto;

    public string? Descripcion { get; set; }
}

/// <summary>
/// Series de la facturación de ventas (Task 6.2), sembradas con <c>HasData</c> e Ids fijos igual que
/// <see cref="Inventario.SerieDiarioInventarioIds"/>: <c>FV-BORR</c> (borradores, con huecos) y <c>FV</c> (facturas
/// posteadas, sin huecos; la consume la Task 6.4).
/// </summary>
public static class SerieFacturaVentaIds
{
    public const string CodigoBorrador = "FV-BORR";
    public const string CodigoPosteada = "FV";
    public const string MonedaPorDefecto = "DOP";

    public static readonly Guid SerieBorradorId = Guid.Parse("e1000000-0000-0000-0000-000000000005");
    public static readonly Guid LineaSerieBorradorId = Guid.Parse("e1000000-0000-0000-0000-000000000006");
    public static readonly Guid SeriePosteadaId = Guid.Parse("e1000000-0000-0000-0000-000000000007");
    public static readonly Guid LineaSeriePosteadaId = Guid.Parse("e1000000-0000-0000-0000-000000000008");
}
