using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Productos.Commands;
using OpenSource1.Core.Entities;

namespace OpenSource1.Application.Features.Productos.Handlers;

public sealed class DeleteProductoCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<DeleteProductoCommand, bool>
{
    public async Task<bool> Handle(DeleteProductoCommand request, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.Repository<Producto>();
        var entity = await repo.GetByIdAsync(new object[] { request.Id }, cancellationToken);
        if (entity is null) return false;
        repo.Remove(entity); await unitOfWork.SaveChangesAsync(cancellationToken); return true;
    }
}
