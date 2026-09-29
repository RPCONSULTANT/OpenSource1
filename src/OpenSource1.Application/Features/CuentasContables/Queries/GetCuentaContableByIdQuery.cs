using MediatR;
using OpenSource1.Application.Features.CuentasContables.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.CuentasContables.Queries;

public sealed record GetCuentaContableByIdQuery(Guid Id) : IRequest<Result<CuentaContableResponse>>;
