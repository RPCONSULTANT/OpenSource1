using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Entradas.Commands;
using OpenSource1.Application.Features.Entradas.Dtos;
using OpenSource1.Core.Entities;

namespace OpenSource1.Application.Features.Entradas.Handlers;

public sealed class UpdateEntradaCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateEntradaCommand, EntradaResponse?>
{
    public async Task<EntradaResponse?> Handle(UpdateEntradaCommand request, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<Entrada>();
        var entrada = await repository.FirstOrDefaultAsync(
            e => e.Id == request.Id,
            asTracking: true,
            cancellationToken);

        if (entrada is null) return null;

        entrada.Titulo      = request.Titulo.Trim();
        entrada.Descripcion = request.Descripcion?.Trim();
        entrada.Tipo        = request.Tipo.Trim();
        entrada.Estado      = request.Estado.Trim();

        repository.Update(entrada);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new(entrada.Id, entrada.Titulo, entrada.Descripcion, entrada.Tipo, entrada.Estado,
                   entrada.CreatedAtUtc, entrada.UpdatedAtUtc);
    }
}
