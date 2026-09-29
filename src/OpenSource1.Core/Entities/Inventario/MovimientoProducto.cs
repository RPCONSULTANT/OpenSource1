using OpenSource1.Core.Abstractions;
using OpenSource1.Core.Enums;

namespace OpenSource1.Core.Entities.Inventario;

/// <summary>
/// Fila del libro de inventario (append-only, protegido por trigger de PostgreSQL: solo INSERT y el
/// UPDATE puntual de <see cref="CantidadRestante"/> que consume la aplicación FIFO). No hereda de
/// <see cref="BaseEntity"/>: sin soft delete, sin <c>UpdatedAt</c>, sin <c>xmin</c> — el libro nunca
/// se corrige, se ajusta con un movimiento nuevo. Implementa <see cref="IAggregateRoot"/> para poder
/// usarse con <c>IUnitOfWork.Repository&lt;T&gt;()</c> si hiciera falta; la escritura normal del
/// servicio de registro (Task 3.4) puede ir por otra vía.
/// </summary>
public sealed class MovimientoProducto : IAggregateRoot
{
    public long Id { get; set; }
    public Guid ProductoId { get; set; }
    public Guid AlmacenId { get; set; }
    public TipoMovimientoInventario TipoMovimiento { get; set; }
    public TipoDocumentoInventario TipoDocumento { get; set; }

    /// <summary>varchar(20).</summary>
    public string? NumeroDocumento { get; set; }

    public int NumeroLineaDocumento { get; set; }
    public DateOnly FechaRegistro { get; set; }
    public DateOnly FechaDocumento { get; set; }

    /// <summary>numeric(18,6) con signo, en unidad BASE del producto.</summary>
    public decimal Cantidad { get; set; }

    /// <summary>numeric(18,6). Solo entradas: nace mayor que cero y la aplicación FIFO la reduce hasta cero.</summary>
    public decimal? CantidadRestante { get; set; }

    /// <summary>numeric(18,6).</summary>
    public decimal CantidadFacturada { get; set; }

    /// <summary>Unidad de medida del documento de origen (no necesariamente la base del producto).</summary>
    public Guid UnidadMedidaId { get; set; }

    /// <summary>numeric(18,6). Factor congelado al registrar (D8); 1 cuando <see cref="UnidadMedidaId"/> es la base.</summary>
    public decimal CantidadPorUnidadMedida { get; set; }

    public Guid? SocioNegocioId { get; set; }
    public TipoOrigenMovimiento TipoOrigen { get; set; }

    /// <summary>varchar(50). Clave de idempotencia del subsistema origen.</summary>
    public required string ClaveOrigen { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>varchar(100).</summary>
    public required string CreatedBy { get; set; }

    /// <summary>Referencia lógica al usuario (otra base de datos): sin FK.</summary>
    public Guid? UsuarioId { get; set; }
}
