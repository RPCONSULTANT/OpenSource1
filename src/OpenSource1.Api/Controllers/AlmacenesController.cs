using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenSource1.Api.Infrastructure;
using OpenSource1.Application.Features.Almacenes;
using OpenSource1.Application.Features.Almacenes.Commands;
using OpenSource1.Application.Features.Almacenes.Dtos;
using OpenSource1.Application.Features.Almacenes.Queries;
using OpenSource1.Application.Security;
using OpenSource1.Core.Common;

namespace OpenSource1.Api.Controllers;

[ApiController]
[Route("api/almacenes")]
public sealed class AlmacenesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<PagedResult<AlmacenResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? codigo,
        [FromQuery] string? nombre,
        [FromQuery] bool? bloqueado,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = PageRequest.TamanoPorDefecto,
        [FromQuery] string? ordenarPor = null,
        [FromQuery] bool descendente = true,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new ListAlmacenesQuery(
                new AlmacenSearchCriteria(codigo, nombre, bloqueado),
                new PageRequest(pagina, tamanoPagina, ordenarPor, descendente)),
            cancellationToken);

        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<AlmacenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetAlmacenByIdQuery(id), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpPost]
    [Authorize(Policy = ApplicationPolicies.CanAdd)]
    [ProducesResponseType<AlmacenResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateAlmacenRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateAlmacenCommand(
                request.Codigo, request.Nombre, request.DireccionLinea1, request.DireccionLinea2,
                request.Ciudad, request.PaisCodigo, request.Bloqueado, request.EsPredeterminado),
            cancellationToken);

        return result.EsFallo
            ? result.ToActionResult()
            : CreatedAtAction(nameof(GetById), new { id = result.Valor.Id }, result.Valor);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanModify)]
    [ProducesResponseType<AlmacenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, UpdateAlmacenRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateAlmacenCommand(
                id, request.Codigo, request.Nombre, request.DireccionLinea1, request.DireccionLinea2,
                request.Ciudad, request.PaisCodigo, request.Bloqueado, request.EsPredeterminado),
            cancellationToken);

        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteAlmacenCommand(id), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : NoContent();
    }
}

public sealed record CreateAlmacenRequest(
    string Codigo,
    string Nombre,
    string? DireccionLinea1,
    string? DireccionLinea2,
    string? Ciudad,
    string? PaisCodigo,
    bool Bloqueado,
    bool EsPredeterminado);

public sealed record UpdateAlmacenRequest(
    string Codigo,
    string Nombre,
    string? DireccionLinea1,
    string? DireccionLinea2,
    string? Ciudad,
    string? PaisCodigo,
    bool? Bloqueado,
    bool? EsPredeterminado);
