using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.SociosNegocio.Commands;
using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Core.Entities;
using OpenSource1.Core.ValueObjects;

namespace OpenSource1.Application.Features.SociosNegocio.Handlers;

public sealed class CreateSocioNegocioCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<CreateSocioNegocioCommand, SocioNegocioResponse>
{
    public async Task<SocioNegocioResponse> Handle(CreateSocioNegocioCommand request, CancellationToken cancellationToken)
    {
        var entity = new SocioDeNegocio
        {
            Nombre = request.Nombre.Trim(),
            Apellido = request.Apellido.Trim(),
            Email = request.Email.Trim(),
            Telefono = request.Telefono?.Trim(),
            Direccion = string.IsNullOrWhiteSpace(request.DireccionLinea1) ? null : DireccionFiscal.Of(request.DireccionLinea1, request.DireccionLinea2, nameof(request.DireccionLinea1)),
            Sector = string.IsNullOrWhiteSpace(request.Sector) ? null : Sector.Of(request.Sector, nameof(request.Sector)),
            Pais = string.IsNullOrWhiteSpace(request.PaisCodigo) ? null : Pais.Of(request.PaisCodigo, nameof(request.PaisCodigo)),
            ImagePath = request.ImagePath
        };
        await unitOfWork.Repository<SocioDeNegocio>().AddAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(entity);
    }

    public static SocioNegocioResponse ToResponse(SocioDeNegocio x) => new()
    {
        Id = x.Id,
        Nombre = x.Nombre,
        Apellido = x.Apellido,
        Email = x.Email,
        Telefono = x.Telefono,
        DireccionLinea1 = x.Direccion?.Linea1,
        DireccionLinea2 = x.Direccion?.Linea2,
        Sector = x.Sector?.Nombre,
        PaisCodigo = x.Pais?.Codigo,
        PaisNombre = x.Pais?.Nombre,
        ImagePath = x.ImagePath,
        CreatedAtUtc = x.CreatedAtUtc.UtcDateTime,
        UpdatedAtUtc = x.UpdatedAtUtc?.UtcDateTime,
        CreatedBy = x.CreatedBy,
        UpdatedBy = x.UpdatedBy
    };
}
