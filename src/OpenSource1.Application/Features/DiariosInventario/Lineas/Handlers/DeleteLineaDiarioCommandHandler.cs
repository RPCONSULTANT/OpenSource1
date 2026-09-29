using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Commands;
using OpenSource1.Application.Features.DiariosInventario.Lotes;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Inventario;

namespace OpenSource1.Application.Features.DiariosInventario.Lineas.Handlers;

/// <summary>
/// Borrado lógico de una línea de diario. Rechaza el borrado si el lote está bloqueado (coherente con
/// <see cref="LoteDiario.Bloqueado"/>: un lote bloqueado no admite altas/modificaciones/borrados de línea).
/// <para>
/// Ronda de corrección final de la Fase 4 (punto 3): abre transacción y toma el <c>FOR UPDATE</c> del lote
/// (<see cref="ILoteDiarioBloqueoService"/>) ANTES de borrar, con el mismo motivo que
/// <see cref="Handlers.UpdateLineaDiarioCommandHandler"/>: un bloqueo concurrente del lote no puede colarse entre la
/// comprobación y el borrado, y el orden global de locks se mantiene lote -&gt; línea.
/// </para>
/// </summary>
public sealed class DeleteLineaDiarioCommandHandler(IUnitOfWork unitOfWork, ILoteDiarioBloqueoService loteBloqueo)
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

        await using var transaccion = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var loteBloqueado = await loteBloqueo.BloquearYObtenerEstadoAsync(entity.LoteDiarioId, cancellationToken);
        if (loteBloqueado is null or true)
        {
            // null es imposible en operación normal (LoteDiarioId es FK Restrict de una línea ya guardada), pero se
            // trata igual que bloqueado: en ambos casos la línea no se puede borrar.
            return Result.Fallo(new Error(
                "diario.lote_bloqueado", "El lote está bloqueado.", "Id"));
        }

        repository.Remove(entity);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Exito();
    }
}
