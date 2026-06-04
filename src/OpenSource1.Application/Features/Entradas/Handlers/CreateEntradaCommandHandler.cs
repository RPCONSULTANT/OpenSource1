using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Entradas.Commands;
using OpenSource1.Application.Features.Entradas.Dtos;
using OpenSource1.Core.Entities;

namespace OpenSource1.Application.Features.Entradas.Handlers;

public sealed class CreateEntradaCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<CreateEntradaCommand, EntradaResponse>
{
    public async Task<EntradaResponse> Handle(CreateEntradaCommand request, CancellationToken cancellationToken)
    {
        var entrada = new Entrada
        {
            Titulo      = request.Titulo.Trim(),
            Descripcion = request.Descripcion?.Trim(),
            Tipo        = request.Tipo.Trim(),
            Estado      = request.Estado.Trim()
        };

        var repository = unitOfWork.Repository<Entrada>();
        await repository.AddAsync(entrada, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(entrada);
    }

    private static EntradaResponse ToResponse(Entrada e) =>
        new(e.Id, e.Titulo, e.Descripcion, e.Tipo, e.Estado, e.CreatedAtUtc, e.UpdatedAtUtc);
}
