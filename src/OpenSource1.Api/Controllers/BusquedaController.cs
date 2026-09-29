using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenSource1.Api.Infrastructure;
using OpenSource1.Application.Features.Busqueda.Dtos;
using OpenSource1.Application.Features.Busqueda.Queries;
using OpenSource1.Application.Security;

namespace OpenSource1.Api.Controllers;

/// <summary>
/// Búsqueda global (Fix-Features A3): clientes, productos, facturas posteadas, borradores de factura, notas de crédito y sus
/// borradores. Hasta <c>limite</c> (1–20, por defecto 5) resultados por tipo; <c>q</c> recortado de 2 a 100 caracteres.
/// </summary>
[ApiController]
[Route("api/busqueda")]
public sealed class BusquedaController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<BusquedaGlobalResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Buscar(
        [FromQuery] string? q,
        [FromQuery] int limite = BuscarGlobalQuery.LimitePorDefecto,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new BuscarGlobalQuery(q, limite), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }
}
