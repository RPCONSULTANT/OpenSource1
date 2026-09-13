using MediatR;
using OpenSource1.Application.Features.TerminosPago.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.TerminosPago.Commands;

public sealed record CreateTerminoPagoCommand(
    string Codigo,
    string Descripcion,
    int DiasVencimiento,
    int DiasDescuento,
    decimal PorcentajeDescuento) : IRequest<Result<TerminoPagoResponse>>;
