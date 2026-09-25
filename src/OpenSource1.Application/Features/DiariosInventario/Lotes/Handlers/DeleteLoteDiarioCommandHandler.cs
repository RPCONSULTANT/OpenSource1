using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Commands;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Inventario;

namespace OpenSource1.Application.Features.DiariosInventario.Lotes.Handlers;

/// <summary>Borrado lógico de un lote de diario. Rechaza el borrado si tiene líneas (borradas o no importa: basta con que existan filas).</summary>
public sealed class DeleteLoteDiarioCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteLoteDiarioCommand, Result>
{
    public async Task<Result> Handle(DeleteLoteDiarioCommand request, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<LoteDiario>();
        var entity = await repository.GetByIdAsync([request.Id], cancellationToken);

        if (entity is null)
        {
            return Result.Fallo(new Error(
                "diario_lote.no_encontrado", "No se encontró el lote de diario solicitado.", "Id"));
        }

        var tieneLineas = await unitOfWork.Repository<LineaDiario>().FirstOrDefaultAsync(
            x => x.LoteDiarioId == request.Id, cancellationToken: cancellationToken);
        if (tieneLineas is not null)
        {
            return Result.Fallo(new Error(
                "diario.conflicto", "No se puede eliminar el lote porque tiene líneas capturadas.", "Id"));
        }

        repository.Remove(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Exito();
    }
}
