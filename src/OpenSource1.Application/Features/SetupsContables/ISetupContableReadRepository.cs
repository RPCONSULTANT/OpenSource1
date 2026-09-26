using OpenSource1.Application.Features.SetupsContables.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.SetupsContables;

/// <summary>
/// Filtro del listado de un setup por sus dos ejes (igualdad exacta; sin filtro = todos). <c>SecundarioId</c> es el grupo de
/// negocio / de IVA de negocio / almacén; <c>PrincipalId</c>, el grupo de producto / de IVA de producto / de inventario.
/// </summary>
public sealed record SetupContableSearchCriteria(Guid? SecundarioId, Guid? PrincipalId);

/// <summary>
/// Lectura Dapper de los tres setups. Orden fijo: código del eje principal, luego el secundario con el comodín primero (así la
/// fila comodín de cada grupo encabeza sus excepciones).
/// </summary>
public interface ISetupContableReadRepository
{
    Task<SetupGeneralResponse?> GetGeneralAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<SetupGeneralResponse>>> ListGeneralAsync(SetupContableSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default);

    Task<SetupIvaResponse?> GetIvaAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<SetupIvaResponse>>> ListIvaAsync(SetupContableSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default);

    Task<SetupInventarioResponse?> GetInventarioAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<SetupInventarioResponse>>> ListInventarioAsync(SetupContableSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default);
}
