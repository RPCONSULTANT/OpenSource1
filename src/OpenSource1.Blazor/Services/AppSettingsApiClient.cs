using System.Net;
using System.Net.Http.Json;
using OpenSource1.Application.Features.AppSettings.Dtos;
using OpenSource1.Core.Common;
using System;

namespace OpenSource1.Blazor.Services;

[Obsolete("Modulo de prueba obsoleto. No usar AppSettings para nuevos desarrollos.")]
public sealed class AppSettingsApiClient(HttpClient httpClient, ILogger<AppSettingsApiClient> logger) : IAppSettingsApiClient
{
    public async Task<PagedResult<AppSettingResponse>> ListAsync(PageRequest? paginacion = null, CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<PagedResult<AppSettingResponse>>(BuildListUrl(paginacion), cancellationToken)
            ?? PagedResult<AppSettingResponse>.Vacio(paginacion ?? new PageRequest());

    private static string BuildListUrl(PageRequest? paginacion)
    {
        if (paginacion is null)
        {
            return "api/app-settings";
        }

        var parameters = new List<string>
        {
            $"pagina={paginacion.Pagina}",
            $"tamanoPagina={paginacion.TamanoPagina}",
            $"descendente={(paginacion.Descendente ? "true" : "false")}"
        };

        if (!string.IsNullOrWhiteSpace(paginacion.OrdenarPor))
        {
            parameters.Add($"ordenarPor={Uri.EscapeDataString(paginacion.OrdenarPor)}");
        }

        return $"api/app-settings?{string.Join("&", parameters)}";
    }

    public async Task<AppSettingOperationResult> CreateAsync(AppSettingInput input, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("api/app-settings", input, cancellationToken);
        return await ToOperationResultAsync(response, "Configuración agregada correctamente.", cancellationToken);
    }

    public async Task<AppSettingOperationResult> UpdateAsync(AppSettingInput input, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"api/app-settings/{Uri.EscapeDataString(input.Key ?? string.Empty)}", input, cancellationToken);
        return await ToOperationResultAsync(response, "Configuración modificada correctamente.", cancellationToken);
    }

    public async Task<AppSettingOperationResult> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"api/app-settings/{Uri.EscapeDataString(key)}", cancellationToken);
        return await ToOperationResultAsync(response, "Configuración eliminada correctamente.", cancellationToken);
    }

    private async Task<AppSettingOperationResult> ToOperationResultAsync(HttpResponseMessage response, string successMessage, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return new AppSettingOperationResult(true, successMessage);
        }

        var safeMessage = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Debe iniciar sesión nuevamente.",
            HttpStatusCode.Forbidden => "No tiene permisos para realizar esta operación.",
            HttpStatusCode.Conflict => "La configuración ya existe.",
            HttpStatusCode.NotFound => "No se encontró la configuración indicada.",
            HttpStatusCode.BadRequest => "Revise los datos del formulario.",
            _ => "No fue posible completar la operación."
        };

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("AppSettings API returned {StatusCode}. Body: {Body}", response.StatusCode, body);

        return new AppSettingOperationResult(false, safeMessage);
    }
}
