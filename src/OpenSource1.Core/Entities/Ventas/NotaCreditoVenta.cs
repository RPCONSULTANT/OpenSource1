using OpenSource1.Core.Enums;

namespace OpenSource1.Core.Entities.Ventas;

/// <summary>
/// Nota de crédito de venta POSTEADA (Task 8.6): documento legal inmutable ligado a su <see cref="FacturaVentaNumero"/>. Replica la
/// cabecera del borrador con los totales congelados (redondeados a 2). PK = <see cref="Numero"/> de la serie <c>NC</c> sin
/// huecos. Append-only con el trigger <c>libro_inventario_append_only()</c> (migración <c>AddNotasCreditoVenta</c>), sin
/// <see cref="BaseEntity"/>, <c>xmin</c> ni <c>IAggregateRoot</c>: la escribe solo el motor de posteo con <c>INSERT</c>.
/// </summary>
public sealed class NotaCreditoVenta
{
    /// <summary>varchar(20), PK. Número de la serie <c>NC</c> (sin huecos).</summary>
    public required string Numero { get; set; }

    /// <summary>varchar(20), único: el <c>Numero</c> del borrador (<c>NC-BORR</c>) del que sale.</summary>
    public required string NumeroBorrador { get; set; }

    /// <summary>varchar(20), FK a <see cref="FacturaVenta.Numero"/>.</summary>
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

    public DateOnly FechaRegistro { get; set; }
    public DateOnly FechaDocumento { get; set; }

    public Guid GrupoNegocioId { get; set; }
    public Guid GrupoIvaNegocioId { get; set; }
    public Guid GrupoClienteContableId { get; set; }

    /// <summary>CxC congelada de la factura con la que se acreditó al cliente; nula en una nota de total 0 (sin movimiento de cliente).</summary>
    public Guid? CuentaCxCId { get; set; }

    /// <summary>varchar(3).</summary>
    public required string Moneda { get; set; }

    public string? Descripcion { get; set; }

    /// <summary>numeric(18,4) redondeado a 2: Σ bases imponibles de <see cref="LineaIvaNotaCreditoVenta"/>.</summary>
    public decimal ImporteSinIva { get; set; }

    /// <summary>numeric(18,4) redondeado a 2: Σ IVA de <see cref="LineaIvaNotaCreditoVenta"/>.</summary>
    public decimal ImporteIva { get; set; }

    /// <summary>numeric(18,4) redondeado a 2: <see cref="ImporteSinIva"/> + <see cref="ImporteIva"/> (positivo o 0).</summary>
    public decimal ImporteTotal { get; set; }

    /// <summary>Asiento del posteo; nulo en una nota de total 0 (devolución de un obsequio: solo documento e inventario).</summary>
    public long? RegistroContableId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>varchar(100).</summary>
    public required string CreatedBy { get; set; }

    /// <summary>Referencia lógica al usuario (otra base de datos): sin FK.</summary>
    public Guid? UsuarioId { get; set; }
}
