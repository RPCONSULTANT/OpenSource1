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
/// escriben el motor de posteo de facturas (Task 6.4) y los cobros (Task 6.5, <c>api/cobros</c>).
/// </summary>
[ApiController]
[Route("api/clientes")]
public sealed class ClientesController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Movimientos del cliente con su importe restante derivado y la marca de abierto; por defecto en orden cronológico
    /// ascendente. <c>soloAbiertos</c>: <c>true</c> abiertos, <c>false</c> cerrados, ausente todos. Task 7.3:
    /// <c>tipoDocumento</c> (1 Factura, 2 Nota de crédito, 3 Pago, 4 Ajuste; otro → 400) y <c>fechaCorte</c> (restante y abierto
    /// A ESA FECHA, con la regla del estado de cuenta, sin los movimientos registrados después; ausente = restante actual).
    /// Fechas invertidas o una página fuera de rango → 400. Orden: <c>Id</c>, <c>FechaRegistro</c>, <c>FechaVencimiento</c>,
    /// <c>NumeroDocumento</c>, <c>ImporteOriginal</c>, <c>ImporteRestante</c> (otra columna se ignora).
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
        [FromQuery] int? tipoDocumento,
        [FromQuery] DateOnly? fechaCorte,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = PageRequest.TamanoPorDefecto,
        [FromQuery] string? ordenarPor = null,
        [FromQuery] bool descendente = false,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new ListMovimientosClienteQuery(
                new MovimientoClienteSearchCriteria(id, desde, hasta, soloAbiertos, tipoDocumento, fechaCorte),
                new PageRequest(pagina, tamanoPagina, ordenarPor, descendente)),
            cancellationToken);

        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    /// <summary>
    /// Estado de cuenta por antigüedad de saldos (Task 7.3) a <c>fechaCorte</c> (incluida; por defecto hoy UTC), paginado por
    /// cliente: por cada cliente con movimientos registrados hasta esa fecha, el restante de sus documentos A ESA FECHA repartido
    /// por días vencidos (<c>corriente</c>, <c>dias1a30</c>, <c>dias31a60</c>, <c>dias61a90</c>, <c>mas90</c>), los restantes
    /// negativos en <c>sinAplicar</c> y el <c>total</c> (= saldo a esa fecha), más los <c>totales</c> de todos los clientes
    /// filtrados. <c>socioId</c> = un cliente (inexistente → página vacía); <c>texto</c> busca en código y nombre;
    /// <c>soloConSaldo</c> oculta los clientes sin documentos abiertos. Orden: <c>Nombre</c> (por defecto), <c>Codigo</c>,
    /// <c>Total</c>; desempate por el Id del socio. Una página fuera de rango → 400.
    /// </summary>
    [HttpGet("estado-cuenta")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<EstadoCuentaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetEstadoCuenta(
        [FromQuery] DateOnly? fechaCorte,
        [FromQuery] Guid? socioId,
        [FromQuery] string? texto,
        [FromQuery] bool soloConSaldo = false,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = PageRequest.TamanoPorDefecto,
        [FromQuery] string? ordenarPor = null,
        [FromQuery] bool descendente = false,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new GetEstadoCuentaQuery(
                new EstadoCuentaCriterios(fechaCorte, socioId, texto, soloConSaldo),
                new PageRequest(pagina, tamanoPagina, ordenarPor, descendente)),
            cancellationToken);
        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    /// <summary>
    /// Movimientos ABIERTOS del cliente (restante derivado ≠ 0: facturas pendientes y pagos sin aplicar), sin paginar y en orden
    /// cronológico; los usa la aplicación de cobros (Task 6.5).
    /// </summary>
    [HttpGet("{id:guid}/movimientos-abiertos")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<IReadOnlyList<MovimientoClienteResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListMovimientosAbiertos(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListMovimientosAbiertosClienteQuery(id), cancellationToken);
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
