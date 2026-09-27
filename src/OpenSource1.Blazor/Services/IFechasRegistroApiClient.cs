using OpenSource1.Application.Features.FechasRegistro.Dtos;

namespace OpenSource1.Blazor.Services;

/// <summary>
/// Cliente de <c>api/configuracion/fechas-registro</c> (Task 8.5): rango general y excepciones por usuario de las fechas de
/// registro permitidas. Solo el Administrador (<c>CanAdministrar</c>); cualquier otro rol recibe 403.
/// </summary>
public interface IFechasRegistroApiClient
{
    Task<FechasRegistroGeneralResponse?> GetGeneralAsync(CancellationToken cancellationToken = default);
    Task<GrupoOperationResult> UpdateGeneralAsync(FechasRegistroRangoInput input, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FechasRegistroUsuarioResponse>> ListUsuariosAsync(CancellationToken cancellationToken = default);
    Task<FechasRegistroUsuarioResponse?> GetUsuarioAsync(Guid id, CancellationToken cancellationToken = default);
    Task<GrupoOperationResult> CreateUsuarioAsync(Guid usuarioId, FechasRegistroRangoInput input, CancellationToken cancellationToken = default);
    Task<GrupoOperationResult> UpdateUsuarioAsync(Guid id, FechasRegistroRangoInput input, CancellationToken cancellationToken = default);
    Task<GrupoOperationResult> DeleteUsuarioAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record FechasRegistroRangoInput(DateOnly? PermitirRegistroDesde, DateOnly? PermitirRegistroHasta);
