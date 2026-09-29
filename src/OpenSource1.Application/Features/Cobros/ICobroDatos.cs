using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.Cobros;

/// <summary>Socio vivo bloqueado <c>FOR SHARE</c> por un cobro, con lo que el cobro necesita de él.</summary>
public sealed record SocioCobro(Guid Id, BloqueoSocioNegocio Bloqueado, Guid? GrupoClienteContableId);

/// <summary>
/// Acceso a datos de los cobros (Task 6.5) con Dapper sobre la transacción de <c>IDbSession</c> (la misma que enrola EF y que usan
/// <c>IRegistroMovimientosCliente</c> e <c>IRegistroContable</c>). Requiere una transacción activa (lanza
/// <see cref="InvalidOperationException"/> si no la hay).
/// </summary>
public interface ICobroDatos
{
    /// <summary>
    /// Bloqueo compartido (<c>FOR SHARE</c>, orden de Id, sin repetir) de los socios VIVOS (no borrados) de <paramref name="ids"/>
    /// hasta el commit, y sus datos leídos con el bloqueo ya tomado. Mismo papel que en el posteo de facturas: el borrado del
    /// socio (<c>FOR UPDATE</c> + guarda de uso) espera al cobro y después ve su movimiento (409), o el cobro espera al borrado y
    /// no encuentra el socio (400). Los socios inexistentes o borrados no aparecen en el resultado.
    /// </summary>
    Task<IReadOnlyList<SocioCobro>> BloquearSociosAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
}
