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

        // El borrado es lógico, así que las FK Restrict no lo detienen: sin esta guarda quedarían
        // equivalencias (UnidadMedidaProducto) o productos (UnidadMedidaBaseId) apuntando a una unidad
        // "borrada" y la conversión de esas cantidades empezaría a fallar en silencio.
        if (await UnidadMedidaUso.EstaEnUsoAsync(unitOfWork, request.Id, cancellationToken))
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

/// <summary>Uso de una unidad de medida por productos (como unidad base) o por sus equivalencias.</summary>
internal static class UnidadMedidaUso
{
    public static async Task<bool> EstaEnUsoAsync(IUnitOfWork unitOfWork, Guid unidadId, CancellationToken cancellationToken)
    {
        var comoBase = await unitOfWork.Repository<Producto>()
            .FirstOrDefaultAsync(x => x.UnidadMedidaBaseId == unidadId, cancellationToken: cancellationToken);
        if (comoBase is not null)
        {
            return true;
        }

        var equivalencia = await unitOfWork.Repository<UnidadMedidaProducto>()
            .FirstOrDefaultAsync(x => x.UnidadMedidaId == unidadId, cancellationToken: cancellationToken);
        return equivalencia is not null;
    }
}
