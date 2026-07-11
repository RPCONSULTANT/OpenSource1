using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenSource1.Application.Features.Productos;
using OpenSource1.Application.Features.Productos.Commands;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Application.Features.Productos.Queries;
using OpenSource1.Application.Security;

namespace OpenSource1.Api.Controllers;

[ApiController]
[Route("api/productos")]
public sealed class ProductosController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<IReadOnlyList<ProductoResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductoResponse>>> List(
        [FromQuery] string? codigo,
        [FromQuery] string? nombre,
        [FromQuery] string? categoria,
        [FromQuery] string? precio,
        [FromQuery] string? stock,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ListProductosQuery(new ProductoSearchCriteria(codigo, nombre, categoria, precio, stock)),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<ProductoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductoResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await sender.Send(new GetProductoByIdQuery(id), cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = ApplicationPolicies.CanAdd)]
    [ProducesResponseType<ProductoResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductoResponse>> Create(CreateProductoRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Codigo) ||
            string.IsNullOrWhiteSpace(request.Nombre) ||
            string.IsNullOrWhiteSpace(request.Categoria) ||
            request.Precio < 0 ||
            request.Stock < 0)
        {
            return BadRequest();
        }

        var result = await sender.Send(
            new CreateProductoCommand(request.Codigo, request.Nombre, request.Precio, request.Stock, request.Categoria, request.ImagePath),
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanModify)]
    [ProducesResponseType<ProductoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductoResponse>> Update(Guid id, UpdateProductoRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Codigo) ||
            string.IsNullOrWhiteSpace(request.Nombre) ||
            string.IsNullOrWhiteSpace(request.Categoria) ||
            request.Precio < 0 ||
            request.Stock < 0)
        {
            return BadRequest();
        }

        var result = await sender.Send(
            new UpdateProductoCommand(id, request.Codigo, request.Nombre, request.Precio, request.Stock, request.Categoria, request.ImagePath),
            cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await sender.Send(new DeleteProductoCommand(id), cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}

public sealed record CreateProductoRequest(string Codigo, string Nombre, decimal Precio, int Stock, string Categoria, string? ImagePath = null);
public sealed record UpdateProductoRequest(string Codigo, string Nombre, decimal Precio, int Stock, string Categoria, string? ImagePath = null);
