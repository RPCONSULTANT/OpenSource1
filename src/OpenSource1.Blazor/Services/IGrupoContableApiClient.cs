using OpenSource1.Application.Features.GruposClienteContable.Dtos;
using OpenSource1.Application.Features.GruposContables.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Blazor.Services;

/// <summary>Cliente de <c>api/grupos-contables/{tipo}</c>: los cinco grupos contables simples (Task 5.3).</summary>
public interface IGrupoContableApiClient
{
    Task<PagedResult<GrupoContableResponse>> ListAsync(TipoGrupoContable tipo, GrupoContableSearchFilter? filter = null, PageRequest? paginacion = null, CancellationToken cancellationToken = default);

    /// <summary>Todos los grupos del tipo (todas las páginas, por código): opciones de los <c>&lt;select&gt;</c> de productos y socios.</summary>
    Task<IReadOnlyList<GrupoContableResponse>> ListAllAsync(TipoGrupoContable tipo, CancellationToken cancellationToken = default);

    Task<GrupoContableResponse?> GetByIdAsync(TipoGrupoContable tipo, Guid id, CancellationToken cancellationToken = default);
    Task<GrupoOperationResult> CreateAsync(TipoGrupoContable tipo, GrupoContableInput input, CancellationToken cancellationToken = default);
    Task<GrupoOperationResult> UpdateAsync(TipoGrupoContable tipo, Guid id, GrupoContableInput input, long xmin, CancellationToken cancellationToken = default);
    Task<GrupoOperationResult> DeleteAsync(TipoGrupoContable tipo, Guid id, CancellationToken cancellationToken = default);
}

/// <summary>Cliente de <c>api/grupos-cliente-contable</c> (Task 5.3).</summary>
public interface IGrupoClienteContableApiClient
{
    Task<PagedResult<GrupoClienteContableResponse>> ListAsync(GrupoContableSearchFilter? filter = null, PageRequest? paginacion = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GrupoClienteContableResponse>> ListAllAsync(CancellationToken cancellationToken = default);
    Task<GrupoClienteContableResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<GrupoOperationResult> CreateAsync(GrupoClienteContableInput input, CancellationToken cancellationToken = default);
    Task<GrupoOperationResult> UpdateAsync(Guid id, GrupoClienteContableInput input, long xmin, CancellationToken cancellationToken = default);
    Task<GrupoOperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record GrupoContableSearchFilter(string? Codigo, string? Descripcion);

public sealed record GrupoContableInput(string Codigo, string Descripcion);

/// <summary>
/// Cuerpo de alta/modificación de un grupo de cliente. El formulario muestra y reenvía siempre el valor completo: en la
/// modificación, una cuenta opcional vacía viaja como <see cref="Guid.Empty"/> (= quitarla), nunca como null (= conservar).
/// </summary>
public sealed record GrupoClienteContableInput(string Codigo, string Descripcion, Guid CuentaCxCId, Guid? CuentaDescuentoId, Guid? CuentaInteresId);

/// <summary>
/// Resultado de una operación. <c>Message</c> es el mensaje resumido; <c>Errors</c> trae los mensajes reales de la API
/// (400/409) para mostrarlos tal cual.
/// </summary>
public sealed record GrupoOperationResult(bool Succeeded, string Message, Guid? EntityId = null, IReadOnlyList<string>? Errors = null);
