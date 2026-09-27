using OpenSource1.Application.Features.FechasRegistro.Dtos;

namespace OpenSource1.Application.Features.FechasRegistro;

public interface IFechasRegistroReadRepository
{
    Task<FechasRegistroGeneralResponse?> GetGeneralAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FechasRegistroUsuarioResponse>> ListUsuariosAsync(CancellationToken cancellationToken = default);

    Task<FechasRegistroUsuarioResponse?> GetUsuarioByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
