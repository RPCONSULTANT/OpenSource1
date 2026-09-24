using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.TerminosPago.Commands;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;

namespace OpenSource1.Application.Features.TerminosPago.Handlers;

public sealed class DeleteTerminoPagoCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteTerminoPagoCommand, Result>
{
    public async Task<Result> Handle(DeleteTerminoPagoCommand request, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<TerminoPago>();
        var entity = await repository.GetByIdAsync([request.Id], cancellationToken);

        if (entity is null)
        {
            return Result.Fallo(new Error(
                "termino_pago.no_encontrado", "No se encontró el término de pago solicitado.", "Id"));
        }

        // El borrado es lógico, así que la FK Restrict de SociosNegocio.TerminoPagoId no lo detiene:
        // sin esta guarda quedarían socios apuntando a un término "borrado". La consulta ya excluye
        // los socios borrados lógicamente (filtro global).
        var enUso = await unitOfWork.Repository<SocioNegocio>()
            .FirstOrDefaultAsync(x => x.TerminoPagoId == request.Id, cancellationToken: cancellationToken);

        if (enUso is not null)
        {
            return Result.Fallo(new Error(
                "termino_pago.en_uso.conflicto",
                "No se puede eliminar el término de pago porque está asignado a uno o más socios de negocio.", "Id"));
        }

        repository.Remove(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Exito();
    }
}
