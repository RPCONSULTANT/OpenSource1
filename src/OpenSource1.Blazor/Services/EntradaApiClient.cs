using System.Net;
using System.Net.Http.Json;
using OpenSource1.Application.Features.Entradas.Dtos;
using System;

namespace OpenSource1.Blazor.Services;

[Obsolete("Modulo de prueba obsoleto. No usar Entradas para nuevos desarrollos.")]
public sealed class EntradaApiClient(HttpClient httpClient, ILogger<EntradaApiClient> logger) : IEntradaApiClient
{
    public async Task<IReadOnlyList<EntradaResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("api/entradas", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Entradas LIST returned {StatusCode}. Body: {Body}", response.StatusCode, body);
            throw new HttpRequestException(
                $"El servidor devolvió {(int)response.StatusCode} al obtener las entradas.",
                inner: null,
                statusCode: response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<EntradaResponse>>(cancellationToken) ?? [];
    }

    public async Task<EntradaOperationResult> CreateAsync(EntradaInput input, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("api/entradas", input, cancellationToken);
        return await ToResultAsync(response, "Entrada registrada correctamente.", cancellationToken);
    }

    public async Task<EntradaOperationResult> UpdateAsync(Guid id, EntradaInput input, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"api/entradas/{id}", input, cancellationToken);
        return await ToResultAsync(response, "Entrada modificada correctamente.", cancellationToken);
    }

    public async Task<EntradaOperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"api/entradas/{id}", cancellationToken);
        return await ToResultAsync(response, "Entrada eliminada correctamente.", cancellationToken);
    }

    private async Task<EntradaOperationResult> ToResultAsync(
        HttpResponseMessage response, string successMessage, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return new EntradaOperationResult(true, successMessage);

        var safe = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Debe iniciar sesión nuevamente.",
            HttpStatusCode.Forbidden    => "No tiene permisos para realizar esta operación.",
            HttpStatusCode.NotFound     => "No se encontró la entrada indicada.",
            HttpStatusCode.BadRequest   => "Revise los datos del formulario.",
            _                           => "No fue posible completar la operación."
        };

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("Entradas API returned {StatusCode}. Body: {Body}", response.StatusCode, body);
        return new EntradaOperationResult(false, safe);
    }
}
