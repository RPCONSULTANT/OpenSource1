using MediatR;
using OpenSource1.Application.Features.FechasRegistro.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.FechasRegistro.Commands;

/// <summary>Modifica el rango general (fila única sembrada). Límites null = sin límite.</summary>
public sealed record UpdateFechasRegistroGeneralCommand(DateOnly? PermitirRegistroDesde, DateOnly? PermitirRegistroHasta)
    : IRequest<Result<FechasRegistroGeneralResponse>>;

/// <summary>Alta de la excepción de un usuario (uno por usuario). El usuario debe existir en Identity.</summary>
public sealed record CreateFechasRegistroUsuarioCommand(Guid UsuarioId, DateOnly? PermitirRegistroDesde, DateOnly? PermitirRegistroHasta)
    : IRequest<Result<FechasRegistroUsuarioResponse>>;

/// <summary>Modifica el rango de una excepción de usuario (el usuario no cambia: para otro usuario, otra fila).</summary>
public sealed record UpdateFechasRegistroUsuarioCommand(Guid Id, DateOnly? PermitirRegistroDesde, DateOnly? PermitirRegistroHasta)
    : IRequest<Result<FechasRegistroUsuarioResponse>>;

/// <summary>Borra la excepción: el usuario vuelve al rango general.</summary>
public sealed record DeleteFechasRegistroUsuarioCommand(Guid Id) : IRequest<Result>;
