using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Services.Contabilidad;

/// <summary>
/// Línea de un asiento: <paramref name="Importe"/> con signo (+ débito, − crédito), distinto de cero y con 4 decimales como
/// máximo. Las dimensiones y los grupos se copian (congelados) al <c>MovimientoContable</c>.
/// </summary>
public sealed record LineaAsiento(Guid CuentaContableId, decimal Importe, string? Descripcion,
    Guid? SocioNegocioId = null, Guid? ProductoId = null, Guid? GrupoNegocioId = null, Guid? GrupoProductoId = null,
    Guid? GrupoIvaNegocioId = null, Guid? GrupoIvaProductoId = null);

/// <summary>Asiento a registrar. La suma de <see cref="Lineas"/> debe ser exactamente 0.</summary>
public sealed record AsientoContable(DateOnly FechaRegistro, DateOnly FechaDocumento, TipoDocumentoContable TipoDocumento,
    string? NumeroDocumento, string Descripcion, TipoOrigenMovimiento TipoOrigen, string ClaveOrigen,
    IReadOnlyList<LineaAsiento> Lineas);

/// <summary>Resultado de un registro: id y número (serie <c>CONTAB</c>) del registro y rango de movimientos escritos.</summary>
public sealed record AsientoRegistrado(long RegistroContableId, string NumeroRegistro, long DesdeMovimiento, long HastaMovimiento);
