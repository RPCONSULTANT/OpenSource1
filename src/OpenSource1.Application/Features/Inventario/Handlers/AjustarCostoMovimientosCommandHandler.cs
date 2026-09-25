using MediatR;
using OpenSource1.Application.Features.Inventario.Commands;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Inventario.Handlers;

/// <summary>Delega en <see cref="IAjusteCostoInventario"/> (que gestiona sus propias transacciones por producto).</summary>
public sealed class AjustarCostoMovimientosCommandHandler(IAjusteCostoInventario ajuste)
    : IRequestHandler<AjustarCostoMovimientosCommand, Result<ResultadoAjusteCosto>>
{
    public Task<Result<ResultadoAjusteCosto>> Handle(AjustarCostoMovimientosCommand request, CancellationToken cancellationToken) =>
        ajuste.AjustarAsync(request.ProductoId, cancellationToken);
}
