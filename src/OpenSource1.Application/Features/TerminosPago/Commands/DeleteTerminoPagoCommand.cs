using MediatR;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.TerminosPago.Commands;

public sealed record DeleteTerminoPagoCommand(Guid Id) : IRequest<Result>;
