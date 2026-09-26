using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenSource1.Api.Infrastructure;
using OpenSource1.Application.Features.MovimientosCliente;
using OpenSource1.Application.Features.MovimientosCliente.Dtos;
using OpenSource1.Application.Features.MovimientosCliente.Queries;
using OpenSource1.Application.Security;
using OpenSource1.Core.Common;

namespace OpenSource1.Api.Controllers;

/// <summary>
/// Libro de clientes (Task 6.3): la UI llama "Clientes" a los socios de negocio (el maestro vive en <c>api/socios-negocio</c>).
/// Solo consultas, todas derivadas del detalle (nada almacenado). <c>{id}</c> es el socio: inexistente o borrado → 404. Lo
/// escriben el motor de posteo de facturas (Task 6.4) y los cobros (Task 6.5).
/// </summary>
[ApiController]
[Route("api/clientes")]
public sealed class ClientesController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Movimientos del cliente con su importe restante derivado y la marca de abierto; por defecto en orden cronológico
    /// ascendente. <c>soloAbiertos</c>: <c>true</c> abiertos, <c>false</c> cerrados, ausente todos.
    /// </summary>
    [HttpGet("{id:guid}/movimientos")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<PagedResult<MovimientoClienteResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListMovimientos(
        Guid id,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        [FromQuery] bool? soloAbiertos,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = PageRequest.TamanoPorDefecto,
        [FromQuery] string? ordenarPor = null,
        [FromQuery] bool descendente = false,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new ListMovimientosClienteQuery(
                new MovimientoClienteSearchCriteria(id, desde, hasta, soloAbiertos),
                new PageRequest(pagina, tamanoPagina, ordenarPor, descendente)),
            cancellationToken);

        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    /// <summary>Saldo derivado del cliente (<c>Σ detalle.Importe</c>; positivo = nos debe) y número de movimientos abiertos.</summary>
    [HttpGet("{id:guid}/saldo")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<SaldoClienteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSaldo(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetSaldoClienteQuery(id), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }
}
