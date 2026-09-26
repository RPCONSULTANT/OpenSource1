namespace OpenSource1.Application.Features.MovimientosCliente;

/// <summary>
/// Filtros de <c>GET api/clientes/{id}/movimientos</c>: rango de <c>FechaRegistro</c> (ambos extremos incluidos) y
/// <see cref="SoloAbiertos"/> (<c>true</c> = restante ≠ 0, <c>false</c> = restante = 0, <c>null</c> = todos).
/// </summary>
public sealed record MovimientoClienteSearchCriteria(
    Guid SocioNegocioId,
    DateOnly? Desde = null,
    DateOnly? Hasta = null,
    bool? SoloAbiertos = null);
