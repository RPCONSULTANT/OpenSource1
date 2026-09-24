using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.SociosNegocio.Commands;
using OpenSource1.Core.Entities;

namespace OpenSource1.Application.Features.SociosNegocio.Handlers;

public sealed class DeleteSocioNegocioCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<DeleteSocioNegocioCommand, bool>
{
    public async Task<bool> Handle(DeleteSocioNegocioCommand request, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.Repository<SocioNegocio>();
        var entity = await repo.GetByIdAsync(new object[] { request.Id }, cancellationToken);
        if (entity is null) return false;
        repo.Remove(entity); await unitOfWork.SaveChangesAsync(cancellationToken); return true;
    }
}
