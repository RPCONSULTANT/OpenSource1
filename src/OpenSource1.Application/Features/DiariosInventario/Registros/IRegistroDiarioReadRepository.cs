using OpenSource1.Application.Features.DiariosInventario.Registros.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.DiariosInventario.Registros;

public interface IRegistroDiarioReadRepository
{
    Task<Result<PagedResult<RegistroDiarioResponse>>> ListAsync(
        Guid? loteDiarioId, PageRequest paginacion, CancellationToken cancellationToken = default);
}
