using System.Net;
using System.Net.Http.Json;
using OpenSource1.Application.Features.GruposClienteContable.Dtos;
using OpenSource1.Application.Features.GruposContables;
using OpenSource1.Application.Features.GruposContables.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Blazor.Services;

public sealed class GrupoContableApiClient(HttpClient httpClient, ILogger<GrupoContableApiClient> logger) : IGrupoContableApiClient
{
    private static string Ruta(TipoGrupoContable tipo) => $"api/grupos-contables/{TiposGrupoContable.De(tipo).Ruta}";

    public async Task<PagedResult<GrupoContableResponse>> ListAsync(
        TipoGrupoContable tipo, GrupoContableSearchFilter? filter = null, PageRequest? paginacion = null, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            ApiRespuestas.ConPaginacion(Ruta(tipo), ApiRespuestas.Filtros(filter), paginacion), cancellationToken);
        await AsegurarExitoAsync(response, "obtener los grupos contables", cancellationToken);
        return await response.Content.ReadFromJsonAsync<PagedResult<GrupoContableResponse>>(cancellationToken)
            ?? PagedResult<GrupoContableResponse>.Vacio(paginacion ?? new PageRequest());
    }

    public async Task<IReadOnlyList<GrupoContableResponse>> ListAllAsync(TipoGrupoContable tipo, CancellationToken cancellationToken = default)
    {
        var todos = new List<GrupoContableResponse>();
        for (var pagina = 1; ; pagina++)
        {
            var resultado = await ListAsync(tipo, null, new PageRequest(pagina, PageRequest.TamanoMaximo, "Codigo", Descendente: false), cancellationToken);
            todos.AddRange(resultado.Items);
            if (resultado.Items.Count == 0 || pagina >= resultado.TotalPaginas)
            {
                return todos;
            }
        }
    }

    public async Task<GrupoContableResponse?> GetByIdAsync(TipoGrupoContable tipo, Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"{Ruta(tipo)}/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await AsegurarExitoAsync(response, "obtener el grupo contable", cancellationToken);
        return await response.Content.ReadFromJsonAsync<GrupoContableResponse>(cancellationToken);
    }

    public async Task<GrupoOperationResult> CreateAsync(TipoGrupoContable tipo, GrupoContableInput input, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(Ruta(tipo), input, cancellationToken);
        return await ApiRespuestas.ToResultAsync(response, "Grupo contable agregado correctamente.", "grupo contable", logger, cancellationToken);
    }

    public async Task<GrupoOperationResult> UpdateAsync(TipoGrupoContable tipo, Guid id, GrupoContableInput input, long xmin, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"{Ruta(tipo)}/{id}", new { input.Codigo, input.Descripcion, Xmin = xmin }, cancellationToken);
        return await ApiRespuestas.ToResultAsync(response, "Grupo contable modificado correctamente.", "grupo contable", logger, cancellationToken);
    }

    public async Task<GrupoOperationResult> DeleteAsync(TipoGrupoContable tipo, Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"{Ruta(tipo)}/{id}", cancellationToken);
        return await ApiRespuestas.ToResultAsync(response, "Grupo contable eliminado correctamente.", "grupo contable", logger, cancellationToken);
    }

    private async Task AsegurarExitoAsync(HttpResponseMessage response, string operacion, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("GruposContables ({Operacion}) returned {StatusCode}. Body: {Body}", operacion, response.StatusCode, body);
            throw new HttpRequestException($"El servidor devolvió {(int)response.StatusCode} al {operacion}.", inner: null, statusCode: response.StatusCode);
        }
    }
}

public sealed class GrupoClienteContableApiClient(HttpClient httpClient, ILogger<GrupoClienteContableApiClient> logger) : IGrupoClienteContableApiClient
{
    private const string Ruta = "api/grupos-cliente-contable";

    public async Task<PagedResult<GrupoClienteContableResponse>> ListAsync(
        GrupoContableSearchFilter? filter = null, PageRequest? paginacion = null, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(ApiRespuestas.ConPaginacion(Ruta, ApiRespuestas.Filtros(filter), paginacion), cancellationToken);
        await AsegurarExitoAsync(response, "obtener los grupos contables de cliente", cancellationToken);
        return await response.Content.ReadFromJsonAsync<PagedResult<GrupoClienteContableResponse>>(cancellationToken)
            ?? PagedResult<GrupoClienteContableResponse>.Vacio(paginacion ?? new PageRequest());
    }

    public async Task<IReadOnlyList<GrupoClienteContableResponse>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var todos = new List<GrupoClienteContableResponse>();
        for (var pagina = 1; ; pagina++)
        {
            var resultado = await ListAsync(null, new PageRequest(pagina, PageRequest.TamanoMaximo, "Codigo", Descendente: false), cancellationToken);
            todos.AddRange(resultado.Items);
            if (resultado.Items.Count == 0 || pagina >= resultado.TotalPaginas)
            {
                return todos;
            }
        }
    }

    public async Task<GrupoClienteContableResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"{Ruta}/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await AsegurarExitoAsync(response, "obtener el grupo contable de cliente", cancellationToken);
        return await response.Content.ReadFromJsonAsync<GrupoClienteContableResponse>(cancellationToken);
    }

    public async Task<GrupoOperationResult> CreateAsync(GrupoClienteContableInput input, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(Ruta, input, cancellationToken);
        return await ApiRespuestas.ToResultAsync(response, "Grupo contable de cliente agregado correctamente.", "grupo contable de cliente", logger, cancellationToken);
    }

    public async Task<GrupoOperationResult> UpdateAsync(Guid id, GrupoClienteContableInput input, long xmin, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            input.Codigo, input.Descripcion, input.CuentaCxCId,
            // Vacío = quitar (Guid.Empty); nunca null (= conservar): el formulario siempre muestra el valor completo.
            CuentaDescuentoId = input.CuentaDescuentoId ?? Guid.Empty,
            CuentaInteresId = input.CuentaInteresId ?? Guid.Empty,
            Xmin = xmin
        };
        using var response = await httpClient.PutAsJsonAsync($"{Ruta}/{id}", body, cancellationToken);
        return await ApiRespuestas.ToResultAsync(response, "Grupo contable de cliente modificado correctamente.", "grupo contable de cliente", logger, cancellationToken);
    }

    public async Task<GrupoOperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"{Ruta}/{id}", cancellationToken);
        return await ApiRespuestas.ToResultAsync(response, "Grupo contable de cliente eliminado correctamente.", "grupo contable de cliente", logger, cancellationToken);
    }

    private async Task AsegurarExitoAsync(HttpResponseMessage response, string operacion, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("GruposClienteContable ({Operacion}) returned {StatusCode}. Body: {Body}", operacion, response.StatusCode, body);
            throw new HttpRequestException($"El servidor devolvió {(int)response.StatusCode} al {operacion}.", inner: null, statusCode: response.StatusCode);
        }
    }
}
