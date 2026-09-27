namespace OpenSource1.Application.Features.MovimientosCliente;

/// <summary>
/// Filtros de <c>GET api/clientes/{id}/movimientos</c>: rango de <c>FechaRegistro</c> (ambos extremos incluidos),
/// <see cref="SoloAbiertos"/> (<c>true</c> = restante ≠ 0, <c>false</c> = restante = 0, <c>null</c> = todos) y, desde la Task 7.3,
/// <see cref="TipoDocumento"/> (valor de <c>TipoDocumentoCliente</c> como entero; uno no definido → 400) y
/// <see cref="FechaCorte"/>: con ella el restante (y por tanto la marca de abierto y el filtro de abiertos) se calcula A ESA
/// FECHA con la misma regla que el estado de cuenta, y los movimientos registrados después no se devuelven. Sin ella, el
/// restante es el actual (contrato de la Fase 6).
/// </summary>
public sealed record MovimientoClienteSearchCriteria(
    Guid SocioNegocioId,
    DateOnly? Desde = null,
    DateOnly? Hasta = null,
    bool? SoloAbiertos = null,
    int? TipoDocumento = null,
    DateOnly? FechaCorte = null);
