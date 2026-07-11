using MediatR;
using OpenSource1.Application.Features.Clientes.Dtos;

namespace OpenSource1.Application.Features.Clientes.Commands;

public sealed record CreateClienteCommand(string Nombre, string Apellido, string Email, string? Telefono, string? Direccion, string? ImagePath = null) : IRequest<ClienteResponse>;
