using MediatR;
using OpenSource1.Application.Features.FechasRegistro.Dtos;
using OpenSource1.Application.Features.FechasRegistro.Queries;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.FechasRegistro.Handlers;

public sealed class GetFechasRegistroGeneralQueryHandler(IFechasRegistroReadRepository readRepository)
    : IRequestHandler<GetFechasRegistroGeneralQuery, Result<FechasRegistroGeneralResponse>>
{
    public async Task<Result<FechasRegistroGeneralResponse>> Handle(GetFechasRegistroGeneralQuery request, CancellationToken cancellationToken) =>
        Result<FechasRegistroGeneralResponse>.Exito(
            await readRepository.GetGeneralAsync(cancellationToken)
            ?? throw new InvalidOperationException("Falta la fila sembrada de la configuración general de fechas de registro."));
}

public sealed class ListFechasRegistroUsuariosQueryHandler(IFechasRegistroReadRepository readRepository)
    : IRequestHandler<ListFechasRegistroUsuariosQuery, Result<IReadOnlyList<FechasRegistroUsuarioResponse>>>
{
    public async Task<Result<IReadOnlyList<FechasRegistroUsuarioResponse>>> Handle(
        ListFechasRegistroUsuariosQuery request, CancellationToken cancellationToken) =>
        Result<IReadOnlyList<FechasRegistroUsuarioResponse>>.Exito(await readRepository.ListUsuariosAsync(cancellationToken));
}

public sealed class GetFechasRegistroUsuarioByIdQueryHandler(IFechasRegistroReadRepository readRepository)
    : IRequestHandler<GetFechasRegistroUsuarioByIdQuery, Result<FechasRegistroUsuarioResponse>>
{
    public async Task<Result<FechasRegistroUsuarioResponse>> Handle(
        GetFechasRegistroUsuarioByIdQuery request, CancellationToken cancellationToken) =>
        await readRepository.GetUsuarioByIdAsync(request.Id, cancellationToken) is { } item
            ? Result<FechasRegistroUsuarioResponse>.Exito(item)
            : Result<FechasRegistroUsuarioResponse>.Fallo(FechasRegistroErrores.UsuarioNoEncontrado());
}
