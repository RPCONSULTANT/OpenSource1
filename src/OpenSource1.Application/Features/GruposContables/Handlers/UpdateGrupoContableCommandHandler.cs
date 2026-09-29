using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.GruposContables.Commands;
using OpenSource1.Application.Features.GruposContables.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Contabilidad;

namespace OpenSource1.Application.Features.GruposContables.Handlers;

/// <summary>
/// Modificación genérica de un grupo contable simple con concurrencia optimista (<c>Xmin</c> vía
/// <c>IGenericRepository.EstablecerVersionOriginal</c>, mismo patrón que lotes de diario y cuentas contables).
/// </summary>
public sealed class UpdateGrupoContableCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateGrupoContableCommand, Result<GrupoContableResponse>>
{
    public async Task<Result<GrupoContableResponse>> Handle(UpdateGrupoContableCommand request, CancellationToken cancellationToken)
    {
        if (!TiposGrupoContable.EsValido(request.Tipo))
        {
            return Result<GrupoContableResponse>.Fallo(GrupoContableDespacho.ErrorTipoInvalido());
        }

        return await GrupoContableDespacho.EjecutarAsync(request.Tipo, new Modificar(unitOfWork, request, cancellationToken));
    }

    private sealed class Modificar(IUnitOfWork unitOfWork, UpdateGrupoContableCommand request, CancellationToken cancellationToken)
        : GrupoContableDespacho.IAccion<Result<GrupoContableResponse>>
    {
        public async Task<Result<GrupoContableResponse>> EjecutarAsync<TGrupo>() where TGrupo : GrupoContable, new()
        {
            var repository = unitOfWork.Repository<TGrupo>();

            // Consulta con seguimiento (no Find): siempre pasa por el filtro global de soft delete.
            var entity = await repository.FirstOrDefaultAsync(x => x.Id == request.Id, asTracking: true, cancellationToken: cancellationToken);
            if (entity is null)
            {
                return Result<GrupoContableResponse>.Fallo(GrupoContableDespacho.ErrorNoEncontrado(request.Tipo));
            }

            var errores = GrupoContableValidator.Validar(request.Codigo, request.Descripcion);
            if (errores.Count > 0)
            {
                return Result<GrupoContableResponse>.Fallo([.. errores]);
            }

            repository.EstablecerVersionOriginal(entity, request.Xmin);

            entity.Codigo = GrupoContableValidator.NormalizarCodigo(request.Codigo);
            entity.Descripcion = request.Descripcion.Trim();

            repository.Update(entity);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<GrupoContableResponse>.Exito(
                GrupoContableDespacho.ToResponse(request.Tipo, entity, repository.ObtenerVersionActual(entity)));
        }
    }
}
