using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OpenSource1.Application.Features.Inventario.Consultas;
using OpenSource1.Application.Features.Inventario.Consultas.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Blazor.Services;

public sealed class InventarioConsultasApiClient(HttpClient httpClient, ILogger<InventarioConsultasApiClient> logger)
    : IInventarioConsultasApiClient
{
    private const string BaseRoute = "api/inventario";

    public Task<ConsultaResultado<PagedResult<MovimientoProductoVistaResponse>>> ListMovimientosProductoAsync(
        MovimientoProductoVistaCriterios criterios, PageRequest paginacion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criterios);
        var parameters = FiltrosMovimientos(
            criterios.ProductoId, criterios.AlmacenId, criterios.Desde, criterios.Hasta, criterios.TipoMovimiento,
            criterios.TipoOrigen, criterios.NumeroDocumento);
        return GetAsync<PagedResult<MovimientoProductoVistaResponse>>(
            ApiRespuestas.ConPaginacion($"{BaseRoute}/movimientos-producto", parameters, paginacion), "los movimientos de producto",
            cancellationToken);
    }

    public Task<ConsultaResultado<PagedResult<MovimientoValorVistaResponse>>> ListMovimientosValorAsync(
        MovimientoValorVistaCriterios criterios, PageRequest paginacion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criterios);
        var parameters = FiltrosMovimientos(
            criterios.ProductoId, criterios.AlmacenId, criterios.Desde, criterios.Hasta, criterios.TipoMovimiento,
            criterios.TipoOrigen, criterios.NumeroDocumento);
        if (criterios.SoloAjustes)
        {
            parameters.Add("soloAjustes=true");
        }

        return GetAsync<PagedResult<MovimientoValorVistaResponse>>(
            ApiRespuestas.ConPaginacion($"{BaseRoute}/movimientos-valor", parameters, paginacion), "los movimientos de valor",
            cancellationToken);
    }

    public Task<ConsultaResultado<ExistenciasVistaResponse>> ListExistenciasAsync(
        ExistenciaVistaCriterios criterios, PageRequest paginacion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criterios);
        var parameters = new List<string>();
        if (criterios.AlmacenId is { } almacenId) parameters.Add($"almacenId={almacenId}");
        if (criterios.ProductoId is { } productoId) parameters.Add($"productoId={productoId}");
        if (!string.IsNullOrWhiteSpace(criterios.Texto)) parameters.Add($"texto={Uri.EscapeDataString(criterios.Texto.Trim())}");
        if (criterios.Fecha is { } fecha) parameters.Add($"fecha={Fecha(fecha)}");
        if (criterios.SoloConExistencia) parameters.Add("soloConExistencia=true");

        return GetAsync<ExistenciasVistaResponse>(
            ApiRespuestas.ConPaginacion($"{BaseRoute}/existencias", parameters, paginacion), "las existencias", cancellationToken);
    }

    private static List<string> FiltrosMovimientos(
        Guid? productoId, Guid? almacenId, DateOnly? desde, DateOnly? hasta, int? tipoMovimiento, int? tipoOrigen,
        string? numeroDocumento)
    {
        var parameters = new List<string>();
        if (productoId is { } p) parameters.Add($"productoId={p}");
        if (almacenId is { } a) parameters.Add($"almacenId={a}");
        if (desde is { } d) parameters.Add($"desde={Fecha(d)}");
        if (hasta is { } h) parameters.Add($"hasta={Fecha(h)}");
        if (tipoMovimiento is { } tm) parameters.Add($"tipoMovimiento={tm.ToString(CultureInfo.InvariantCulture)}");
        if (tipoOrigen is { } to) parameters.Add($"tipoOrigen={to.ToString(CultureInfo.InvariantCulture)}");
        if (!string.IsNullOrWhiteSpace(numeroDocumento)) parameters.Add($"numeroDocumento={Uri.EscapeDataString(numeroDocumento.Trim())}");
        return parameters;
    }

    private static string Fecha(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private async Task<ConsultaResultado<T>> GetAsync<T>(string url, string entidad, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(url, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            T? payload;
            try
            {
                payload = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "API de inventario devolvió {StatusCode} con un cuerpo ilegible al obtener {Entidad}.", response.StatusCode, entidad);
                payload = default;
            }

            return payload is null
                ? new ConsultaResultado<T>(false, $"La API no devolvió {entidad}.")
                : new ConsultaResultado<T>(true, string.Empty, payload);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("API de inventario devolvió {StatusCode} al obtener {Entidad}. Body: {Body}", response.StatusCode, entidad, body);

        var mensajes = response.StatusCode == HttpStatusCode.BadRequest ? ApiRespuestas.ExtraerMensajes(body) : [];
        var safe = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Debe iniciar sesión nuevamente.",
            HttpStatusCode.Forbidden => "No tiene permisos para consultar el inventario.",
            HttpStatusCode.BadRequest => mensajes.Count > 0 ? "Revise los filtros:" : "Revise los filtros.",
            _ => $"No fue posible cargar {entidad}."
        };
        return new ConsultaResultado<T>(false, safe, Errors: mensajes);
    }
}
