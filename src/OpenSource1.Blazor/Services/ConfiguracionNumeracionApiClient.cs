using System.Net.Http.Json;
using OpenSource1.Application.Features.ConfiguracionNumeracion.Dtos;
using OpenSource1.Core.Enums;

namespace OpenSource1.Blazor.Services;

public sealed class ConfiguracionNumeracionApiClient(HttpClient httpClient, ILogger<ConfiguracionNumeracionApiClient> logger)
    : IConfiguracionNumeracionApiClient
{
    private const string Ruta = "api/configuracion/numeracion";

    public async Task<IReadOnlyList<ConfiguracionNumeracionResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(Ruta, cancellationToken);
        await VentaApiRespuestas.AsegurarExitoAsync(response, "la configuración de numeración", logger, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<ConfiguracionNumeracionResponse>>(cancellationToken) ?? [];
    }

    public async Task<VentaOperationResult<ConfiguracionNumeracionResponse>> UpdateAsync(
        TipoDocumentoSerie tipo, Guid serieId, long xmin, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"{Ruta}/{(short)tipo}", new UpdateBody(serieId, xmin), cancellationToken);
        return await VentaApiRespuestas.ToResultAsync<ConfiguracionNumeracionResponse>(
            response, "Configuración guardada.", "No hay configuración para ese tipo de documento.", "configuración de numeración", logger,
            cancellationToken);
    }

    // Cuerpo de red exacto de ConfiguracionNumeracionController.
    private sealed record UpdateBody(Guid SerieId, long Xmin);
}
