using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.SociosNegocio.Commands;
using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;

namespace OpenSource1.Application.Features.SociosNegocio.Handlers;

/// <summary>
/// Modificación de un socio de negocio. <c>Codigo</c> es inmutable: el comando no lo lleva y
/// <see cref="SocioNegocioReglas.Aplicar"/> nunca lo toca.
/// </summary>
public sealed class UpdateSocioNegocioCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateSocioNegocioCommand, Result<SocioNegocioResponse>>
{
    public async Task<Result<SocioNegocioResponse>> Handle(UpdateSocioNegocioCommand request, CancellationToken cancellationToken)
    {
        var errores = SocioNegocioValidator.Validar(request);
        if (errores.Count > 0)
        {
            return Result<SocioNegocioResponse>.Fallo([.. errores]);
        }

        var repo = unitOfWork.Repository<SocioNegocio>();
        // Consulta con seguimiento (no Find): respeta el filtro de soft delete, así un socio ya
        // borrado responde no_encontrado en vez de modificarse/borrarse de nuevo.
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == request.Id, asTracking: true, cancellationToken: cancellationToken);
        if (entity is null)
        {
            return Result<SocioNegocioResponse>.Fallo(new Error(
                "socio_negocio.no_encontrado", "No se encontró el socio de negocio solicitado.", "Id"));
        }

        var verificacion = await SocioNegocioReglas.VerificarReferenciasAsync(unitOfWork, request, request.Id, cancellationToken);
        if (verificacion is not null)
        {
            return Result<SocioNegocioResponse>.Fallo(verificacion.Value);
        }

        SocioNegocioReglas.Aplicar(entity, request);

        repo.Update(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<SocioNegocioResponse>.Exito(CreateSocioNegocioCommandHandler.ToResponse(entity));
    }
}
