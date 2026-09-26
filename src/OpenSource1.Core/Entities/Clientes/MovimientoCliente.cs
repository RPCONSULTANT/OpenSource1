using OpenSource1.Core.Enums;

namespace OpenSource1.Core.Entities.Clientes;

/// <summary>
/// Movimiento del libro de clientes (spec 6.4, equivalente a <c>Cust. Ledger Entry</c>): una factura, nota de crédito, pago o
/// ajuste de un socio (el facturar-a). Append-only con el trigger <c>libro_inventario_append_only()</c>, sin
/// <see cref="BaseEntity"/>, sin soft delete, sin <c>xmin</c> ni <c>IAggregateRoot</c>.
/// <para>
/// SIN <c>ImporteRestante</c> y SIN <c>Abierta</c> (D1, D7): se derivan de <see cref="MovimientoClienteDetalle"/>:
/// <c>ImporteRestante = Σ detalle.Importe</c>, <c>Abierta = ImporteRestante &lt;&gt; 0</c> y el saldo del socio es la suma del
/// detalle de todos sus movimientos.
/// </para>
/// </summary>
public sealed class MovimientoCliente
{
    public long Id { get; set; }

    public Guid SocioNegocioId { get; set; }

    public DateOnly FechaRegistro { get; set; }
    public DateOnly FechaDocumento { get; set; }
    public DateOnly FechaVencimiento { get; set; }

    public TipoDocumentoCliente TipoDocumento { get; set; }

    /// <summary>varchar(20).</summary>
    public required string NumeroDocumento { get; set; }

    /// <summary>varchar(200).</summary>
    public string? Descripcion { get; set; }

    /// <summary>numeric(18,4), con signo: + factura (debe el cliente), − pago/nota de crédito.</summary>
    public decimal ImporteOriginal { get; set; }

    /// <summary>Grupo de cliente contable con el que se derivó <see cref="CuentaCxCId"/>.</summary>
    public Guid GrupoClienteContableId { get; set; }

    /// <summary>Cuenta de CxC CONGELADA al registrar (D8).</summary>
    public Guid CuentaCxCId { get; set; }

    public TipoOrigenMovimiento TipoOrigen { get; set; }

    /// <summary>varchar(50).</summary>
    public required string ClaveOrigen { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>varchar(100).</summary>
    public required string CreatedBy { get; set; }

    /// <summary>Referencia lógica al usuario (otra base de datos): sin FK.</summary>
    public Guid? UsuarioId { get; set; }
}
