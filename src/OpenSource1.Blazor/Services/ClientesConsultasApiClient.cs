using System.Globalization;
using OpenSource1.Application.Features.MovimientosCliente;
using OpenSource1.Application.Features.MovimientosCliente.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Blazor.Services;

public sealed class ClientesConsultasApiClient(HttpClient httpClient, ILogger<ClientesConsultasApiClient> logger)
    : IClientesConsultasApiClient
{
    private const string SinPermiso = "No tiene permisos para consultar el libro de clientes.";

    public Task<ConsultaResultado<PagedResult<MovimientoClienteResponse>>> ListMovimientosAsync(
        MovimientoClienteSearchCriteria criterios, PageRequest paginacion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criterios);
        var parameters = new List<string>();
        if (criterios.Desde is { } desde) parameters.Add($"desde={Fecha(desde)}");
        if (criterios.Hasta is { } hasta) parameters.Add($"hasta={Fecha(hasta)}");
        if (criterios.SoloAbiertos is { } abiertos) parameters.Add($"soloAbiertos={(abiertos ? "true" : "false")}");
        if (criterios.TipoDocumento is { } tipo) parameters.Add($"tipoDocumento={tipo.ToString(CultureInfo.InvariantCulture)}");
        if (criterios.FechaCorte is { } corte) parameters.Add($"fechaCorte={Fecha(corte)}");

        return ConsultasApi.GetAsync<PagedResult<MovimientoClienteResponse>>(
            httpClient, ApiRespuestas.ConPaginacion($"api/clientes/{criterios.SocioNegocioId}/movimientos", parameters, paginacion),
            "los movimientos del cliente", SinPermiso, logger, "No se encontró el cliente indicado.", cancellationToken);
    }

    public Task<ConsultaResultado<EstadoCuentaResponse>> GetEstadoCuentaAsync(
        EstadoCuentaCriterios criterios, PageRequest paginacion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criterios);
        var parameters = new List<string>();
        if (criterios.FechaCorte is { } corte) parameters.Add($"fechaCorte={Fecha(corte)}");
        if (criterios.SocioNegocioId is { } socio) parameters.Add($"socioId={socio}");
        if (!string.IsNullOrWhiteSpace(criterios.Texto)) parameters.Add($"texto={Uri.EscapeDataString(criterios.Texto.Trim())}");
        if (criterios.SoloConSaldo) parameters.Add("soloConSaldo=true");

        return ConsultasApi.GetAsync<EstadoCuentaResponse>(
            httpClient, ApiRespuestas.ConPaginacion("api/clientes/estado-cuenta", parameters, paginacion),
            "el estado de cuenta", SinPermiso, logger, cancellationToken: cancellationToken);
    }

    private static string Fecha(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
