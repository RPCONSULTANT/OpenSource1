using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.SociosNegocio.Commands;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;

namespace OpenSource1.Application.Features.SociosNegocio.Handlers;

public sealed class DeleteSocioNegocioCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteSocioNegocioCommand, Result>
{
    public async Task<Result> Handle(DeleteSocioNegocioCommand request, CancellationToken cancellationToken)
    {
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

        repo.Remove(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Exito();
    }
}
