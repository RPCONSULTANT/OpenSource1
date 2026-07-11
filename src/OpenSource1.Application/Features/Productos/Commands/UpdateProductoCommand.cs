using MediatR;
using OpenSource1.Application.Features.Productos.Dtos;

namespace OpenSource1.Application.Features.Productos.Commands;

public sealed record UpdateProductoCommand(Guid Id, string Codigo, string Nombre, decimal Precio, int Stock, string Categoria, string? ImagePath = null) : IRequest<ProductoResponse?>;
