using MediatR;
using OpenSource1.Application.Features.CategoriasProducto.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.CategoriasProducto.Commands;

public sealed record UpdateCategoriaProductoCommand(Guid Id, string Codigo, string Nombre, Guid? CategoriaPadreId)
    : IRequest<Result<CategoriaProductoResponse>>;
