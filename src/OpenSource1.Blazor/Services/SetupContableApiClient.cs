using System.Net;
using System.Net.Http.Json;
using OpenSource1.Application.Features.SetupsContables;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Blazor.Services;

/// <summary>
/// Cliente de <c>api/setups-contables/{general|iva|inventario}</c> (Task 5.4). Genérico por tipo: <typeparamref name="T"/> es el
/// DTO del setup (<c>SetupGeneralResponse</c>, <c>SetupIvaResponse</c>, <c>SetupInventarioResponse</c>) y el cuerpo de alta o
/// modificación lo arma la página con los nombres de la API. Los 400/409 devuelven los mensajes reales de la API.
/// </summary>
public interface ISetupContableApiClient
{
    Task<PagedResult<T>> ListAsync<T>(TipoSetupContable tipo, Guid? secundarioId, Guid? principalId, PageRequest paginacion, CancellationToken cancellationToken = default);
    Task<T?> GetByIdAsync<T>(TipoSetupContable tipo, Guid id, CancellationToken cancellationToken = default) where T : class;
    Task<GrupoOperationResult> CreateAsync(TipoSetupContable tipo, object cuerpo, CancellationToken cancellationToken = default);
    Task<GrupoOperationResult> UpdateAsync(TipoSetupContable tipo, Guid id, object cuerpo, CancellationToken cancellationToken = default);
    Task<GrupoOperationResult> DeleteAsync(TipoSetupContable tipo, Guid id, CancellationToken cancellationToken = default);
}

public sealed class SetupContableApiClient(HttpClient httpClient, ILogger<SetupContableApiClient> logger) : ISetupContableApiClient
{
    private const string Entidad = "setup contable";

    private static string Ruta(TipoSetupContable tipo) => $"api/setups-contables/{TiposSetupContable.De(tipo).Ruta}";

    public async Task<PagedResult<T>> ListAsync<T>(
        TipoSetupContable tipo, Guid? secundarioId, Guid? principalId, PageRequest paginacion, CancellationToken cancellationToken = default)
    {
        var descriptor = TiposSetupContable.De(tipo);
        var parameters = new List<string>();
        if (secundarioId is { } secundario) parameters.Add($"{descriptor.ParametroSecundario}={secundario}");
        if (principalId is { } principal) parameters.Add($"{descriptor.ParametroPrincipal}={principal}");

        using var response = await httpClient.GetAsync(ApiRespuestas.ConPaginacion(Ruta(tipo), parameters, paginacion), cancellationToken);
        await AsegurarExitoAsync(response, "obtener los setups contables", cancellationToken);
        return await response.Content.ReadFromJsonAsync<PagedResult<T>>(cancellationToken) ?? PagedResult<T>.Vacio(paginacion);
    }

    public async Task<T?> GetByIdAsync<T>(TipoSetupContable tipo, Guid id, CancellationToken cancellationToken = default) where T : class
    {
        using var response = await httpClient.GetAsync($"{Ruta(tipo)}/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await AsegurarExitoAsync(response, "obtener el setup contable", cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }

    public async Task<GrupoOperationResult> CreateAsync(TipoSetupContable tipo, object cuerpo, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(Ruta(tipo), cuerpo, cancellationToken);
        return await ApiRespuestas.ToResultAsync(response, "Setup contable agregado correctamente.", Entidad, logger, cancellationToken);
    }

    public async Task<GrupoOperationResult> UpdateAsync(TipoSetupContable tipo, Guid id, object cuerpo, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"{Ruta(tipo)}/{id}", cuerpo, cancellationToken);
        return await ApiRespuestas.ToResultAsync(response, "Setup contable modificado correctamente.", Entidad, logger, cancellationToken);
    }

    public async Task<GrupoOperationResult> DeleteAsync(TipoSetupContable tipo, Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"{Ruta(tipo)}/{id}", cancellationToken);
        return await ApiRespuestas.ToResultAsync(response, "Setup contable eliminado correctamente.", Entidad, logger, cancellationToken);
    }

    private async Task AsegurarExitoAsync(HttpResponseMessage response, string operacion, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("SetupsContables ({Operacion}) returned {StatusCode}. Body: {Body}", operacion, response.StatusCode, body);
            throw new HttpRequestException($"El servidor devolvió {(int)response.StatusCode} al {operacion}.", inner: null, statusCode: response.StatusCode);
        }
    }
}
