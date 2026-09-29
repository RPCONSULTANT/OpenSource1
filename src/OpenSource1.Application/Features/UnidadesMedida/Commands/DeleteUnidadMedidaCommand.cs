using MediatR;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.UnidadesMedida.Commands;

public sealed record DeleteUnidadMedidaCommand(Guid Id) : IRequest<Result>;
