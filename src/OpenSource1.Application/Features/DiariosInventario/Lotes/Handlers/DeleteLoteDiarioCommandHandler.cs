using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Commands;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Inventario;

namespace OpenSource1.Application.Features.DiariosInventario.Lotes.Handlers;

/// <summary>
/// Borrado lógico de un lote de diario. Rechaza el borrado si tiene líneas VIVAS: el filtro global de EF
/// (<c>!IsDeleted</c>) excluye las borradas lógicamente de <c>FirstOrDefaultAsync</c>, así que un lote cuyas líneas
/// ya se borraron todas sí puede eliminarse.
/// <para>
/// Ronda de corrección final de la Fase 4: abre transacción y toma el <c>FOR UPDATE</c> de la fila del lote
/// (<see cref="ILoteDiarioBloqueoService"/>, el mismo que <c>CreateLineaDiarioCommandHandler</c> y
/// <c>PostearLoteDiarioCommandHandler</c>) ANTES de comprobar si tiene líneas. Sin este lock hay una carrera con el
/// alta de línea: el alta toma <c>FOR UPDATE</c> del lote e inserta su línea dentro de su propia transacción; este
/// borrado, sin tomar el mismo lock, puede leer "sin líneas" ANTES de que el alta confirme, esperar después en su
/// propio <c>UPDATE</c> de borrado lógico (bloqueado por la fila del lote que el alta sí bloqueó) y aplicarse una
/// vez que la línea ya quedó insertada y confirmada, dejando un lote borrado con una línea viva. Con el lock
/// compartido, las dos transacciones se serializan de verdad: quien pierde la carrera por el <c>FOR UPDATE</c> ve
/// el efecto de la otra ya confirmado (el alta ve el lote borrado -> 404 <c>diario_lote.no_encontrado</c>; el
/// borrado ve la línea recién confirmada -> 409 <c>diario.conflicto</c>).
/// </para>
/// </summary>
public sealed class DeleteLoteDiarioCommandHandler(IUnitOfWork unitOfWork, ILoteDiarioBloqueoService loteBloqueo)
    : IRequestHandler<DeleteLoteDiarioCommand, Result>
{
    public async Task<Result> Handle(DeleteLoteDiarioCommand request, CancellationToken cancellationToken)
    {
        // Sin CommitAsync, salir del "await using" deshace la transacción (y libera el lock) en cualquier "return"
        // de fallo de abajo.
        await using var transaccion = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var bloqueado = await loteBloqueo.BloquearYObtenerEstadoAsync(request.Id, cancellationToken);
        if (bloqueado is null)
        {
            return Result.Fallo(new Error(
                "diario_lote.no_encontrado", "No se encontró el lote de diario solicitado.", "Id"));
        }

        var repository = unitOfWork.Repository<LoteDiario>();
        var entity = await repository.GetByIdAsync([request.Id], cancellationToken);
        if (entity is null)
        {
            // Imposible con la fila ya bloqueada arriba (misma condición IsDeleted = false); defensivo.
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
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Exito();
    }
}
