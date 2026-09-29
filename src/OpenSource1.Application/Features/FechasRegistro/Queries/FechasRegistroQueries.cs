using MediatR;
using OpenSource1.Application.Features.FechasRegistro.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.FechasRegistro.Queries;

public sealed record GetFechasRegistroGeneralQuery : IRequest<Result<FechasRegistroGeneralResponse>>;

/// <summary>Todas las excepciones de usuario vivas, ordenadas por nombre de usuario (tabla acotada por el número de usuarios).</summary>
public sealed record ListFechasRegistroUsuariosQuery : IRequest<Result<IReadOnlyList<FechasRegistroUsuarioResponse>>>;

public sealed record GetFechasRegistroUsuarioByIdQuery(Guid Id) : IRequest<Result<FechasRegistroUsuarioResponse>>;
