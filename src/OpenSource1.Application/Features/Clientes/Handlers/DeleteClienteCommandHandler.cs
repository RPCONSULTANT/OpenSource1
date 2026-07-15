using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Clientes.Commands;
using OpenSource1.Core.Entities;

namespace OpenSource1.Application.Features.Clientes.Handlers;

public sealed class DeleteClienteCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<DeleteClienteCommand, bool>
{
    public async Task<bool> Handle(DeleteClienteCommand request, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.Repository<Cliente>();
        var entity = await repo.GetByIdAsync(new object[] { request.Id }, cancellationToken);
        if (entity is null) return false;
        repo.Remove(entity); await unitOfWork.SaveChangesAsync(cancellationToken); return true;
    }
}
