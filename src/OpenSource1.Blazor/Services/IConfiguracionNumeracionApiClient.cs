using OpenSource1.Application.Features.ConfiguracionNumeracion.Dtos;
using OpenSource1.Core.Enums;

namespace OpenSource1.Blazor.Services;

/// <summary>
/// Cliente tipado de <c>api/configuracion/numeracion</c> (spec no-series): la serie predeterminada de cada tipo de documento.
/// Consultar = CanConsult; cambiar = CanAdministrar en la API (con <c>xmin</c>).
/// </summary>
public interface IConfiguracionNumeracionApiClient
{
    /// <summary>Una fila por tipo de documento. Un error de la API lanza <see cref="HttpRequestException"/>.</summary>
    Task<IReadOnlyList<ConfiguracionNumeracionResponse>> ListAsync(CancellationToken cancellationToken = default);

    Task<VentaOperationResult<ConfiguracionNumeracionResponse>> UpdateAsync(
        TipoDocumentoSerie tipo, Guid serieId, long xmin, CancellationToken cancellationToken = default);
}
