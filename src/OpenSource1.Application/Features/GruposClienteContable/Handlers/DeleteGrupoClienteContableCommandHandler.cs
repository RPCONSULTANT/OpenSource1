using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.GruposClienteContable.Commands;
using OpenSource1.Application.Services.Contabilidad;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Contabilidad;

namespace OpenSource1.Application.Features.GruposClienteContable.Handlers;

/// <summary>
/// Borrado lógico. En uso por un socio de negocio vivo (<see cref="IGrupoContableUsoService"/>) → 409
/// <c>grupo_cliente_contable.conflicto</c>.
/// </summary>
public sealed class DeleteGrupoClienteContableCommandHandler(IUnitOfWork unitOfWork, IGrupoContableUsoService usoService)
    : IRequestHandler<DeleteGrupoClienteContableCommand, Result>
{
    public async Task<Result> Handle(DeleteGrupoClienteContableCommand request, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<GrupoClienteContable>();
        var entity = await repository.FirstOrDefaultAsync(x => x.Id == request.Id, asTracking: true, cancellationToken: cancellationToken);
        if (entity is null)
        {
            return Result.Fallo(GrupoClienteContableReglas.NoEncontrado());
        }

        if (await usoService.GrupoClienteContableEnUsoAsync(request.Id, cancellationToken))
        {
            return Result.Fallo(new Error(
                "grupo_cliente_contable.conflicto",
                $"No se puede eliminar el grupo contable de cliente {entity.Codigo} porque está en uso.",
                "Id"));
        }

        repository.Remove(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Exito();
    }
}
