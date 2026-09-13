using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.TerminosPago.Commands;
using OpenSource1.Application.Features.TerminosPago.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;

namespace OpenSource1.Application.Features.TerminosPago.Handlers;

public sealed class CreateTerminoPagoCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<CreateTerminoPagoCommand, Result<TerminoPagoResponse>>
{
    public async Task<Result<TerminoPagoResponse>> Handle(CreateTerminoPagoCommand request, CancellationToken cancellationToken)
    {
        var errores = TerminoPagoValidator.Validar(
            request.Codigo, request.Descripcion, request.DiasVencimiento, request.DiasDescuento, request.PorcentajeDescuento);

        if (errores.Count > 0)
        {
            return Result<TerminoPagoResponse>.Fallo([.. errores]);
        }

        var entity = new TerminoPago
        {
            Codigo = request.Codigo.Trim(),
            Descripcion = request.Descripcion.Trim(),
            DiasVencimiento = request.DiasVencimiento,
            DiasDescuento = request.DiasDescuento,
            PorcentajeDescuento = request.PorcentajeDescuento
        };

        await unitOfWork.Repository<TerminoPago>().AddAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<TerminoPagoResponse>.Exito(ToResponse(entity));
    }

    public static TerminoPagoResponse ToResponse(TerminoPago x) => new()
    {
        Id = x.Id,
        Codigo = x.Codigo,
        Descripcion = x.Descripcion,
        DiasVencimiento = x.DiasVencimiento,
        DiasDescuento = x.DiasDescuento,
        PorcentajeDescuento = x.PorcentajeDescuento,
        CreatedAtUtc = x.CreatedAtUtc.UtcDateTime,
        UpdatedAtUtc = x.UpdatedAtUtc?.UtcDateTime,
        CreatedBy = x.CreatedBy,
        UpdatedBy = x.UpdatedBy
    };
}
