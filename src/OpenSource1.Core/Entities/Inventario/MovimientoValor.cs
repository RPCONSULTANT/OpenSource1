using OpenSource1.Core.Abstractions;
using OpenSource1.Core.Enums;

namespace OpenSource1.Core.Entities.Inventario;

/// <summary>
/// Fila del libro de valor (append-only, mismo trigger que <see cref="MovimientoProducto"/> pero sin
/// excepción para UPDATE: aquí ni siquiera la cantidad restante cambia). No hereda de
/// <see cref="BaseEntity"/> por la misma razón que <see cref="MovimientoProducto"/>.
/// </summary>
public sealed class MovimientoValor : IAggregateRoot
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

    /// <summary>Sin FK hasta la Fase 5 (grupos de inventario aún no existen como catálogo).</summary>
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
