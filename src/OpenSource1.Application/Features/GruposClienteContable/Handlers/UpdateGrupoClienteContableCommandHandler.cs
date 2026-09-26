using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.GruposClienteContable.Commands;
using OpenSource1.Application.Features.GruposClienteContable.Dtos;
using OpenSource1.Application.Features.GruposContables;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Contabilidad;

namespace OpenSource1.Application.Features.GruposClienteContable.Handlers;

/// <summary>Modificación con semántica mixta documentada en <see cref="UpdateGrupoClienteContableCommand"/>.</summary>
public sealed class UpdateGrupoClienteContableCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateGrupoClienteContableCommand, Result<GrupoClienteContableResponse>>
{
    public async Task<Result<GrupoClienteContableResponse>> Handle(UpdateGrupoClienteContableCommand request, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<GrupoClienteContable>();
        var entity = await repository.FirstOrDefaultAsync(x => x.Id == request.Id, asTracking: true, cancellationToken: cancellationToken);
        if (entity is null)
        {
            return Result<GrupoClienteContableResponse>.Fallo(GrupoClienteContableReglas.NoEncontrado());
        }

        var errores = GrupoContableValidator.Validar(request.Codigo, request.Descripcion, GrupoClienteContableReglas.Prefijo);
        if (errores.Count > 0)
        {
            return Result<GrupoClienteContableResponse>.Fallo([.. errores]);
        }

        var cxcId = request.CuentaCxCId;
        // null = conservar; Guid.Empty = quitar la cuenta opcional.
        var descuentoId = request.CuentaDescuentoId is null ? entity.CuentaDescuentoId
            : request.CuentaDescuentoId == Guid.Empty ? null : request.CuentaDescuentoId;
        var interesId = request.CuentaInteresId is null ? entity.CuentaInteresId
            : request.CuentaInteresId == Guid.Empty ? null : request.CuentaInteresId;

        // Solo se revalida la cuenta que CAMBIA: un grupo cuya cuenta se bloqueó después debe seguir siendo editable (la
        // cuenta bloqueada la rechazará el derivador al postear, Task 5.4).
        var validaciones = new (Guid? Nueva, Guid? Actual, string Campo, string Etiqueta)[]
        {
            (cxcId, entity.CuentaCxCId, "CuentaCxCId", "por cobrar"),
            (descuentoId, entity.CuentaDescuentoId, "CuentaDescuentoId", "de descuentos"),
            (interesId, entity.CuentaInteresId, "CuentaInteresId", "de intereses"),
        };
        foreach (var (nueva, actual, campo, etiqueta) in validaciones)
        {
            if (nueva is { } id && nueva != actual)
            {
                var validacion = await GrupoClienteContableReglas.ValidarCuentaAsync(unitOfWork, id, campo, etiqueta, cancellationToken);
                if (validacion.EsFallo)
                {
                    return Result<GrupoClienteContableResponse>.Fallo(validacion);
                }
            }
        }

        repository.EstablecerVersionOriginal(entity, request.Xmin);

        entity.Codigo = GrupoContableValidator.NormalizarCodigo(request.Codigo);
        entity.Descripcion = request.Descripcion.Trim();
        entity.CuentaCxCId = cxcId;
        entity.CuentaDescuentoId = descuentoId;
        entity.CuentaInteresId = interesId;

        repository.Update(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<GrupoClienteContableResponse>.Exito(GrupoClienteContableReglas.ToResponse(
            entity,
            await GrupoClienteContableReglas.BuscarCuentaAsync(unitOfWork, entity.CuentaCxCId, cancellationToken),
            await GrupoClienteContableReglas.BuscarCuentaAsync(unitOfWork, entity.CuentaDescuentoId, cancellationToken),
            await GrupoClienteContableReglas.BuscarCuentaAsync(unitOfWork, entity.CuentaInteresId, cancellationToken),
            repository.ObtenerVersionActual(entity)));
    }
}
