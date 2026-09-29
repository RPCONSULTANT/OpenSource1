using MediatR;
using OpenSource1.Application.Features.Productos.Queries;
using OpenSource1.Application.Services.Inventario;

namespace OpenSource1.Application.Features.Productos.Handlers;

public sealed class GetProductoExistenciasQueryHandler(IProductoReadRepository readRepository, IConsultaInventario consultaInventario)
    : IRequestHandler<GetProductoExistenciasQuery, IReadOnlyList<ExistenciaAlmacen>?>
{
    public async Task<IReadOnlyList<ExistenciaAlmacen>?> Handle(GetProductoExistenciasQuery request, CancellationToken cancellationToken)
    {
        var producto = await readRepository.GetByIdAsync(request.ProductoId, cancellationToken);
        if (producto is null)
        {
            return null;
        }

        return await consultaInventario.ExistenciasPorAlmacenAsync(request.ProductoId, cancellationToken);
    }
}
