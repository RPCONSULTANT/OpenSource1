using OpenSource1.Core.Enums;

namespace OpenSource1.Core.Entities.Inventario;

/// <summary>
/// Fila del libro de valor (append-only, mismo trigger que <see cref="MovimientoProducto"/> pero sin
/// excepción para UPDATE: aquí ni siquiera la cantidad restante cambia). No hereda de
/// <see cref="BaseEntity"/> por la misma razón que <see cref="MovimientoProducto"/>.
/// </summary>
/// <remarks>
/// Deliberadamente NO implementa <c>IAggregateRoot</c> (revisión final de la Fase 3): así
/// <c>IUnitOfWork.Repository&lt;T&gt;()</c>/<c>GenericRepository</c> no compilan con esta entidad y nadie puede volver
/// a escribirla por ahí (Update/Remove del change tracker), que es justo lo que el libro append-only prohíbe. Nada en
/// el código la usa con <c>Repository&lt;MovimientoValor&gt;()</c>; EF la sigue mapeando igual, porque el mapeo de
/// <c>ApplicationDbContext</c> no depende de esa interfaz.
/// </remarks>
public sealed class MovimientoValor
{
    public long Id { get; set; }

    /// <summary>FK opcional a <see cref="MovimientoProducto"/>: nulo en ajustes de redondeo sin cantidad asociada.</summary>
    public long? MovimientoProductoId { get; set; }

    public Guid ProductoId { get; set; }
    public Guid AlmacenId { get; set; }
    public TipoValor TipoValor { get; set; }
    public TipoMovimientoInventario TipoMovimiento { get; set; }
    public DateOnly FechaRegistro { get; set; }

    /// <summary>numeric(18,6); 0 en ajustes/redondeo.</summary>
    public decimal CantidadValorada { get; set; }

    /// <summary>numeric(18,6).</summary>
    public decimal CantidadFacturada { get; set; }

    /// <summary>numeric(18,4) con signo.</summary>
    public decimal ImporteCosto { get; set; }

    /// <summary>numeric(18,4).</summary>
    public decimal CostoPorUnidad { get; set; }

    /// <summary>numeric(18,4).</summary>
    public decimal ImporteVenta { get; set; }

    /// <summary>numeric(18,4); 0 hasta que el posteo a contabilidad lo iguale a <see cref="ImporteCosto"/>.</summary>
    public decimal ImporteCostoPosteadoContabilidad { get; set; }

    public bool Ajuste { get; set; }
    public TipoDocumentoInventario TipoDocumento { get; set; }

    /// <summary>varchar(20).</summary>
    public string? NumeroDocumento { get; set; }

    public int NumeroLineaDocumento { get; set; }

    /// <summary>
    /// Grupos CONGELADOS al registrar (Task 5.5, D8), FK a <c>GruposInventario</c>/<c>GruposNegocio</c>/<c>GruposProducto</c>:
    /// inventario y producto del producto, negocio del socio (null sin socio). Null si el producto no tenía grupo: el batch
    /// de costo (Task 5.6) lo trata como grupo faltante. La migración <c>AddLibroContable</c> rellenó los ya existentes.
    /// </summary>
    public Guid? GrupoInventarioId { get; set; }
    public Guid? GrupoNegocioId { get; set; }
    public Guid? GrupoProductoId { get; set; }

    public TipoOrigenMovimiento TipoOrigen { get; set; }

    /// <summary>varchar(50).</summary>
    public required string ClaveOrigen { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>varchar(100).</summary>
    public required string CreatedBy { get; set; }

    public Guid? UsuarioId { get; set; }
}
