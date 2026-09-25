using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Commands;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Inventario;

namespace OpenSource1.Application.Features.DiariosInventario.Lineas.Handlers;

/// <summary>
/// Borrado lógico de una línea de diario. Rechaza el borrado si el lote está bloqueado (coherente con
/// <see cref="LoteDiario.Bloqueado"/>: un lote bloqueado no admite altas/modificaciones/borrados de línea).
/// </summary>
public sealed class DeleteLineaDiarioCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteLineaDiarioCommand, Result>
{
    public async Task<Result> Handle(DeleteLineaDiarioCommand request, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<LineaDiario>();
        var entity = await repository.GetByIdAsync([request.Id], cancellationToken);

        if (entity is null)
        {
            return Result.Fallo(new Error(
                "diario_linea.no_encontrado", "No se encontró la línea de diario solicitada.", "Id"));
        }

        var lote = await unitOfWork.Repository<LoteDiario>().FirstOrDefaultAsync(
            x => x.Id == entity.LoteDiarioId, cancellationToken: cancellationToken);
        if (lote is null || lote.Bloqueado)
        {
            return Result.Fallo(new Error(
                "diario.lote_bloqueado", "El lote está bloqueado.", "Id"));
        }

        repository.Remove(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Exito();
    }
}
