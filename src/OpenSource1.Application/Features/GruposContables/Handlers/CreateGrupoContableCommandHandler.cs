using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.GruposContables.Commands;
using OpenSource1.Application.Features.GruposContables.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Contabilidad;

namespace OpenSource1.Application.Features.GruposContables.Handlers;

/// <summary>Alta genérica de un grupo contable simple en la tabla de su tipo (ver <see cref="GrupoContableDespacho"/>).</summary>
public sealed class CreateGrupoContableCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<CreateGrupoContableCommand, Result<GrupoContableResponse>>
{
    public async Task<Result<GrupoContableResponse>> Handle(CreateGrupoContableCommand request, CancellationToken cancellationToken)
    {
        if (!TiposGrupoContable.EsValido(request.Tipo))
        {
            return Result<GrupoContableResponse>.Fallo(GrupoContableDespacho.ErrorTipoInvalido());
        }

        var errores = GrupoContableValidator.Validar(request.Codigo, request.Descripcion);
        if (errores.Count > 0)
        {
            return Result<GrupoContableResponse>.Fallo([.. errores]);
        }

        return await GrupoContableDespacho.EjecutarAsync(request.Tipo, new Crear(unitOfWork, request, cancellationToken));
    }

    private sealed class Crear(IUnitOfWork unitOfWork, CreateGrupoContableCommand request, CancellationToken cancellationToken)
        : GrupoContableDespacho.IAccion<Result<GrupoContableResponse>>
    {
        public async Task<Result<GrupoContableResponse>> EjecutarAsync<TGrupo>() where TGrupo : GrupoContable, new()
        {
            var entity = new TGrupo
            {
                Codigo = GrupoContableValidator.NormalizarCodigo(request.Codigo),
                Descripcion = request.Descripcion.Trim()
            };

            var repository = unitOfWork.Repository<TGrupo>();
            await repository.AddAsync(entity, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<GrupoContableResponse>.Exito(
                GrupoContableDespacho.ToResponse(request.Tipo, entity, repository.ObtenerVersionActual(entity)));
        }
    }
}
