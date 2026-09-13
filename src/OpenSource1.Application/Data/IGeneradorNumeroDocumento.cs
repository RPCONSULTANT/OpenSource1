using OpenSource1.Core.Common;

namespace OpenSource1.Application.Data;

/// <summary>
/// Genera el siguiente número de documento de una <see cref="Core.Entities.Serie"/>, sin
/// huecos ni duplicados bajo concurrencia. Consumida por las Fases 4 y 6 (diarios de
/// inventario, facturas) — no tiene consumidores todavía en esta fase.
/// </summary>
/// <remarks>
/// <see cref="SiguienteAsync"/> exige una transacción activa en la <c>IDbSession</c> del
/// scope: internamente hace un <c>SELECT ... FOR UPDATE</c> sobre la <c>LineaSerie</c>
/// vigente, y ese bloqueo de fila solo se retiene hasta el commit/rollback de una
/// transacción — fuera de una transacción, Postgres lo libera al terminar el statement, lo
/// que rompe la garantía de "sin huecos" bajo concurrencia. El llamante (un handler de
/// posteo en Fases 4/6) debe invocar esta operación dentro de un
/// <c>IUnitOfWork.BeginTransactionAsync</c>; si no hay transacción activa, el método falla
/// explícitamente con <c>Result.Fallo</c> en vez de relajar el requisito.
/// </remarks>
public interface IGeneradorNumeroDocumento
{
    Task<Result<string>> SiguienteAsync(string codigoSerie, DateOnly fecha, CancellationToken cancellationToken = default);
}
