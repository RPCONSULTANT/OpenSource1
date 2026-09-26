using OpenSource1.Application.Features.CuentasContables.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Blazor.Services;

public interface ICuentaContableApiClient
{
    Task<PagedResult<CuentaContableResponse>> ListAsync(CuentaContableSearchFilter? filter = null, PageRequest? paginacion = null, CancellationToken cancellationToken = default);

    Task<CuentaContableResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CuentaContableOperationResult> CreateAsync(CuentaContableInput input, CancellationToken cancellationToken = default);
    Task<CuentaContableOperationResult> UpdateAsync(Guid id, CuentaContableInput input, long xmin, CancellationToken cancellationToken = default);
    Task<CuentaContableOperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record CuentaContableSearchFilter(
    string? Numero, string? Nombre, TipoCuentaContable? TipoCuenta, TipoResultadoCuenta? TipoResultado, bool? Bloqueada);

/// <summary>
/// Cuerpo de alta/modificación hacia la API. Todos los campos viajan siempre desde este formulario de
/// un solo paso (no hay "conservar sin tocar" en la UI: siempre muestra y reenvía el valor completo,
/// mismo patrón que AlmacenInput).
/// </summary>
public sealed record CuentaContableInput(
    string Numero,
    string Nombre,
    TipoCuentaContable TipoCuenta,
    TipoResultadoCuenta TipoResultado,
    bool PosteoDirecto,
    bool Bloqueada,
    int Sangria);

/// <summary>
/// Resultado de una operación. <c>Message</c> es el mensaje resumido; <c>Errors</c> trae los
/// mensajes reales que devolvió la API (400/409) para mostrarlos tal cual en la UI.
/// </summary>
public sealed record CuentaContableOperationResult(
    bool Succeeded, string Message, Guid? EntityId = null, IReadOnlyList<string>? Errors = null);
