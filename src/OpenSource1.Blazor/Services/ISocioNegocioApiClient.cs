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
    string? Pais,
    string? Codigo = null,
    TipoSocioNegocio? Tipo = null,
    string? NumeroDocumentoFiscal = null);

/// <summary>
/// Cuerpo de alta/modificación hacia la API. En el alta, <c>RazonSocial</c>/<c>NumeroDocumentoFiscal</c>/
/// <c>Ciudad</c> nulos o vacíos significan "sin dato" y <c>TerminoPagoId</c> nulo "sin término". En la
/// modificación la API tiene semántica parcial para estos campos: cadena vacía = limpiar, y
/// <see cref="Guid.Empty"/> en <c>TerminoPagoId</c> = limpiar el término (null = conservar). Por eso
/// el formulario de edición envía <c>""</c> y <c>Guid.Empty</c> para vaciar y nunca null. <c>Email</c>,
/// <c>Telefono</c>, dirección, <c>Sector</c>, <c>PaisCodigo</c> e <c>ImagePath</c> son de reemplazo
/// completo: se envían siempre. El <c>Codigo</c> no viaja: lo asigna el sistema. Grupos contables (Task 5.3): null = sin grupo
/// en el alta y "conservar" en la modificación (no hay "limpiar"; la ficha los envía null si la API de grupos no cargó).
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
    BloqueoSocioNegocio Bloqueado = BloqueoSocioNegocio.Ninguno,
    Guid? GrupoNegocioId = null,
    Guid? GrupoIvaNegocioId = null,
    Guid? GrupoClienteContableId = null);

/// <summary>
/// Resultado de una operación. <c>Message</c> es el mensaje resumido; <c>Errors</c> trae los mensajes
/// reales que devolvió la API (400/409/422) para mostrarlos tal cual en la UI.
/// </summary>
public sealed record SocioNegocioOperationResult(
    bool Succeeded, string Message, Guid? EntityId = null, IReadOnlyList<string>? Errors = null);
