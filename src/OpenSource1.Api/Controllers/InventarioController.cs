using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenSource1.Api.Infrastructure;
using OpenSource1.Application.Features.Inventario.Commands;
using OpenSource1.Application.Security;
using OpenSource1.Application.Services.Inventario;

namespace OpenSource1.Api.Controllers;

[ApiController]
[Route("api/inventario")]
public sealed class InventarioController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Rutina "Ajustar costo movimientos": todos los productos con <c>CostoAjustado = false</c>, o solo
    /// <paramref name="productoId"/>. Idempotente. 200 con <c>{ productosAjustados, movimientosValorCreados }</c>.
    /// </summary>
    [HttpPost("ajustar-costo")]
    [Authorize(Policy = ApplicationPolicies.CanModify)]
    [ProducesResponseType<ResultadoAjusteCosto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AjustarCosto([FromQuery] Guid? productoId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new AjustarCostoMovimientosCommand(productoId), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }
}
