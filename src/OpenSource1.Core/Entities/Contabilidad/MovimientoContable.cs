using OpenSource1.Core.Enums;

namespace OpenSource1.Core.Entities.Contabilidad;

/// <summary>
/// Fila del libro contable (spec 5.5, equivalente a <c>G/L Entry</c>): append-only con el trigger
/// <c>libro_inventario_append_only()</c> (migración <c>AddLibroContable</c>), sin <see cref="BaseEntity"/>, sin soft delete ni
/// <c>xmin</c>. Lo escribe SOLO <c>IRegistroContable</c> (Dapper, <c>INSERT</c>); nadie lo actualiza ni lo borra.
/// </summary>
/// <remarks>
/// Deliberadamente NO implementa <c>IAggregateRoot</c> (mismo motivo que <c>MovimientoValor</c>): así
/// <c>IUnitOfWork.Repository&lt;T&gt;()</c> no compila con esta entidad y nadie puede escribirla por el change tracker.
/// </remarks>
public sealed class MovimientoContable
{
    public long Id { get; set; }

    public Guid CuentaContableId { get; set; }

    /// <summary>varchar(20). Número de la cuenta CONGELADO al registrar (D8): no cambia si luego se renumera la cuenta.</summary>
    public required string NumeroCuenta { get; set; }

    public DateOnly FechaRegistro { get; set; }
    public DateOnly FechaDocumento { get; set; }
    public TipoDocumentoContable TipoDocumento { get; set; }

    /// <summary>varchar(20).</summary>
    public string? NumeroDocumento { get; set; }

    /// <summary>varchar(200).</summary>
    public required string Descripcion { get; set; }

    /// <summary>numeric(18,4), distinto de cero, con signo: + débito, − crédito. La suma por registro es siempre 0.</summary>
    public decimal Importe { get; set; }

    /// <summary>numeric(18,4): <c>max(Importe, 0)</c>. Desglose para reporte.</summary>
    public decimal Debito { get; set; }

    /// <summary>numeric(18,4): <c>max(-Importe, 0)</c>. Desglose para reporte.</summary>
    public decimal Credito { get; set; }

    public long RegistroContableId { get; set; }

    /// <summary>Dimensiones de análisis (nulables).</summary>
    public Guid? SocioNegocioId { get; set; }
    public Guid? ProductoId { get; set; }

    /// <summary>Grupos congelados (D8) con los que se derivó la cuenta.</summary>
    public Guid? GrupoNegocioId { get; set; }
    public Guid? GrupoProductoId { get; set; }
    public Guid? GrupoIvaNegocioId { get; set; }
    public Guid? GrupoIvaProductoId { get; set; }

    public TipoOrigenMovimiento TipoOrigen { get; set; }

    /// <summary>varchar(50).</summary>
    public required string ClaveOrigen { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>varchar(100).</summary>
    public required string CreatedBy { get; set; }

    /// <summary>Referencia lógica al usuario (otra base de datos): sin FK.</summary>
    public Guid? UsuarioId { get; set; }
}
