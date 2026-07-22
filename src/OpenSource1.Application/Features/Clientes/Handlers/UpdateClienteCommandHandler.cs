using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Clientes.Commands;
using OpenSource1.Application.Features.Clientes.Dtos;
using OpenSource1.Core.Entities;
using OpenSource1.Core.ValueObjects;

namespace OpenSource1.Application.Features.Clientes.Handlers;

public sealed class UpdateClienteCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<UpdateClienteCommand, ClienteResponse?>
{
    public async Task<ClienteResponse?> Handle(UpdateClienteCommand request, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.Repository<Cliente>();
        var entity = await repo.GetByIdAsync(new object[] { request.Id }, cancellationToken);
        if (entity is null) return null;
        entity.Nombre = request.Nombre.Trim();
        entity.Apellido = request.Apellido.Trim();
        entity.Email = request.Email.Trim();
        entity.Telefono = request.Telefono?.Trim();
        entity.Direccion = string.IsNullOrWhiteSpace(request.DireccionLinea1) ? null : new DireccionCliente(request.DireccionLinea1, request.DireccionLinea2);
        entity.Sector = string.IsNullOrWhiteSpace(request.Sector) ? null : new Sector(request.Sector);
        entity.Pais = string.IsNullOrWhiteSpace(request.PaisCodigo) ? null : Pais.Of(request.PaisCodigo);
        entity.ImagePath = request.ImagePath;
        repo.Update(entity); await unitOfWork.SaveChangesAsync(cancellationToken); return CreateClienteCommandHandler.ToResponse(entity);
    }
}
