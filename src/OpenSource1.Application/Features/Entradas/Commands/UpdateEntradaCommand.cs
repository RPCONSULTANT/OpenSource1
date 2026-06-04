using MediatR;
using OpenSource1.Application.Features.Entradas.Dtos;

namespace OpenSource1.Application.Features.Entradas.Commands;

public sealed record UpdateEntradaCommand(
    Guid Id,
    string Titulo,
    string? Descripcion,
    string Tipo,
    string Estado) : IRequest<EntradaResponse?>;
