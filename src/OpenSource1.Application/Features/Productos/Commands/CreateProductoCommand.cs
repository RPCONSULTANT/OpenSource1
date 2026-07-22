using MediatR;
using OpenSource1.Application.Features.Productos.Dtos;

namespace OpenSource1.Application.Features.Productos.Commands;

public sealed record CreateProductoCommand(
    string Codigo,
    string Nombre,
    decimal Precio,
    int Stock,
    string CategoriaCodigo,
    string CategoriaNombre,
    string UnidadMedidaCodigo,
    string? ImagePath = null) : IRequest<ProductoResponse>;
