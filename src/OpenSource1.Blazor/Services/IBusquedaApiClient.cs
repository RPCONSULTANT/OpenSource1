using OpenSource1.Application.Features.Busqueda.Dtos;

namespace OpenSource1.Blazor.Services;

/// <summary>
/// Cliente tipado de <c>GET api/busqueda</c> (Fix-Features A3). Un 400 trae los mensajes reales de la API en
/// <see cref="ConsultaResultado{T}.Errors"/>; un fallo de red se propaga como excepción (la página lo captura).
/// </summary>
public interface IBusquedaApiClient
{
    Task<ConsultaResultado<BusquedaGlobalResponse>> BuscarAsync(string q, int limite = 5, CancellationToken cancellationToken = default);
}
