namespace OpenSource1.Application.Features.Contabilidad;

/// <summary>
/// Rango de <c>GET api/contabilidad/balance-comprobacion</c> sobre <c>FechaRegistro</c>, ambos extremos incluidos. Sin
/// <see cref="Desde"/>, el rango empieza con el libro (saldo inicial 0); sin <see cref="Hasta"/>, no tiene límite superior.
/// </summary>
public sealed record BalanceComprobacionCriterios(DateOnly? Desde = null, DateOnly? Hasta = null);
