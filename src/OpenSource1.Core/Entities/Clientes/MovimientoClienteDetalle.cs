using OpenSource1.Core.Enums;

namespace OpenSource1.Core.Entities.Clientes;

/// <summary>
/// Detalle del libro de clientes (spec 6.4, equivalente a <c>Detailed Cust. Ledg. Entry</c>). Todo cambio del importe
/// pendiente de un <see cref="MovimientoCliente"/> es una fila nueva aquí; NUNCA se actualiza nada (D3). Aplicar un pago a una
/// factura inserta dos filas <see cref="TipoDetalleCliente.Aplicacion"/> de signo opuesto, una en cada movimiento, que se apuntan
/// mutuamente con <see cref="MovimientoClienteAplicadoId"/>. Append-only, sin <see cref="BaseEntity"/> ni <c>IAggregateRoot</c>.
/// </summary>
public sealed class MovimientoClienteDetalle
{
    public long Id { get; set; }

    public long MovimientoClienteId { get; set; }

    public TipoDetalleCliente TipoMovimiento { get; set; }

    /// <summary>numeric(18,4), con signo.</summary>
    public decimal Importe { get; set; }

    public DateOnly FechaRegistro { get; set; }

    /// <summary>La contraparte de una <see cref="TipoDetalleCliente.Aplicacion"/> (obligatoria en ella; siempre otro movimiento).</summary>
    public long? MovimientoClienteAplicadoId { get; set; }

    public TipoOrigenMovimiento TipoOrigen { get; set; }

    /// <summary>varchar(50).</summary>
    public required string ClaveOrigen { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>varchar(100).</summary>
    public required string CreatedBy { get; set; }

    /// <summary>Referencia lógica al usuario (otra base de datos): sin FK.</summary>
    public Guid? UsuarioId { get; set; }
}
