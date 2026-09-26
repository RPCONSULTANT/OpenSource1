using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenSource1.Api.Infrastructure;
using OpenSource1.Application.Features.CuentasContables;
using OpenSource1.Application.Features.CuentasContables.Commands;
using OpenSource1.Application.Features.CuentasContables.Dtos;
using OpenSource1.Application.Features.CuentasContables.Queries;
using OpenSource1.Application.Security;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Api.Controllers;

[ApiController]
[Route("api/cuentas-contables")]
public sealed class CuentasContablesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<PagedResult<CuentaContableResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? numero,
        [FromQuery] string? nombre,
        [FromQuery] TipoCuentaContable? tipoCuenta,
        [FromQuery] TipoResultadoCuenta? tipoResultado,
        [FromQuery] bool? bloqueada,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = PageRequest.TamanoPorDefecto,
        [FromQuery] string? ordenarPor = null,
        [FromQuery] bool descendente = false,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new ListCuentasContablesQuery(
                new CuentaContableSearchCriteria(numero, nombre, tipoCuenta, tipoResultado, bloqueada),
                new PageRequest(pagina, tamanoPagina, ordenarPor, descendente)),
            cancellationToken);

        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<CuentaContableResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCuentaContableByIdQuery(id), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpPost]
    [Authorize(Policy = ApplicationPolicies.CanAdd)]
    [ProducesResponseType<CuentaContableResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateCuentaContableRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateCuentaContableCommand(
                request.Numero, request.Nombre, request.TipoCuenta, request.TipoResultado,
                request.PosteoDirecto, request.Bloqueada, request.Sangria),
            cancellationToken);

        return result.EsFallo
            ? result.ToActionResult()
            : CreatedAtAction(nameof(GetById), new { id = result.Valor.Id }, result.Valor);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanModify)]
    [ProducesResponseType<CuentaContableResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, UpdateCuentaContableRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateCuentaContableCommand(
                id, request.Numero, request.Nombre, request.TipoCuenta, request.TipoResultado,
                request.PosteoDirecto, request.Bloqueada, request.Sangria, request.Xmin),
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
        var result = await sender.Send(new DeleteCuentaContableCommand(id), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : NoContent();
    }
}

public sealed record CreateCuentaContableRequest(
    string Numero,
    string Nombre,
    TipoCuentaContable TipoCuenta,
    TipoResultadoCuenta TipoResultado,
    bool PosteoDirecto,
    bool Bloqueada,
    int Sangria);

public sealed record UpdateCuentaContableRequest(
    string Numero,
    string Nombre,
    TipoCuentaContable TipoCuenta,
    TipoResultadoCuenta TipoResultado,
    bool? PosteoDirecto,
    bool? Bloqueada,
    int? Sangria,
    long Xmin);
