using System.Globalization;
using OpenSource1.Application.Features.Busqueda.Dtos;

namespace OpenSource1.Blazor.Services;

public sealed class BusquedaApiClient(HttpClient httpClient, ILogger<BusquedaApiClient> logger) : IBusquedaApiClient
{
    public Task<ConsultaResultado<BusquedaGlobalResponse>> BuscarAsync(string q, int limite = 5, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(q);
        var url = $"api/busqueda?q={Uri.EscapeDataString(q.Trim())}&limite={limite.ToString(CultureInfo.InvariantCulture)}";
        return ConsultasApi.GetAsync<BusquedaGlobalResponse>(
            httpClient, url, "los resultados de la búsqueda", "No tiene permisos para buscar registros.", logger,
            cancellationToken: cancellationToken);
    }
}
