using MediatR;

namespace OpenSource1.Application.Features.Entradas.Commands;

public sealed record DeleteEntradaCommand(Guid Id) : IRequest<bool>;
