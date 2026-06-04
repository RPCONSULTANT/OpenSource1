using MediatR;
using OpenSource1.Application.Features.Entradas.Dtos;

namespace OpenSource1.Application.Features.Entradas.Commands;

public sealed record CreateEntradaCommand(
    string Titulo,
    string? Descripcion,
    string Tipo,
    string Estado) : IRequest<EntradaResponse>;
