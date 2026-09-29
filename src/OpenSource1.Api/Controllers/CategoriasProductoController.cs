using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenSource1.Api.Infrastructure;
using OpenSource1.Application.Features.CategoriasProducto;
using OpenSource1.Application.Features.CategoriasProducto.Commands;
using OpenSource1.Application.Features.CategoriasProducto.Dtos;
using OpenSource1.Application.Features.CategoriasProducto.Queries;
using OpenSource1.Application.Security;
using OpenSource1.Core.Common;

namespace OpenSource1.Api.Controllers;

[ApiController]
[Route("api/categorias-producto")]
public sealed class CategoriasProductoController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<PagedResult<CategoriaProductoResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? codigo,
        [FromQuery] string? nombre,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = PageRequest.TamanoPorDefecto,
        [FromQuery] string? ordenarPor = null,
        [FromQuery] bool descendente = true,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new ListCategoriasProductoQuery(
                new CategoriaProductoSearchCriteria(codigo, nombre),
                new PageRequest(pagina, tamanoPagina, ordenarPor, descendente)),
            cancellationToken);

        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<CategoriaProductoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCategoriaProductoByIdQuery(id), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpPost]
    [Authorize(Policy = ApplicationPolicies.CanAdd)]
    [ProducesResponseType<CategoriaProductoResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateCategoriaProductoRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateCategoriaProductoCommand(request.Codigo, request.Nombre, request.CategoriaPadreId), cancellationToken);

        return result.EsFallo
            ? result.ToActionResult()
            : CreatedAtAction(nameof(GetById), new { id = result.Valor.Id }, result.Valor);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanModify)]
    [ProducesResponseType<CategoriaProductoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, UpdateCategoriaProductoRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateCategoriaProductoCommand(id, request.Codigo, request.Nombre, request.CategoriaPadreId), cancellationToken);

        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteCategoriaProductoCommand(id), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : NoContent();
    }
}

public sealed record CreateCategoriaProductoRequest(string Codigo, string Nombre, Guid? CategoriaPadreId);

public sealed record UpdateCategoriaProductoRequest(string Codigo, string Nombre, Guid? CategoriaPadreId);
