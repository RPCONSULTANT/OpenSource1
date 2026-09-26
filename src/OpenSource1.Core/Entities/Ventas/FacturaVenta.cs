using OpenSource1.Core.Enums;

namespace OpenSource1.Core.Entities.Ventas;

/// <summary>
/// Factura de venta POSTEADA (spec 6.2): documento legal inmutable. Replica la cabecera de <see cref="FacturaVentaBorrador"/>
/// SIN <c>Estado</c>, sin <c>xmin</c> y sin soft delete (D6), con los totales congelados (redondeados a 2). PK = <see cref="Numero"/>
/// de la serie <c>FV</c> sin huecos. Append-only con el trigger <c>libro_inventario_append_only()</c> (migración
/// <c>AddFacturasVentaYLibroClientes</c>) y sin <see cref="BaseEntity"/> ni <c>IAggregateRoot</c>: la escribe solo el motor de
/// posteo (Task 6.4) con <c>INSERT</c>; nadie la actualiza ni la borra.
/// </summary>
public sealed class FacturaVenta
{
    /// <summary>varchar(20), PK. Número de la serie <c>FV</c> (sin huecos).</summary>
    public required string Numero { get; set; }

    /// <summary>varchar(20), único: el <c>Numero</c> del borrador (<c>FV-BORR</c>) del que sale. Impide postear dos veces el mismo.</summary>
    public required string NumeroBorrador { get; set; }

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

    public DateOnly FechaRegistro { get; set; }
    public DateOnly FechaDocumento { get; set; }
    public DateOnly FechaVencimiento { get; set; }

    public Guid? TerminoPagoId { get; set; }

    public Guid GrupoNegocioId { get; set; }
    public Guid GrupoIvaNegocioId { get; set; }
    public Guid GrupoClienteContableId { get; set; }

    public Guid AlmacenId { get; set; }

    /// <summary>varchar(3).</summary>
    public required string Moneda { get; set; }

    public string? Descripcion { get; set; }

    /// <summary>numeric(18,4) redondeado a 2: Σ bases imponibles de <see cref="LineaIvaFacturaVenta"/>.</summary>
    public decimal ImporteSinIva { get; set; }

    /// <summary>numeric(18,4) redondeado a 2: Σ IVA de <see cref="LineaIvaFacturaVenta"/> (IVA agrupado, spec 6.3).</summary>
    public decimal ImporteIva { get; set; }

    /// <summary>numeric(18,4) redondeado a 2: <see cref="ImporteSinIva"/> + <see cref="ImporteIva"/>.</summary>
    public decimal ImporteTotal { get; set; }

    /// <summary>
    /// Asiento del posteo (<c>RegistrosContables</c>). Nulo solo si la factura no genera ningún movimiento contable (todas sus
    /// líneas a importe 0): el libro contable no admite importes 0.
    /// </summary>
    public long? RegistroContableId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>varchar(100).</summary>
    public required string CreatedBy { get; set; }

    /// <summary>Referencia lógica al usuario (otra base de datos): sin FK.</summary>
    public Guid? UsuarioId { get; set; }
}
