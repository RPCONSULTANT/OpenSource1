using System.Text.Json.Serialization;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.Inventario.Consultas.Dtos;

/// <summary>
/// Fila de la vista de movimientos de producto (Task 7.2). <see cref="Cantidad"/> y <see cref="CantidadRestante"/> van en unidad
/// BASE del producto (<see cref="UnidadBaseCodigo"/>); <see cref="UnidadMedidaCodigo"/> y <see cref="CantidadPorUnidadMedida"/>
/// son los del documento de origen (factor congelado).
/// </summary>
public sealed class MovimientoProductoVistaResponse
{
    public long Id { get; init; }
    public DateOnly FechaRegistro { get; init; }
    public DateOnly FechaDocumento { get; init; }
    public TipoMovimientoInventario TipoMovimiento { get; init; }
    public TipoDocumentoInventario TipoDocumento { get; init; }
    public string? NumeroDocumento { get; init; }
    public int NumeroLineaDocumento { get; init; }
    public Guid ProductoId { get; init; }
    public string ProductoCodigo { get; init; } = string.Empty;
    public string ProductoNombre { get; init; } = string.Empty;
    public Guid AlmacenId { get; init; }
    public string AlmacenCodigo { get; init; } = string.Empty;
    public string AlmacenNombre { get; init; } = string.Empty;
    public decimal Cantidad { get; init; }

    /// <summary>Solo entradas (null en salidas).</summary>
    public decimal? CantidadRestante { get; init; }

    public string UnidadBaseCodigo { get; init; } = string.Empty;
    public Guid UnidadMedidaId { get; init; }
    public string UnidadMedidaCodigo { get; init; } = string.Empty;
    public decimal CantidadPorUnidadMedida { get; init; }
    public TipoOrigenMovimiento TipoOrigen { get; init; }

    /// <summary>
    /// Existencia del producto (en el almacén filtrado, o en todos) justo después de este movimiento, en orden
    /// (<c>FechaRegistro</c>, <c>Id</c>): incluye todo lo anterior al rango y a la página. Solo con el filtro de producto; sin él
    /// la propiedad no se serializa.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? SaldoAcumulado { get; init; }
}

/// <summary>Fila de la vista de movimientos de valor (Task 7.2).</summary>
public sealed class MovimientoValorVistaResponse
{
    public long Id { get; init; }
    public long? MovimientoProductoId { get; init; }
    public DateOnly FechaRegistro { get; init; }
    public TipoMovimientoInventario TipoMovimiento { get; init; }
    public TipoDocumentoInventario TipoDocumento { get; init; }
    public string? NumeroDocumento { get; init; }
    public Guid ProductoId { get; init; }
    public string ProductoCodigo { get; init; } = string.Empty;
    public string ProductoNombre { get; init; } = string.Empty;
    public Guid AlmacenId { get; init; }
    public string AlmacenCodigo { get; init; } = string.Empty;
    public string AlmacenNombre { get; init; } = string.Empty;
    public decimal CantidadValorada { get; init; }
    public decimal ImporteCosto { get; init; }
    public decimal CostoPorUnidad { get; init; }
    public decimal ImporteVenta { get; init; }
    public decimal ImporteCostoPosteadoContabilidad { get; init; }

    /// <summary><c>ImporteCostoPosteadoContabilidad = ImporteCosto</c>: el batch de costo ya lo llevó al libro contable.</summary>
    public bool Contabilizado { get; init; }

    public bool Ajuste { get; init; }
    public TipoValor TipoValor { get; init; }
    public TipoOrigenMovimiento TipoOrigen { get; init; }
}

/// <summary>
/// Existencia y valor de un producto en un almacén a una fecha (Task 7.2): <c>Existencia = Σ MovimientosProducto.Cantidad</c>
/// (misma derivación que <c>IConsultaInventario</c>) y <c>Valor = Σ MovimientosValor.ImporteCosto</c>, ambos con
/// <c>FechaRegistro &lt;= fecha</c>. <see cref="CostoMedio"/> = valor / existencia solo si la existencia es positiva.
/// </summary>
public sealed class ExistenciaVistaResponse
{
    public Guid ProductoId { get; init; }
    public string ProductoCodigo { get; init; } = string.Empty;
    public string ProductoNombre { get; init; } = string.Empty;
    public string UnidadBaseCodigo { get; init; } = string.Empty;
    public Guid AlmacenId { get; init; }
    public string AlmacenCodigo { get; init; } = string.Empty;
    public string AlmacenNombre { get; init; } = string.Empty;
    public decimal Existencia { get; init; }
    public decimal Valor { get; init; }
    public decimal? CostoMedio { get; init; }
}

/// <summary>Página de existencias con la fecha de corte aplicada y el valor total de TODAS las filas filtradas (no solo de la página).</summary>
public sealed record ExistenciasVistaResponse(PagedResult<ExistenciaVistaResponse> Pagina, DateOnly Fecha, decimal ValorTotal);
