using OpenSource1.Application.Features.Inventario.Consultas;
using OpenSource1.Application.Features.Inventario.Consultas.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Blazor.Services;

/// <summary>
/// Cliente tipado de las vistas de inventario (Task 7.2): <c>GET api/inventario/movimientos-producto</c>,
/// <c>/movimientos-valor</c> y <c>/existencias</c>. Un 400 devuelve los mensajes reales de la API (fechas invertidas, tipo no
/// válido…) en <see cref="ConsultaResultado{T}.Errors"/>; los demás estados, un mensaje genérico. Los fallos de red se propagan
/// como excepción (la página los captura y responde igualmente con un aviso).
/// </summary>
public interface IInventarioConsultasApiClient
{
    Task<ConsultaResultado<PagedResult<MovimientoProductoVistaResponse>>> ListMovimientosProductoAsync(
        MovimientoProductoVistaCriterios criterios, PageRequest paginacion, CancellationToken cancellationToken = default);

    Task<ConsultaResultado<PagedResult<MovimientoValorVistaResponse>>> ListMovimientosValorAsync(
        MovimientoValorVistaCriterios criterios, PageRequest paginacion, CancellationToken cancellationToken = default);

    Task<ConsultaResultado<ExistenciasVistaResponse>> ListExistenciasAsync(
        ExistenciaVistaCriterios criterios, PageRequest paginacion, CancellationToken cancellationToken = default);
}

/// <summary>Resultado de una consulta: el valor si tuvo éxito; si no, el mensaje (y en un 400, los mensajes reales de la API).</summary>
public sealed record ConsultaResultado<T>(bool Succeeded, string Message, T? Valor = default, IReadOnlyList<string>? Errors = null);
