using MediatR;
using OpenSource1.Application.Features.UnidadesMedida.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.UnidadesMedida.Commands;

public sealed record UpdateUnidadMedidaCommand(Guid Id, string Codigo, string Nombre, short Decimales)
    : IRequest<Result<UnidadMedidaResponse>>;
