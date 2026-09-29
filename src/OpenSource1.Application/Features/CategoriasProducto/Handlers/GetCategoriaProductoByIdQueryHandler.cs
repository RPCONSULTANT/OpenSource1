using MediatR;
using OpenSource1.Application.Features.CategoriasProducto.Dtos;
using OpenSource1.Application.Features.CategoriasProducto.Queries;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.CategoriasProducto.Handlers;

public sealed class GetCategoriaProductoByIdQueryHandler(ICategoriaProductoReadRepository readRepository)
    : IRequestHandler<GetCategoriaProductoByIdQuery, Result<CategoriaProductoResponse>>
{
    public async Task<Result<CategoriaProductoResponse>> Handle(GetCategoriaProductoByIdQuery request, CancellationToken cancellationToken)
    {
        var item = await readRepository.GetByIdAsync(request.Id, cancellationToken);

        return item is null
            ? Result<CategoriaProductoResponse>.Fallo(new Error(
                "categoria_producto.no_encontrado", "No se encontró la categoría solicitada.", "Id"))
            : Result<CategoriaProductoResponse>.Exito(item);
    }
}
