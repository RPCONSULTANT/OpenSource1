using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.UnidadesMedida.Commands;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;

namespace OpenSource1.Application.Features.UnidadesMedida.Handlers;

public sealed class DeleteUnidadMedidaCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteUnidadMedidaCommand, Result>
{
    public async Task<Result> Handle(DeleteUnidadMedidaCommand request, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<UnidadMedida>();
        var entity = await repository.GetByIdAsync([request.Id], cancellationToken);

        if (entity is null)
        {
            return Result.Fallo(new Error(
                "unidad_medida.no_encontrado", "No se encontró la unidad de medida solicitada.", "Id"));
        }

        // El borrado es lógico, así que la FK Restrict de UnidadMedidaProducto no lo detiene:
        // sin esta guarda quedarían equivalencias apuntando a una unidad "borrada" y la
        // conversión de esas cantidades empezaría a fallar en silencio.
        var enUso = await unitOfWork.Repository<UnidadMedidaProducto>()
            .FirstOrDefaultAsync(x => x.UnidadMedidaId == request.Id, cancellationToken: cancellationToken);

        if (enUso is not null)
        {
            return Result.Fallo(new Error(
                "unidad_medida.en_uso.conflicto",
                "No se puede eliminar la unidad de medida porque está asociada a uno o más productos.", "Id"));
        }

        repository.Remove(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Exito();
    }
}
