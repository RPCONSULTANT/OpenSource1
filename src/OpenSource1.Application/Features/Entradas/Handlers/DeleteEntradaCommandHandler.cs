using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Entradas.Commands;
using OpenSource1.Core.Entities;

namespace OpenSource1.Application.Features.Entradas.Handlers;

public sealed class DeleteEntradaCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteEntradaCommand, bool>
{
    public async Task<bool> Handle(DeleteEntradaCommand request, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<Entrada>();
        var entrada = await repository.FirstOrDefaultAsync(
            e => e.Id == request.Id,
            asTracking: true,
            cancellationToken);

        if (entrada is null) return false;

        repository.Remove(entrada);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
