using MediatR;
using OpenSource1.Application.Features.Clientes.Dtos;

namespace OpenSource1.Application.Features.Clientes.Commands;

public sealed record UpdateClienteCommand(
    Guid Id,
    string Nombre,
    string Apellido,
    string Email,
    string? Telefono,
    string? DireccionLinea1,
    string? DireccionLinea2,
    string? Sector,
    string? PaisCodigo,
    string? ImagePath = null) : IRequest<ClienteResponse?>;
