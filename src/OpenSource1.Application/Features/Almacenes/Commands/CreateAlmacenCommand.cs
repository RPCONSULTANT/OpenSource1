using MediatR;
using OpenSource1.Application.Features.Almacenes.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Almacenes.Commands;

public sealed record CreateAlmacenCommand(
    string Codigo,
    string Nombre,
    string? DireccionLinea1,
    string? DireccionLinea2,
    string? Ciudad,
    string? PaisCodigo,
    bool Bloqueado,
    bool EsPredeterminado) : IRequest<Result<AlmacenResponse>>;
