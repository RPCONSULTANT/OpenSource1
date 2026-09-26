using System.Net;
using System.Net.Http.Json;
using OpenSource1.Application.Services.Contabilidad;

namespace OpenSource1.Blazor.Services;

public sealed class ContabilidadApiClient(HttpClient httpClient, ILogger<ContabilidadApiClient> logger) : IContabilidadApiClient
{
    private const string BaseRoute = "api/contabilidad";

    public async Task<PosteoCostoOperationResult> PostearCostoInventarioAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync($"{BaseRoute}/postear-costo-inventario", content: null, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadFromJsonAsync<ResultadoPosteoCostoInventario>(cancellationToken);
            return new PosteoCostoOperationResult(true, "Costo de inventario contabilizado.", payload);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("Postear costo de inventario returned {StatusCode}. Body: {Body}", response.StatusCode, body);

        // Mensajes reales de la API en 400/409 (mismo criterio que el resto de clientes); genérico por estado en lo demás.
        var mensajes = response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict ? ApiRespuestas.ExtraerMensajes(body) : [];
        var safe = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Debe iniciar sesión nuevamente.",
            HttpStatusCode.Forbidden => "No tiene permisos para contabilizar el costo de inventario.",
            _ when mensajes.Count > 0 => string.Join(" ", mensajes),
            _ => "No fue posible contabilizar el costo de inventario."
        };
        return new PosteoCostoOperationResult(false, safe, Errors: mensajes);
    }
}
