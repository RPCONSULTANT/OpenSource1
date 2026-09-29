using MediatR;
using OpenSource1.Application.Features.GruposContables.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.GruposContables.Commands;

public sealed record CreateGrupoContableCommand(TipoGrupoContable Tipo, string Codigo, string Descripcion)
    : IRequest<Result<GrupoContableResponse>>;

/// <summary>
/// Modificación de un grupo contable simple. <c>Codigo</c> y <c>Descripcion</c> son de reemplazo completo (los únicos
/// campos). <c>Xmin</c> es obligatorio (concurrencia optimista vía <c>xmin</c>): si no coincide con el vigente, 409
/// <c>entidad.modificada_por_otro</c>.
/// </summary>
public sealed record UpdateGrupoContableCommand(TipoGrupoContable Tipo, Guid Id, string Codigo, string Descripcion, long Xmin)
    : IRequest<Result<GrupoContableResponse>>;

public sealed record DeleteGrupoContableCommand(TipoGrupoContable Tipo, Guid Id) : IRequest<Result>;
