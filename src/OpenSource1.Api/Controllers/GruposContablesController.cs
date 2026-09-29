using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenSource1.Api.Infrastructure;
using OpenSource1.Application.Features.GruposContables;
using OpenSource1.Application.Features.GruposContables.Commands;
using OpenSource1.Application.Features.GruposContables.Dtos;
using OpenSource1.Application.Features.GruposContables.Queries;
using OpenSource1.Application.Security;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Api.Controllers;

/// <summary>
/// Mantenimiento genérico de los cinco grupos contables simples (Task 5.3). El tipo va por NOMBRE en la ruta:
/// <c>negocio</c>, <c>producto</c>, <c>iva-negocio</c>, <c>iva-producto</c>, <c>inventario</c> (ver <see cref="TiposGrupoContable"/>);
/// un tipo desconocido es un recurso inexistente → 404.
/// </summary>
[ApiController]
[Route("api/grupos-contables/{tipo}")]
public sealed class GruposContablesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<PagedResult<GrupoContableResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(
        string tipo,
        [FromQuery] string? codigo,
        [FromQuery] string? descripcion,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = PageRequest.TamanoPorDefecto,
        [FromQuery] string? ordenarPor = null,
        [FromQuery] bool descendente = false,
        CancellationToken cancellationToken = default)
    {
        if (TiposGrupoContable.DesdeRuta(tipo) is not { } tipoGrupo)
        {
            return TipoNoEncontrado(tipo);
        }

        var result = await sender.Send(
            new ListGruposContablesQuery(
                tipoGrupo,
                new GrupoContableSearchCriteria(codigo, descripcion),
                new PageRequest(pagina, tamanoPagina, ordenarPor, descendente)),
            cancellationToken);

        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<GrupoContableResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string tipo, Guid id, CancellationToken cancellationToken)
    {
        if (TiposGrupoContable.DesdeRuta(tipo) is not { } tipoGrupo)
        {
            return TipoNoEncontrado(tipo);
        }

        var result = await sender.Send(new GetGrupoContableByIdQuery(tipoGrupo, id), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpPost]
    [Authorize(Policy = ApplicationPolicies.CanAdd)]
    [ProducesResponseType<GrupoContableResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(string tipo, CreateGrupoContableRequest request, CancellationToken cancellationToken)
    {
        if (TiposGrupoContable.DesdeRuta(tipo) is not { } tipoGrupo)
        {
            return TipoNoEncontrado(tipo);
        }

        var result = await sender.Send(new CreateGrupoContableCommand(tipoGrupo, request.Codigo, request.Descripcion), cancellationToken);

        return result.EsFallo
            ? result.ToActionResult()
            : CreatedAtAction(nameof(GetById), new { tipo = TiposGrupoContable.De(tipoGrupo).Ruta, id = result.Valor.Id }, result.Valor);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanModify)]
    [ProducesResponseType<GrupoContableResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(string tipo, Guid id, UpdateGrupoContableRequest request, CancellationToken cancellationToken)
    {
        if (TiposGrupoContable.DesdeRuta(tipo) is not { } tipoGrupo)
        {
            return TipoNoEncontrado(tipo);
        }

        var result = await sender.Send(
            new UpdateGrupoContableCommand(tipoGrupo, id, request.Codigo, request.Descripcion, request.Xmin), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(string tipo, Guid id, CancellationToken cancellationToken)
    {
        if (TiposGrupoContable.DesdeRuta(tipo) is not { } tipoGrupo)
        {
            return TipoNoEncontrado(tipo);
        }

        var result = await sender.Send(new DeleteGrupoContableCommand(tipoGrupo, id), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : NoContent();
    }

    private IActionResult TipoNoEncontrado(string tipo) =>
        Result.Fallo(new Error(
                "tipo_grupo_contable.no_encontrado",
                $"No existe el tipo de grupo contable '{tipo}'. Tipos válidos: {string.Join(", ", TiposGrupoContable.Todos.Select(t => t.Ruta))}.",
                "tipo"))
            .ToActionResult();
}

public sealed record CreateGrupoContableRequest(string Codigo, string Descripcion);

public sealed record UpdateGrupoContableRequest(string Codigo, string Descripcion, long Xmin);
