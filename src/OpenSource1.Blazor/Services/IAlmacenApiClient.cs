using OpenSource1.Application.Features.Almacenes.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Blazor.Services;

public interface IAlmacenApiClient
{
    Task<PagedResult<AlmacenResponse>> ListAsync(AlmacenSearchFilter? filter = null, PageRequest? paginacion = null, CancellationToken cancellationToken = default);

    Task<AlmacenResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AlmacenOperationResult> CreateAsync(AlmacenInput input, CancellationToken cancellationToken = default);
    Task<AlmacenOperationResult> UpdateAsync(Guid id, AlmacenInput input, CancellationToken cancellationToken = default);
    Task<AlmacenOperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record AlmacenSearchFilter(string? Codigo, string? Nombre, bool? Bloqueado);

/// <summary>
/// Cuerpo de alta/modificación hacia la API. <c>Codigo</c> y <c>Nombre</c> son de reemplazo
/// completo, igual que en la API. Los demás campos también viajan siempre desde este formulario de
/// un solo paso (no hay "conservar sin tocar" en la UI: siempre muestra y reenvía el valor
/// completo, incluida cadena vacía para limpiar una dirección/ciudad/país opcional — ver
/// <c>AlmacenValidator</c>/<c>Normalizar</c> en el handler). <c>Bloqueado</c> y
/// <c>EsPredeterminado</c> no son opcionales aquí: el &lt;select&gt; de la UI siempre elige uno de
/// los dos valores, así que la API los recibe siempre explícitos (nunca ausentes/null).
/// </summary>
public sealed record AlmacenInput(
    string Codigo,
    string Nombre,
    string? DireccionLinea1,
    string? DireccionLinea2,
    string? Ciudad,
    string? PaisCodigo,
    bool Bloqueado,
    bool EsPredeterminado);

/// <summary>
/// Resultado de una operación. <c>Message</c> es el mensaje resumido; <c>Errors</c> trae los
/// mensajes reales que devolvió la API (400/409) para mostrarlos tal cual en la UI.
/// </summary>
public sealed record AlmacenOperationResult(
    bool Succeeded, string Message, Guid? EntityId = null, IReadOnlyList<string>? Errors = null);
