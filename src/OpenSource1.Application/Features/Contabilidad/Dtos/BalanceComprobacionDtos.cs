using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.Contabilidad.Dtos;

/// <summary>
/// Fila del balance de comprobación (Task 7.4). Una cuenta de Posteo con movimientos en el rango o saldo inicial distinto de
/// cero lleva sus importes; una cuenta de Encabezado es una fila de título (<see cref="EsEncabezado"/>) sin importes (null),
/// intercalada por número con su <see cref="Sangria"/>. Número y nombre son los ACTUALES de la cuenta (aunque esté borrada
/// lógicamente: <see cref="Borrada"/>).
/// </summary>
public sealed class BalanceComprobacionFilaResponse
{
    public Guid CuentaContableId { get; init; }
    public string Numero { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public TipoCuentaContable TipoCuenta { get; init; }
    public TipoResultadoCuenta TipoResultado { get; init; }
    public int Sangria { get; init; }
    public bool EsEncabezado { get; init; }
    public bool Borrada { get; init; }

    /// <summary>Σ Importe con <c>FechaRegistro &lt; desde</c> (0 sin <c>desde</c>). Sin cierre de ejercicio.</summary>
    public decimal? SaldoInicial { get; init; }

    /// <summary>Σ Debito del rango.</summary>
    public decimal? Debitos { get; init; }

    /// <summary>Σ Credito del rango.</summary>
    public decimal? Creditos { get; init; }

    /// <summary>Σ Importe con <c>FechaRegistro &lt;= hasta</c> (= saldo inicial + débitos − créditos).</summary>
    public decimal? SaldoFinal { get; init; }

    /// <summary>Movimientos de la cuenta en el rango.</summary>
    public int? Movimientos { get; init; }
}

/// <summary>Totales de las filas de Posteo: Σ saldos iniciales = Σ saldos finales = 0 y Σ débitos = Σ créditos.</summary>
public sealed record BalanceComprobacionTotales(decimal SaldoInicial, decimal Debitos, decimal Creditos, decimal SaldoFinal);

/// <summary>Balance de comprobación del rango (sin paginar: una fila por cuenta del plan, ordenadas por número).</summary>
public sealed record BalanceComprobacionResponse(
    DateOnly? Desde, DateOnly? Hasta, IReadOnlyList<BalanceComprobacionFilaResponse> Filas, BalanceComprobacionTotales Totales);
