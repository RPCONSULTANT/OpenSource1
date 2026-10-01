using System.Linq.Expressions;
using OpenSource1.Core.Abstractions;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Data.Repositories;

public interface IGenericRepository<TEntity>
    where TEntity : class, IAggregateRoot
{
    IQueryable<TEntity> Query(bool asTracking = false);
    Task<TEntity?> GetByIdAsync(object[] keyValues, CancellationToken cancellationToken = default);
    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, bool asTracking = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Como <see cref="ListAsync"/> pero CON seguimiento, en una sola consulta: para modificar o borrar varias filas (el <c>xmin</c>
    /// rastreado es el leído; una entidad sin seguimiento adjuntada después perdería esa propiedad sombra).
    /// </summary>
    Task<IReadOnlyList<TEntity>> ListRastreadasAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Variante paginada de <see cref="ListAsync"/>. Implementada con un <c>CountAsync</c> más un
    /// <c>Skip</c>/<c>Take</c> sobre la misma consulta, ambos ejecutados contra la conexión del
    /// scope actual. <see cref="ListAsync"/> sin paginar se conserva para el uso interno de los
    /// handlers de escritura (verificaciones de existencia, etc.).
    /// </summary>
    Task<PagedResult<TEntity>> ListPagedAsync(
        Expression<Func<TEntity, bool>>? predicate,
        PageRequest paginacion,
        CancellationToken cancellationToken = default);

    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    void Update(TEntity entity);
    void Remove(TEntity entity);

    /// <summary>
    /// Concurrencia optimista basada en <c>xmin</c> (Postgres, sin columna adicional; ver spec 1.5): fija el valor que
    /// el cliente vio al leer la fila como valor "original" rastreado por el ORM, de modo que el UPDATE que genera
    /// <see cref="Update"/> lleve <c>WHERE xmin = @original</c>. Si la fila cambió entre la lectura y esta llamada,
    /// <c>SaveChangesAsync</c> lanza <c>DbUpdateConcurrencyException</c> (el manejador global la traduce a 409
    /// <c>entidad.modificada_por_otro</c>). Usado por los PUT de LoteDiario/LineaDiario (Task 4.2); los demás
    /// maestros no lo invocan todavía.
    /// </summary>
    void EstablecerVersionOriginal(TEntity entity, long xmin);

    /// <summary>Valor vigente de <c>xmin</c> de la entidad (tras el alta/modificación), para exponerlo en la respuesta y habilitar el próximo PUT.</summary>
    long ObtenerVersionActual(TEntity entity);
}
