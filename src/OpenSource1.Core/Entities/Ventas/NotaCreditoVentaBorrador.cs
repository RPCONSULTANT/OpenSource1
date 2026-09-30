using OpenSource1.Core.Enums;

namespace OpenSource1.Core.Entities.Ventas;

/// <summary>
/// Cabecera de un borrador de nota de crédito de venta (Task 8.6). Siempre ligada a una <see cref="FacturaVenta"/> POSTEADA
/// (<see cref="FacturaVentaNumero"/>): al crearse copia de la factura los socios, el snapshot de facturación, los grupos
/// congelados y la CxC congelada del movimiento de cliente de la factura; nada de eso se edita después (solo las fechas y la
/// descripción). Maestro con soft delete y concurrencia optimista (<c>xmin</c>). Los totales no se almacenan: se derivan de las
/// líneas (<c>GET borradores/{id}/totales</c>).
/// </summary>
public sealed class NotaCreditoVentaBorrador : BaseEntity
{
    /// <summary>Número de la serie de borradores (<see cref="SerieBorradorId"/>), inmutable.</summary>
    public required string Numero { get; set; }

    /// <summary>varchar(20), FK a <see cref="FacturaVenta.Numero"/>: la factura posteada que se acredita. Inmutable.</summary>
    public required string FacturaVentaNumero { get; set; }

    public Guid SocioNegocioId { get; set; }
    public Guid SocioNegocioFacturarAId { get; set; }

    public required string NombreFacturacion { get; set; }
    public string? RazonSocialFacturacion { get; set; }
    public TipoDocumentoFiscal TipoDocumentoFiscal { get; set; }
    public string? NumeroDocumentoFiscal { get; set; }
    public string? DireccionFacturacionLinea1 { get; set; }
    public string? DireccionFacturacionLinea2 { get; set; }
    public string? CiudadFacturacion { get; set; }
    public string? PaisCodigoFacturacion { get; set; }

    /// <summary>No anterior a la de la factura.</summary>
    public DateOnly FechaRegistro { get; set; }
    public DateOnly FechaDocumento { get; set; }

    public Guid GrupoNegocioId { get; set; }
    public Guid GrupoIvaNegocioId { get; set; }
    public Guid GrupoClienteContableId { get; set; }

    /// <summary>
    /// CxC CONGELADA del movimiento de cliente de la factura (informativa: el posteo la vuelve a leer de ese movimiento). Nula si
    /// la factura es de total 0 (Task 8.4: sin movimiento de cliente).
    /// </summary>
    public Guid? CuentaCxCId { get; set; }

    public string Moneda { get; set; } = SerieFacturaVentaIds.MonedaPorDefecto;

    public string? Descripcion { get; set; }

    public EstadoNotaCreditoBorrador Estado { get; set; } = EstadoNotaCreditoBorrador.Abierta;

    /// <summary>Serie que dio <see cref="Numero"/> (tipo BorradorNotaCreditoVenta). Fija desde la creación.</summary>
    public Guid SerieBorradorId { get; set; }

    /// <summary>Serie con la que se numerará la nota al postear (tipo NotaCreditoVenta). Editable mientras esté Abierta.</summary>
    public Guid SerieRegistroId { get; set; }

    /// <summary>Número de la nota posteada desde este borrador; solo con <see cref="EstadoNotaCreditoBorrador.Posteada"/>.</summary>
    public string? NotaCreditoVentaNumero { get; set; }
}

/// <summary>
/// Series de las notas de crédito de venta (Task 8.6), sembradas con <c>HasData</c> e Ids fijos igual que
/// <see cref="SerieFacturaVentaIds"/>: <c>NC-BORR</c> (borradores, con huecos) y <c>NC</c> (notas posteadas, sin huecos).
/// </summary>
public static class SerieNotaCreditoVentaIds
{
    public const string CodigoBorrador = "NC-BORR";
    public const string CodigoPosteada = "NC";

    public static readonly Guid SerieBorradorId = Guid.Parse("e1000000-0000-0000-0000-00000000000b");
    public static readonly Guid LineaSerieBorradorId = Guid.Parse("e1000000-0000-0000-0000-00000000000c");
    public static readonly Guid SeriePosteadaId = Guid.Parse("e1000000-0000-0000-0000-00000000000d");
    public static readonly Guid LineaSeriePosteadaId = Guid.Parse("e1000000-0000-0000-0000-00000000000e");
}
