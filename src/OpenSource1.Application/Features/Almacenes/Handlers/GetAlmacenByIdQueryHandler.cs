using MediatR;
using OpenSource1.Application.Features.Almacenes.Dtos;
using OpenSource1.Application.Features.Almacenes.Queries;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Almacenes.Handlers;

public sealed class GetAlmacenByIdQueryHandler(IAlmacenReadRepository readRepository)
    : IRequestHandler<GetAlmacenByIdQuery, Result<AlmacenResponse>>
{
    public async Task<Result<AlmacenResponse>> Handle(GetAlmacenByIdQuery request, CancellationToken cancellationToken)
    {
        var item = await readRepository.GetByIdAsync(request.Id, cancellationToken);

        return item is null
            ? Result<AlmacenResponse>.Fallo(new Error(
                "almacen.no_encontrado", "No se encontró el almacén solicitado.", "Id"))
            : Result<AlmacenResponse>.Exito(item);
    }
}
