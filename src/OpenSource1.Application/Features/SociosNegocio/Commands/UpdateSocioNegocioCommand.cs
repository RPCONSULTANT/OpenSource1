using MediatR;
using OpenSource1.Application.Features.SociosNegocio.Dtos;

namespace OpenSource1.Application.Features.SociosNegocio.Commands;

public sealed record UpdateSocioNegocioCommand(
    Guid Id,
    string Nombre,
    string Apellido,
    string Email,
    string? Telefono,
    string? DireccionLinea1,
    string? DireccionLinea2,
    string? Sector,
    string? PaisCodigo,
    string? ImagePath = null) : IRequest<SocioNegocioResponse?>;
