using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenSource1.Api.Infrastructure;
using OpenSource1.Application.Features.Entradas.Commands;
using OpenSource1.Application.Features.Entradas.Dtos;
using OpenSource1.Application.Features.Entradas.Queries;
using OpenSource1.Application.Security;
using OpenSource1.Core.Common;
using System;

namespace OpenSource1.Api.Controllers;

[ApiController]
[Route("api/entradas")]
[Obsolete("Modulo de prueba obsoleto. No usar Entradas para nuevos desarrollos.")]
public sealed class EntradasController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<PagedResult<EntradaResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = PageRequest.TamanoPorDefecto,
        [FromQuery] string? ordenarPor = null,
        [FromQuery] bool descendente = true,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new ListEntradasQuery(new PageRequest(pagina, tamanoPagina, ordenarPor, descendente)), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<EntradaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EntradaResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetEntradaByIdQuery(id), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = ApplicationPolicies.CanAdd)]
    [ProducesResponseType<EntradaResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EntradaResponse>> Create(
        CreateEntradaRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Titulo) ||
            string.IsNullOrWhiteSpace(request.Tipo)   ||
            string.IsNullOrWhiteSpace(request.Estado))
            return BadRequest();

        var result = await sender.Send(
            new CreateEntradaCommand(request.Titulo, request.Descripcion, request.Tipo, request.Estado),
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanModify)]
    [ProducesResponseType<EntradaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EntradaResponse>> Update(
        Guid id,
        UpdateEntradaRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Titulo) ||
            string.IsNullOrWhiteSpace(request.Tipo)   ||
            string.IsNullOrWhiteSpace(request.Estado))
            return BadRequest();

        var result = await sender.Send(
            new UpdateEntradaCommand(id, request.Titulo, request.Descripcion, request.Tipo, request.Estado),
            cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await sender.Send(new DeleteEntradaCommand(id), cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}

public sealed record CreateEntradaRequest(string Titulo, string? Descripcion, string Tipo, string Estado);
public sealed record UpdateEntradaRequest(string Titulo, string? Descripcion, string Tipo, string Estado);
