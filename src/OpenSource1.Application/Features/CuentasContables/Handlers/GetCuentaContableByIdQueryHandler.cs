using MediatR;
using OpenSource1.Application.Features.CuentasContables.Dtos;
using OpenSource1.Application.Features.CuentasContables.Queries;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.CuentasContables.Handlers;

public sealed class GetCuentaContableByIdQueryHandler(ICuentaContableReadRepository readRepository)
    : IRequestHandler<GetCuentaContableByIdQuery, Result<CuentaContableResponse>>
{
    public async Task<Result<CuentaContableResponse>> Handle(GetCuentaContableByIdQuery request, CancellationToken cancellationToken)
    {
        var item = await readRepository.GetByIdAsync(request.Id, cancellationToken);

        return item is null
            ? Result<CuentaContableResponse>.Fallo(new Error(
                "cuenta_contable.no_encontrado", "No se encontró la cuenta contable solicitada.", "Id"))
            : Result<CuentaContableResponse>.Exito(item);
    }
}
