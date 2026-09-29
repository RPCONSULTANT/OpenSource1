using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.TerminosPago.Commands;
using OpenSource1.Application.Features.TerminosPago.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;

namespace OpenSource1.Application.Features.TerminosPago.Handlers;

public sealed class UpdateTerminoPagoCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateTerminoPagoCommand, Result<TerminoPagoResponse>>
{
    public async Task<Result<TerminoPagoResponse>> Handle(UpdateTerminoPagoCommand request, CancellationToken cancellationToken)
    {
        var errores = TerminoPagoValidator.Validar(
            request.Codigo, request.Descripcion, request.DiasVencimiento, request.DiasDescuento, request.PorcentajeDescuento);

        if (errores.Count > 0)
        {
            return Result<TerminoPagoResponse>.Fallo([.. errores]);
        }

        var repository = unitOfWork.Repository<TerminoPago>();
        var entity = await repository.GetByIdAsync([request.Id], cancellationToken);

        if (entity is null)
        {
            return Result<TerminoPagoResponse>.Fallo(new Error(
                "termino_pago.no_encontrado", "No se encontró el término de pago solicitado.", "Id"));
        }

        entity.Codigo = request.Codigo.Trim();
        entity.Descripcion = request.Descripcion.Trim();
        entity.DiasVencimiento = request.DiasVencimiento;
        entity.DiasDescuento = request.DiasDescuento;
        entity.PorcentajeDescuento = request.PorcentajeDescuento;

        repository.Update(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<TerminoPagoResponse>.Exito(CreateTerminoPagoCommandHandler.ToResponse(entity));
    }
}
