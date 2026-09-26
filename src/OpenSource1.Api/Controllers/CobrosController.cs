using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenSource1.Api.Infrastructure;
using OpenSource1.Application.Features.Cobros;
using OpenSource1.Application.Security;

namespace OpenSource1.Api.Controllers;

/// <summary>
/// Cobros de clientes (Task 6.5): registrar un pago (movimiento de cliente Pago + asiento caja/CxC, serie <c>COBRO</c>) y aplicarlo
/// a una factura (solo detalle del libro de clientes, sin asiento). Ambas acciones exigen CanModify. Los movimientos abiertos del
/// cliente se consultan en <c>GET api/clientes/{id}/movimientos-abiertos</c>. Referencias inválidas en el cuerpo → 400 (no 404).
/// </summary>
[ApiController]
[Route("api/cobros")]
public sealed class CobrosController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Registra un pago del cliente. 200 con <c>{ numero, movimientoClienteId, registroContable }</c> (número COBRO, Id del movimiento
    /// de cliente para aplicarlo y número del registro contable). <c>fechaDocumento</c> por defecto = <c>fechaRegistro</c>;
    /// <c>cuentaCajaId</c> por defecto = <c>1101 Caja</c>.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = ApplicationPolicies.CanModify)]
    [ProducesResponseType<ResultadoPagoCliente>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegistrarPago(RegistrarPagoClienteRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RegistrarPagoClienteCommand(
                request.SocioNegocioId ?? Guid.Empty, request.Importe ?? 0m, request.FechaRegistro ?? default, request.FechaDocumento,
                request.CuentaCajaId, request.Descripcion),
            cancellationToken);

        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    /// <summary>
    /// Aplica un pago (movimiento con restante negativo) a una factura (restante positivo) del mismo cliente, por un importe ≤ el
    /// mínimo de los dos restantes. 200 con los restantes resultantes. <c>fechaRegistro</c> por defecto = hoy.
    /// </summary>
    [HttpPost("aplicaciones")]
    [Authorize(Policy = ApplicationPolicies.CanModify)]
    [ProducesResponseType<ResultadoAplicacionPago>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Aplicar(AplicarPagoRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new AplicarPagoCommand(
                request.MovimientoFacturaId ?? 0, request.MovimientoPagoId ?? 0, request.Importe ?? 0m, request.FechaRegistro),
            cancellationToken);

        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }
}

/// <summary>Cuerpo de <c>POST api/cobros</c>. Los campos ausentes llegan como nulos y el handler los rechaza con su campo.</summary>
public sealed record RegistrarPagoClienteRequest(
    Guid? SocioNegocioId,
    decimal? Importe,
    DateOnly? FechaRegistro,
    DateOnly? FechaDocumento,
    Guid? CuentaCajaId,
    string? Descripcion);

/// <summary>Cuerpo de <c>POST api/cobros/aplicaciones</c>.</summary>
public sealed record AplicarPagoRequest(
    long? MovimientoFacturaId,
    long? MovimientoPagoId,
    decimal? Importe,
    DateOnly? FechaRegistro);
