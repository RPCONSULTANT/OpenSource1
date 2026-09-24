using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Blazor.Services;

public interface ISocioNegocioApiClient
{
    Task<PagedResult<SocioNegocioResponse>> ListAsync(SocioNegocioSearchFilter? filter = null, PageRequest? paginacion = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recupera todos los clientes que coincidan con el filtro, paginando internamente contra la
    /// API con el tamaño de página máximo. Uso reservado a consumidores que necesitan el conjunto
    /// completo (reportes crudos, paneles con agregados) y no a los listados paginados.
    /// </summary>
    Task<IReadOnlyList<SocioNegocioResponse>> ListAllAsync(SocioNegocioSearchFilter? filter = null, CancellationToken cancellationToken = default);

    Task<SocioNegocioResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SocioNegocioOperationResult> CreateAsync(SocioNegocioInput input, CancellationToken cancellationToken = default);
    Task<SocioNegocioOperationResult> UpdateAsync(Guid id, SocioNegocioInput input, CancellationToken cancellationToken = default);
    Task<SocioNegocioOperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record SocioNegocioSearchFilter(
    string? NombreComercial,
    string? Email,
    string? Telefono,
    string? Direccion,
    string? Sector,
    string? Pais);

/// <summary>
/// Cuerpo de alta/modificación hacia la API. La UI actual (Task 2.6) solo edita los campos
/// clásicos; el resto lleva los valores por defecto del alta (Cliente, SinDocumento, sin límite,
/// sin bloqueo) o, en una modificación, los valores ya guardados en el socio (ver
/// <c>ClienteDetail</c>) para no pisarlos. La UI de los campos nuevos llega con la Task 2.7.
/// </summary>
public sealed record SocioNegocioInput(
    string NombreComercial,
    string? Email,
    string? Telefono,
    string? DireccionLinea1,
    string? DireccionLinea2,
    string? Sector,
    string? PaisCodigo,
    string? ImagePath = null,
    string? RazonSocial = null,
    TipoSocioNegocio Tipo = TipoSocioNegocio.Cliente,
    TipoDocumentoFiscal TipoDocumentoFiscal = TipoDocumentoFiscal.SinDocumento,
    string? NumeroDocumentoFiscal = null,
    string? Ciudad = null,
    Guid? TerminoPagoId = null,
    decimal LimiteCredito = 0m,
    BloqueoSocioNegocio Bloqueado = BloqueoSocioNegocio.Ninguno);

public sealed record SocioNegocioOperationResult(bool Succeeded, string Message, Guid? EntityId = null);
