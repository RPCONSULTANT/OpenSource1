using MediatR;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.CuentasContables.Commands;

public sealed record DeleteCuentaContableCommand(Guid Id) : IRequest<Result>;
