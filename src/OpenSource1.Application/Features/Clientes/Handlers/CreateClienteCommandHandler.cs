using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Clientes.Commands;
using OpenSource1.Application.Features.Clientes.Dtos;
using OpenSource1.Core.Entities;

namespace OpenSource1.Application.Features.Clientes.Handlers;

public sealed class CreateClienteCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<CreateClienteCommand, ClienteResponse>
{
    public async Task<ClienteResponse> Handle(CreateClienteCommand request, CancellationToken cancellationToken)
    {
        var entity = new Cliente { Nombre = request.Nombre.Trim(), Apellido = request.Apellido.Trim(), Email = request.Email.Trim(), Telefono = request.Telefono?.Trim(), Direccion = request.Direccion?.Trim(), ImagePath = request.ImagePath };
        await unitOfWork.Repository<Cliente>().AddAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(entity);
    }

    public static ClienteResponse ToResponse(Cliente x) => new() { Id = x.Id, Nombre = x.Nombre, Apellido = x.Apellido, Email = x.Email, Telefono = x.Telefono, Direccion = x.Direccion, ImagePath = x.ImagePath, CreatedAtUtc = x.CreatedAtUtc.UtcDateTime, UpdatedAtUtc = x.UpdatedAtUtc?.UtcDateTime, CreatedBy = x.CreatedBy, UpdatedBy = x.UpdatedBy };
}
