using OpenSource1.Application.Features.MovimientosCliente;
using OpenSource1.Application.Features.MovimientosCliente.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Blazor.Services;

/// <summary>
/// Cliente tipado de las vistas de clientes (Task 7.3): <c>GET api/clientes/{id}/movimientos</c> con todos sus filtros y
/// <c>GET api/clientes/estado-cuenta</c>. Un 400 devuelve los mensajes reales de la API en
/// <see cref="ConsultaResultado{T}.Errors"/>; un 404 del socio de la ruta, "No se encontró el cliente indicado."
/// </summary>
public interface IClientesConsultasApiClient
{
    Task<ConsultaResultado<PagedResult<MovimientoClienteResponse>>> ListMovimientosAsync(
        MovimientoClienteSearchCriteria criterios, PageRequest paginacion, CancellationToken cancellationToken = default);

    Task<ConsultaResultado<EstadoCuentaResponse>> GetEstadoCuentaAsync(
        EstadoCuentaCriterios criterios, PageRequest paginacion, CancellationToken cancellationToken = default);
}
