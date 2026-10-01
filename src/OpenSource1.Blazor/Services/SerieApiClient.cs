using System.Net.Http.Json;
using OpenSource1.Application.Features.Series.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Blazor.Services;

public sealed class SerieApiClient(HttpClient httpClient, ILogger<SerieApiClient> logger) : ISerieApiClient
{
    private const string Ruta = "api/series";
    private const string NoEncontrada = "No se encontró la serie de numeración indicada (puede haber sido eliminada).";
    private const string Entidad = "series de numeración";

    public async Task<PagedResult<SerieResponse>> ListAsync(SerieFiltro? filtro = null, PageRequest? paginacion = null, CancellationToken cancellationToken = default)
    {
        var pagina = paginacion ?? new PageRequest(1, PageRequest.TamanoPorDefecto, "Codigo", Descendente: false);
        var parameters = new List<string>();
        if (!string.IsNullOrWhiteSpace(filtro?.Codigo))
        {
            parameters.Add($"codigo={Uri.EscapeDataString(filtro.Codigo.Trim())}");
        }

        if (filtro?.Tipo is { } tipo)
        {
            parameters.Add($"tipo={tipo}");
        }

        if (filtro?.Activa is { } activa)
        {
            parameters.Add($"activa={(activa ? "true" : "false")}");
        }

        using var response = await httpClient.GetAsync(ApiRespuestas.ConPaginacion(Ruta, parameters, pagina), cancellationToken);
        await VentaApiRespuestas.AsegurarExitoAsync(response, Entidad, logger, cancellationToken);
        return await response.Content.ReadFromJsonAsync<PagedResult<SerieResponse>>(cancellationToken) ?? PagedResult<SerieResponse>.Vacio(pagina);
    }

    public async Task<IReadOnlyList<SerieResponse>> ActivasDelTipoAsync(TipoDocumentoSerie tipo, CancellationToken cancellationToken = default) =>
        (await ListAsync(new SerieFiltro(null, (int)tipo, true), new PageRequest(1, PageRequest.TamanoMaximo, "Codigo", Descendente: false), cancellationToken)).Items;

    public Task<SerieDetalleResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        VentaApiRespuestas.GetOrNullAsync<SerieDetalleResponse>(httpClient, $"{Ruta}/{id}", "la serie de numeración", logger, cancellationToken);

    public async Task<VentaOperationResult<ProximoNumeroResponse>> ProximoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"{Ruta}/{id}/proximo", cancellationToken);
        return await VentaApiRespuestas.ToResultAsync<ProximoNumeroResponse>(response, "Próximo número.", NoEncontrada, Entidad, logger, cancellationToken);
    }

    public async Task<VentaOperationResult<SerieResponse>> CreateAsync(SerieInput input, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(Ruta, input, cancellationToken);
        return await VentaApiRespuestas.ToResultAsync<SerieResponse>(response, "Serie creada.", NoEncontrada, Entidad, logger, cancellationToken);
    }

    public async Task<VentaOperationResult<SerieResponse>> UpdateAsync(Guid id, SerieInput input, long xmin, CancellationToken cancellationToken = default)
    {
        var body = new UpdateSerieBody(input.Codigo, input.Descripcion, input.TipoDocumento, input.PermiteHuecos, input.Activa, xmin);
        using var response = await httpClient.PutAsJsonAsync($"{Ruta}/{id}", body, cancellationToken);
        return await VentaApiRespuestas.ToResultAsync<SerieResponse>(response, "Serie modificada.", NoEncontrada, Entidad, logger, cancellationToken);
    }

    public async Task<VentaOperationResult<bool>> DeleteAsync(Guid id, long xmin, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"{Ruta}/{id}?xmin={xmin}", cancellationToken);
        return await VentaApiRespuestas.ToPlainResultAsync(response, "Serie eliminada.", NoEncontrada, Entidad, logger, cancellationToken);
    }

    public async Task<VentaOperationResult<LineaSerieResponse>> CreateLineaAsync(Guid serieId, LineaSerieInput input, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync($"{Ruta}/{serieId}/lineas", input, cancellationToken);
        return await VentaApiRespuestas.ToResultAsync<LineaSerieResponse>(response, "Línea agregada.", NoEncontrada, Entidad, logger, cancellationToken);
    }

    public async Task<VentaOperationResult<LineaSerieResponse>> UpdateLineaAsync(
        Guid serieId, Guid lineaId, LineaSerieInput input, long xmin, CancellationToken cancellationToken = default)
    {
        var body = new UpdateLineaBody(input.NumeroInicial, input.NumeroFinal, input.FechaInicial, xmin, input.NumeroAviso, input.Incremento, input.Bloqueada);
        using var response = await httpClient.PutAsJsonAsync($"{Ruta}/{serieId}/lineas/{lineaId}", body, cancellationToken);
        return await VentaApiRespuestas.ToResultAsync<LineaSerieResponse>(response, "Línea modificada.", NoEncontrada, Entidad, logger, cancellationToken);
    }

    public async Task<VentaOperationResult<bool>> DeleteLineaAsync(Guid serieId, Guid lineaId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"{Ruta}/{serieId}/lineas/{lineaId}", cancellationToken);
        return await VentaApiRespuestas.ToPlainResultAsync(response, "Línea eliminada.", NoEncontrada, Entidad, logger, cancellationToken);
    }

    // Cuerpos de red exactos de SeriesController (Blazor no referencia el proyecto Api).
    private sealed record UpdateSerieBody(
        string Codigo, string Descripcion, TipoDocumentoSerie TipoDocumento, bool PermiteHuecos, bool Activa, long Xmin);

    private sealed record UpdateLineaBody(
        string NumeroInicial, string NumeroFinal, DateOnly FechaInicial, long Xmin, string? NumeroAviso, int Incremento, bool Bloqueada);
}
