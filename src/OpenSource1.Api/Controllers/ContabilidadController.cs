using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenSource1.Api.Infrastructure;
using OpenSource1.Application.Features.Contabilidad;
using OpenSource1.Application.Features.Contabilidad.Dtos;
using OpenSource1.Application.Features.Contabilidad.Queries;
using OpenSource1.Application.Security;
using OpenSource1.Core.Common;

namespace OpenSource1.Api.Controllers;

/// <summary>
/// Consultas del libro contable (Task 5.5). Solo lectura: el libro lo escribe únicamente <c>IRegistroContable</c> desde los
/// procesos de posteo (batch de costo, facturación). Las vistas completas llegan en la Fase 7.
/// </summary>
[ApiController]
[Route("api/contabilidad")]
public sealed class ContabilidadController(ISender sender) : ControllerBase
{
    /// <summary>Movimientos contables por cuenta y rango de fechas de registro; por defecto en orden cronológico ascendente.</summary>
    [HttpGet("movimientos")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<PagedResult<MovimientoContableResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListMovimientos(
        [FromQuery] Guid? cuentaId,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = PageRequest.TamanoPorDefecto,
        [FromQuery] string? ordenarPor = null,
        [FromQuery] bool descendente = false,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new ListMovimientosContablesQuery(
                new MovimientoContableSearchCriteria(cuentaId, desde, hasta),
                new PageRequest(pagina, tamanoPagina, ordenarPor, descendente)),
            cancellationToken);

        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    /// <summary>Registros contables, del más reciente al más antiguo por defecto, con sus totales derivados.</summary>
    [HttpGet("registros")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<PagedResult<RegistroContableResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListRegistros(
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = PageRequest.TamanoPorDefecto,
        [FromQuery] string? ordenarPor = null,
        [FromQuery] bool descendente = true,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new ListRegistrosContablesQuery(new PageRequest(pagina, tamanoPagina, ordenarPor, descendente)),
            cancellationToken);

        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }
}
