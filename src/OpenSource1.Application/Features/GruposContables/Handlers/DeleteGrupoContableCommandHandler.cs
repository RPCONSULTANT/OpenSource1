using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.GruposContables.Commands;
using OpenSource1.Application.Services.Contabilidad;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Contabilidad;

namespace OpenSource1.Application.Features.GruposContables.Handlers;

/// <summary>
/// Borrado lógico genérico de un grupo contable simple. En uso (<see cref="IGrupoContableUsoService"/>: productos y socios
/// vivos; la Task 5.4 añade los setups) → 409 <c>grupo_contable.conflicto</c>. El borrado es lógico, así que la FK Restrict
/// no lo detendría: sin esta guarda un producto quedaría apuntando a un grupo "borrado".
/// </summary>
public sealed class DeleteGrupoContableCommandHandler(IUnitOfWork unitOfWork, IGrupoContableUsoService usoService)
    : IRequestHandler<DeleteGrupoContableCommand, Result>
{
    public async Task<Result> Handle(DeleteGrupoContableCommand request, CancellationToken cancellationToken)
    {
        if (!TiposGrupoContable.EsValido(request.Tipo))
        {
            return Result.Fallo(GrupoContableDespacho.ErrorTipoInvalido());
        }

        return await GrupoContableDespacho.EjecutarAsync(request.Tipo, new Borrar(unitOfWork, usoService, request, cancellationToken));
    }

    private sealed class Borrar(
        IUnitOfWork unitOfWork, IGrupoContableUsoService usoService, DeleteGrupoContableCommand request, CancellationToken cancellationToken)
        : GrupoContableDespacho.IAccion<Result>
    {
        public async Task<Result> EjecutarAsync<TGrupo>() where TGrupo : GrupoContable, new()
        {
            var repository = unitOfWork.Repository<TGrupo>();
            var entity = await repository.FirstOrDefaultAsync(x => x.Id == request.Id, asTracking: true, cancellationToken: cancellationToken);
            if (entity is null)
            {
                return Result.Fallo(GrupoContableDespacho.ErrorNoEncontrado(request.Tipo));
            }

            if (await usoService.EstaEnUsoAsync(request.Tipo, request.Id, cancellationToken))
            {
                return Result.Fallo(new Error(
                    "grupo_contable.conflicto",
                    $"No se puede eliminar el {TiposGrupoContable.De(request.Tipo).Nombre.ToLowerInvariant()} {entity.Codigo} porque está en uso.",
                    "Id"));
            }

            repository.Remove(entity);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Exito();
        }
    }
}
