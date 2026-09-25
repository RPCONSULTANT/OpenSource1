using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Commands;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Inventario;

namespace OpenSource1.Application.Features.DiariosInventario.Lineas.Handlers;

/// <summary>Borrado lógico de una línea de diario. Sin más guardas: una línea no registrada no tiene dependientes.</summary>
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

        repository.Remove(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Exito();
    }
}
