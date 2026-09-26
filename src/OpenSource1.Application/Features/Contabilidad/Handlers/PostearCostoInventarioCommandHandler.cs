using MediatR;
using OpenSource1.Application.Features.Contabilidad.Commands;
using OpenSource1.Application.Services.Contabilidad;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Contabilidad.Handlers;

/// <summary>Delega en <see cref="IPosteoCostoInventario"/> (que gestiona sus propias transacciones por producto y fecha).</summary>
public sealed class PostearCostoInventarioCommandHandler(IPosteoCostoInventario posteo)
    : IRequestHandler<PostearCostoInventarioCommand, Result<ResultadoPosteoCostoInventario>>
{
    public async Task<Result<ResultadoPosteoCostoInventario>> Handle(
        PostearCostoInventarioCommand request, CancellationToken cancellationToken) =>
        Result<ResultadoPosteoCostoInventario>.Exito(await posteo.PostearAsync(request.ProductoId, cancellationToken));
}
