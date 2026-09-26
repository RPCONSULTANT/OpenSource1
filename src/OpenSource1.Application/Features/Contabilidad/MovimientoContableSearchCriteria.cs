namespace OpenSource1.Application.Features.Contabilidad;

/// <summary>Filtros de <c>GET api/contabilidad/movimientos</c>: cuenta y rango de <c>FechaRegistro</c> (ambos extremos incluidos).</summary>
public sealed record MovimientoContableSearchCriteria(Guid? CuentaContableId, DateOnly? Desde, DateOnly? Hasta);
