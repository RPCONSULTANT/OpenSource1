using MediatR;
using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Application.Features.SociosNegocio.Queries;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.SociosNegocio.Handlers;

public sealed class GetSocioNegocioByIdQueryHandler(ISocioNegocioReadRepository readRepository)
    : IRequestHandler<GetSocioNegocioByIdQuery, Result<SocioNegocioResponse>>
{
    public async Task<Result<SocioNegocioResponse>> Handle(GetSocioNegocioByIdQuery request, CancellationToken cancellationToken)
    {
        var item = await readRepository.GetByIdAsync(request.Id, cancellationToken);

        return item is null
            ? Result<SocioNegocioResponse>.Fallo(new Error(
                "socio_negocio.no_encontrado", "No se encontró el socio de negocio solicitado.", "Id"))
            : Result<SocioNegocioResponse>.Exito(item);
    }
}
