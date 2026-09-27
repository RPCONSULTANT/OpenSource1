using System.Net;
using System.Net.Http.Json;
using OpenSource1.Application.Features.FechasRegistro.Dtos;

namespace OpenSource1.Blazor.Services;

public sealed class FechasRegistroApiClient(HttpClient httpClient, ILogger<FechasRegistroApiClient> logger) : IFechasRegistroApiClient
{
    private const string Ruta = "api/configuracion/fechas-registro";
    private const string Entidad = "registro de fechas del usuario";

    public async Task<FechasRegistroGeneralResponse?> GetGeneralAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(Ruta, cancellationToken);
        await AsegurarExitoAsync(response, "la configuración general de fechas de registro", cancellationToken);
        return await response.Content.ReadFromJsonAsync<FechasRegistroGeneralResponse>(cancellationToken);
    }

    public async Task<GrupoOperationResult> UpdateGeneralAsync(FechasRegistroRangoInput input, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync(Ruta, input, cancellationToken);
        return await ApiRespuestas.ToResultAsync(response, "Fechas de registro generales guardadas.", "rango general", logger, cancellationToken);
    }

    public async Task<IReadOnlyList<FechasRegistroUsuarioResponse>> ListUsuariosAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"{Ruta}/usuarios", cancellationToken);
        await AsegurarExitoAsync(response, "las fechas de registro por usuario", cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<FechasRegistroUsuarioResponse>>(cancellationToken) ?? [];
    }

    public async Task<FechasRegistroUsuarioResponse?> GetUsuarioAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"{Ruta}/usuarios/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await AsegurarExitoAsync(response, "las fechas de registro del usuario", cancellationToken);
        return await response.Content.ReadFromJsonAsync<FechasRegistroUsuarioResponse>(cancellationToken);
    }

    public async Task<GrupoOperationResult> CreateUsuarioAsync(Guid usuarioId, FechasRegistroRangoInput input, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            $"{Ruta}/usuarios", new { usuarioId, input.PermitirRegistroDesde, input.PermitirRegistroHasta }, cancellationToken);
        return await ApiRespuestas.ToResultAsync(response, "Fechas de registro del usuario agregadas.", Entidad, logger, cancellationToken);
    }

    public async Task<GrupoOperationResult> UpdateUsuarioAsync(Guid id, FechasRegistroRangoInput input, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"{Ruta}/usuarios/{id}", input, cancellationToken);
        return await ApiRespuestas.ToResultAsync(response, "Fechas de registro del usuario modificadas.", Entidad, logger, cancellationToken);
    }

    public async Task<GrupoOperationResult> DeleteUsuarioAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"{Ruta}/usuarios/{id}", cancellationToken);
        return await ApiRespuestas.ToResultAsync(response, "Fechas de registro del usuario eliminadas.", Entidad, logger, cancellationToken);
    }

    private async Task AsegurarExitoAsync(HttpResponseMessage response, string que, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("FechasRegistro API returned {StatusCode}. Body: {Body}", response.StatusCode, body);
        throw new HttpRequestException(
            $"El servidor devolvió {(int)response.StatusCode} al obtener {que}.", inner: null, statusCode: response.StatusCode);
    }
}
