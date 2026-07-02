using MediatR;
using OpenSource1.Application.Features.Clientes.Dtos;

namespace OpenSource1.Application.Features.Clientes.Commands;

public sealed record UpdateClienteCommand(Guid Id, string NombreCompleto, string DocumentoIdentidad, string Email, string? Telefono, string? Direccion, bool Activo) : IRequest<ClienteResponse?>;
