using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenSource1.Api.Infrastructure;
using OpenSource1.Application.Features.UnidadesMedida;
using OpenSource1.Application.Features.UnidadesMedida.Commands;
using OpenSource1.Application.Features.UnidadesMedida.Dtos;
using OpenSource1.Application.Features.UnidadesMedida.Queries;
using OpenSource1.Application.Security;
using OpenSource1.Core.Common;

namespace OpenSource1.Api.Controllers;

[ApiController]
[Route("api/unidades-medida")]
public sealed class UnidadesMedidaController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<PagedResult<UnidadMedidaResponse>>(StatusCodes.Status200OK)]
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
            new ListUnidadesMedidaQuery(
                new UnidadMedidaSearchCriteria(codigo, nombre),
                new PageRequest(pagina, tamanoPagina, ordenarPor, descendente)),
            cancellationToken);

        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<UnidadMedidaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetUnidadMedidaByIdQuery(id), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpPost]
    [Authorize(Policy = ApplicationPolicies.CanAdd)]
    [ProducesResponseType<UnidadMedidaResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateUnidadMedidaRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateUnidadMedidaCommand(request.Codigo, request.Nombre, request.Decimales), cancellationToken);

        return result.EsFallo
            ? result.ToActionResult()
            : CreatedAtAction(nameof(GetById), new { id = result.Valor.Id }, result.Valor);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanModify)]
    [ProducesResponseType<UnidadMedidaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, UpdateUnidadMedidaRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateUnidadMedidaCommand(id, request.Codigo, request.Nombre, request.Decimales), cancellationToken);

        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteUnidadMedidaCommand(id), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : NoContent();
    }
}

public sealed record CreateUnidadMedidaRequest(string Codigo, string Nombre, short Decimales);

public sealed record UpdateUnidadMedidaRequest(string Codigo, string Nombre, short Decimales);
