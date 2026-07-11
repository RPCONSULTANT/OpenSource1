using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Clientes.Commands;
using OpenSource1.Application.Features.Clientes.Dtos;
using OpenSource1.Core.Entities;

namespace OpenSource1.Application.Features.Clientes.Handlers;

public sealed class UpdateClienteCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<UpdateClienteCommand, ClienteResponse?>
{
    public async Task<ClienteResponse?> Handle(UpdateClienteCommand request, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.Repository<Cliente>();
        var entity = await repo.GetByIdAsync(new object[] { request.Id }, cancellationToken);
        if (entity is null) return null;
        entity.Nombre = request.Nombre.Trim(); entity.Apellido = request.Apellido.Trim(); entity.Email = request.Email.Trim(); entity.Telefono = request.Telefono?.Trim(); entity.Direccion = request.Direccion?.Trim(); entity.ImagePath = request.ImagePath;
        repo.Update(entity); await unitOfWork.SaveChangesAsync(cancellationToken); return CreateClienteCommandHandler.ToResponse(entity);
    }
}
