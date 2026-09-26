using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.SociosNegocio.Commands;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;

namespace OpenSource1.Application.Features.SociosNegocio.Handlers;

/// <summary>
/// Borrado lógico de un socio de negocio. Rechaza el borrado con 409 <c>socio_negocio.conflicto</c> si el socio está en uso
/// (<see cref="ISocioNegocioUsoService"/>: borradores de factura vivos, facturas, movimientos de cliente, contables o de
/// inventario — guarda pendiente de la Fase 5, Task 6.4).
/// </summary>
public sealed class DeleteSocioNegocioCommandHandler(IUnitOfWork unitOfWork, ISocioNegocioUsoService usoService)
    : IRequestHandler<DeleteSocioNegocioCommand, Result>
{
    public async Task<Result> Handle(DeleteSocioNegocioCommand request, CancellationToken cancellationToken)
    {
        // En una transacción y con la fila del socio bloqueada (FOR UPDATE) ANTES de evaluar el uso: un posteo de factura
        // concurrente (que lee sus socios FOR SHARE) no puede dejar una factura de un socio borrado.
        await using var transaccion = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await usoService.BloquearAsync(request.Id, cancellationToken);

        var repo = unitOfWork.Repository<SocioNegocio>();
        // Consulta con seguimiento (no Find): defensa en profundidad. Find devolvería una entidad ya
        // rastreada en el mismo scope aunque esté borrada lógicamente; la consulta siempre pasa por
        // el filtro global de soft delete. (Entre peticiones, con un scope por petición, ambas vías
        // dan el mismo resultado.)
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == request.Id, asTracking: true, cancellationToken: cancellationToken);

        if (entity is null)
        {
            return Result.Fallo(new Error(
                "socio_negocio.no_encontrado", "No se encontró el socio de negocio solicitado.", "Id"));
        }

        if (await usoService.EstaEnUsoAsync(request.Id, cancellationToken))
        {
            return Result.Fallo(new Error(
                "socio_negocio.conflicto",
                "No se puede eliminar el socio de negocio porque tiene facturas, borradores de factura o movimientos.",
                "Id"));
        }

        repo.Remove(entity);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Exito();
    }
}
