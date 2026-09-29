using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenSource1.Api.Infrastructure;
using OpenSource1.Application.Features.GruposClienteContable;
using OpenSource1.Application.Features.GruposClienteContable.Commands;
using OpenSource1.Application.Features.GruposClienteContable.Dtos;
using OpenSource1.Application.Features.GruposClienteContable.Queries;
using OpenSource1.Application.Security;
using OpenSource1.Core.Common;

namespace OpenSource1.Api.Controllers;

[ApiController]
[Route("api/grupos-cliente-contable")]
public sealed class GruposClienteContableController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<PagedResult<GrupoClienteContableResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? codigo,
        [FromQuery] string? descripcion,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = PageRequest.TamanoPorDefecto,
        [FromQuery] string? ordenarPor = null,
        [FromQuery] bool descendente = false,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new ListGruposClienteContableQuery(
                new GrupoClienteContableSearchCriteria(codigo, descripcion),
                new PageRequest(pagina, tamanoPagina, ordenarPor, descendente)),
            cancellationToken);

        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<GrupoClienteContableResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetGrupoClienteContableByIdQuery(id), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpPost]
    [Authorize(Policy = ApplicationPolicies.CanAdd)]
    [ProducesResponseType<GrupoClienteContableResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateGrupoClienteContableRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateGrupoClienteContableCommand(
                request.Codigo, request.Descripcion, request.CuentaCxCId, request.CuentaDescuentoId, request.CuentaInteresId),
            cancellationToken);

        return result.EsFallo
            ? result.ToActionResult()
            : CreatedAtAction(nameof(GetById), new { id = result.Valor.Id }, result.Valor);
    }

    /// <summary>
    /// Modificación. <c>codigo</c>, <c>descripcion</c> y <c>cuentaCxCId</c> son de reemplazo completo; <c>cuentaDescuentoId</c> y
    /// <c>cuentaInteresId</c>: ausente/<c>null</c> = conservar, Guid vacío = quitar. <c>xmin</c> obligatorio.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanModify)]
    [ProducesResponseType<GrupoClienteContableResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, UpdateGrupoClienteContableRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateGrupoClienteContableCommand(
                id, request.Codigo, request.Descripcion, request.CuentaCxCId, request.CuentaDescuentoId, request.CuentaInteresId, request.Xmin),
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
        var result = await sender.Send(new DeleteGrupoClienteContableCommand(id), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : NoContent();
    }
}

public sealed record CreateGrupoClienteContableRequest(
    string Codigo,
    string Descripcion,
    Guid CuentaCxCId,
    Guid? CuentaDescuentoId = null,
    Guid? CuentaInteresId = null);

public sealed record UpdateGrupoClienteContableRequest(
    string Codigo,
    string Descripcion,
    Guid CuentaCxCId,
    Guid? CuentaDescuentoId,
    Guid? CuentaInteresId,
    long Xmin);
