using OpenSource1.Application.Features.Contabilidad.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Contabilidad;

/// <summary>Consultas paginadas del libro contable (Task 5.5; las vistas completas llegan en la Fase 7).</summary>
public interface IContabilidadReadRepository
{
    Task<Result<PagedResult<MovimientoContableResponse>>> ListMovimientosAsync(
        MovimientoContableSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<RegistroContableResponse>>> ListRegistrosAsync(
        PageRequest paginacion, CancellationToken cancellationToken = default);
}
