using System.Net.Http.Json;
using OpenSource1.Application.Features.Cobros;
using OpenSource1.Application.Features.MovimientosCliente.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Blazor.Services;

/// <summary>
/// Cliente tipado del libro de clientes (<c>api/clientes/{id}/saldo|movimientos|movimientos-abiertos</c>) y de los cobros
/// (<c>api/cobros</c>, <c>api/cobros/aplicaciones</c>) — Task 6.6. Registrar y aplicar exigen CanModify en la API.
/// </summary>
public interface ICobroApiClient
{
    /// <summary><see langword="null"/> = el socio no existe (404).</summary>
    Task<SaldoClienteResponse?> GetSaldoAsync(Guid socioId, CancellationToken cancellationToken = default);

    /// <summary>Abiertos (restante ≠ 0) en orden cronológico. <see langword="null"/> = el socio no existe (404).</summary>
    Task<IReadOnlyList<MovimientoClienteResponse>?> ListMovimientosAbiertosAsync(Guid socioId, CancellationToken cancellationToken = default);

    /// <summary>Todos los movimientos, paginados. <see langword="null"/> = el socio no existe (404).</summary>
    Task<PagedResult<MovimientoClienteResponse>?> ListMovimientosAsync(
        Guid socioId, bool? soloAbiertos = null, PageRequest? paginacion = null, CancellationToken cancellationToken = default);

    Task<VentaOperationResult<ResultadoPagoCliente>> RegistrarPagoAsync(RegistrarPagoInput input, CancellationToken cancellationToken = default);

    Task<VentaOperationResult<ResultadoAplicacionPago>> AplicarPagoAsync(AplicarPagoInput input, CancellationToken cancellationToken = default);
}

/// <summary>Pago del cliente. <c>CuentaCajaId</c> null = la cuenta por defecto de la API (<c>1101 Caja</c>).</summary>
public sealed record RegistrarPagoInput(Guid SocioNegocioId, decimal Importe, DateOnly FechaRegistro, Guid? CuentaCajaId, string? Descripcion);

/// <summary>Aplicación de un pago a una factura del mismo cliente. <c>FechaRegistro</c> null = hoy.</summary>
public sealed record AplicarPagoInput(long MovimientoFacturaId, long MovimientoPagoId, decimal Importe, DateOnly? FechaRegistro);

public sealed class CobroApiClient(HttpClient httpClient, ILogger<CobroApiClient> logger) : ICobroApiClient
{
    private const string SocioNoEncontrado = "No se encontró el cliente indicado.";

    public Task<SaldoClienteResponse?> GetSaldoAsync(Guid socioId, CancellationToken cancellationToken = default) =>
        VentaApiRespuestas.GetOrNullAsync<SaldoClienteResponse>(
            httpClient, $"api/clientes/{socioId}/saldo", "el saldo del cliente", logger, cancellationToken);

    public async Task<IReadOnlyList<MovimientoClienteResponse>?> ListMovimientosAbiertosAsync(
        Guid socioId, CancellationToken cancellationToken = default) =>
        await VentaApiRespuestas.GetOrNullAsync<List<MovimientoClienteResponse>>(
            httpClient, $"api/clientes/{socioId}/movimientos-abiertos", "los movimientos abiertos del cliente", logger, cancellationToken);

    public Task<PagedResult<MovimientoClienteResponse>?> ListMovimientosAsync(
        Guid socioId, bool? soloAbiertos = null, PageRequest? paginacion = null, CancellationToken cancellationToken = default)
    {
        var parameters = new List<string>();
        if (soloAbiertos is { } abiertos)
        {
            parameters.Add($"soloAbiertos={(abiertos ? "true" : "false")}");
        }

        return VentaApiRespuestas.GetOrNullAsync<PagedResult<MovimientoClienteResponse>>(
            httpClient, ApiRespuestas.ConPaginacion($"api/clientes/{socioId}/movimientos", parameters, paginacion),
            "los movimientos del cliente", logger, cancellationToken);
    }

    public async Task<VentaOperationResult<ResultadoPagoCliente>> RegistrarPagoAsync(
        RegistrarPagoInput input, CancellationToken cancellationToken = default)
    {
        var body = new PagoBody(input.SocioNegocioId, input.Importe, input.FechaRegistro, null, input.CuentaCajaId, input.Descripcion);
        using var response = await httpClient.PostAsJsonAsync("api/cobros", body, cancellationToken);
        return await VentaApiRespuestas.ToResultAsync<ResultadoPagoCliente>(
            response, "Pago registrado.", SocioNoEncontrado, "cobros", logger, cancellationToken);
    }

    public async Task<VentaOperationResult<ResultadoAplicacionPago>> AplicarPagoAsync(
        AplicarPagoInput input, CancellationToken cancellationToken = default)
    {
        var body = new AplicacionBody(input.MovimientoFacturaId, input.MovimientoPagoId, input.Importe, input.FechaRegistro);
        using var response = await httpClient.PostAsJsonAsync("api/cobros/aplicaciones", body, cancellationToken);
        return await VentaApiRespuestas.ToResultAsync<ResultadoAplicacionPago>(
            response, "Pago aplicado.", "No se encontró el movimiento indicado.", "aplicaciones de cobro", logger, cancellationToken);
    }

    // Cuerpos de red exactos de CobrosController.
    private sealed record PagoBody(
        Guid SocioNegocioId, decimal Importe, DateOnly FechaRegistro, DateOnly? FechaDocumento, Guid? CuentaCajaId, string? Descripcion);

    private sealed record AplicacionBody(long MovimientoFacturaId, long MovimientoPagoId, decimal Importe, DateOnly? FechaRegistro);
}
