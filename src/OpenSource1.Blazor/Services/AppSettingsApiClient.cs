using System.Net;
using System.Net.Http.Json;
using OpenSource1.Application.Features.AppSettings.Dtos;

namespace OpenSource1.Blazor.Services;

public sealed class AppSettingsApiClient(HttpClient httpClient, ILogger<AppSettingsApiClient> logger) : IAppSettingsApiClient
{
    public async Task<IReadOnlyList<AppSettingResponse>> ListAsync(CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<IReadOnlyList<AppSettingResponse>>("api/app-settings", cancellationToken) ?? [];

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
