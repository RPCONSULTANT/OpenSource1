using System.Globalization;
using OpenSource1.Application.Features.Contabilidad;
using OpenSource1.Application.Features.Contabilidad.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Blazor.Services;

public sealed class ContabilidadConsultasApiClient(HttpClient httpClient, ILogger<ContabilidadConsultasApiClient> logger)
    : IContabilidadConsultasApiClient
{
    private const string SinPermiso = "No tiene permisos para consultar el libro contable.";

    public Task<ConsultaResultado<PagedResult<MovimientoContableResponse>>> ListMovimientosAsync(
        MovimientoContableSearchCriteria criterios, PageRequest paginacion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criterios);
        var parameters = new List<string>();
        if (criterios.CuentaContableId is { } cuenta) parameters.Add($"cuentaId={cuenta}");
        if (criterios.Desde is { } desde) parameters.Add($"desde={Fecha(desde)}");
        if (criterios.Hasta is { } hasta) parameters.Add($"hasta={Fecha(hasta)}");
        if (criterios.TipoDocumento is { } tipo) parameters.Add($"tipoDocumento={tipo.ToString(CultureInfo.InvariantCulture)}");
        if (!string.IsNullOrWhiteSpace(criterios.NumeroDocumento)) parameters.Add($"numeroDocumento={Uri.EscapeDataString(criterios.NumeroDocumento.Trim())}");
        if (criterios.SocioNegocioId is { } socio) parameters.Add($"socioId={socio}");
        if (criterios.RegistroContableId is { } registro) parameters.Add($"registroId={registro.ToString(CultureInfo.InvariantCulture)}");

        return ConsultasApi.GetAsync<PagedResult<MovimientoContableResponse>>(
            httpClient, ApiRespuestas.ConPaginacion("api/contabilidad/movimientos", parameters, paginacion),
            "los movimientos contables", SinPermiso, logger, cancellationToken: cancellationToken);
    }

    public Task<ConsultaResultado<BalanceComprobacionResponse>> GetBalanceComprobacionAsync(
        BalanceComprobacionCriterios criterios, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criterios);
        var parameters = new List<string>();
        if (criterios.Desde is { } desde) parameters.Add($"desde={Fecha(desde)}");
        if (criterios.Hasta is { } hasta) parameters.Add($"hasta={Fecha(hasta)}");

        return ConsultasApi.GetAsync<BalanceComprobacionResponse>(
            httpClient, ApiRespuestas.ConPaginacion("api/contabilidad/balance-comprobacion", parameters, null),
            "el balance de comprobación", SinPermiso, logger, cancellationToken: cancellationToken);
    }

    private static string Fecha(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
