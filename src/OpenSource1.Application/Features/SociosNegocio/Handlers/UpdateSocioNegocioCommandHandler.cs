using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.SociosNegocio.Commands;
using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Core.Entities;
using OpenSource1.Core.ValueObjects;

namespace OpenSource1.Application.Features.SociosNegocio.Handlers;

public sealed class UpdateSocioNegocioCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<UpdateSocioNegocioCommand, SocioNegocioResponse?>
{
    public async Task<SocioNegocioResponse?> Handle(UpdateSocioNegocioCommand request, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.Repository<SocioNegocio>();
        var entity = await repo.GetByIdAsync(new object[] { request.Id }, cancellationToken);
        if (entity is null) return null;
        entity.Nombre = request.Nombre.Trim();
        entity.Apellido = request.Apellido.Trim();
        entity.Email = request.Email.Trim();
        entity.Telefono = request.Telefono?.Trim();
        entity.Direccion = string.IsNullOrWhiteSpace(request.DireccionLinea1) ? null : DireccionFiscal.Of(request.DireccionLinea1, request.DireccionLinea2, nameof(request.DireccionLinea1));
        entity.Sector = string.IsNullOrWhiteSpace(request.Sector) ? null : Sector.Of(request.Sector, nameof(request.Sector));
        entity.Pais = string.IsNullOrWhiteSpace(request.PaisCodigo) ? null : Pais.Of(request.PaisCodigo, nameof(request.PaisCodigo));
        entity.ImagePath = request.ImagePath;
        repo.Update(entity); await unitOfWork.SaveChangesAsync(cancellationToken); return CreateSocioNegocioCommandHandler.ToResponse(entity);
    }
}
