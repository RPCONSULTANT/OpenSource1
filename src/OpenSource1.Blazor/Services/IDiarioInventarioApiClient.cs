using OpenSource1.Application.Features.DiariosInventario.Lineas.Dtos;
using OpenSource1.Application.Features.DiariosInventario.Lotes;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Dtos;
using OpenSource1.Application.Features.DiariosInventario.Plantillas.Dtos;
using OpenSource1.Application.Features.DiariosInventario.Registros.Commands;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Blazor.Services;

/// <summary>
/// Cliente tipado de <c>api/diarios-inventario</c> (Task 4.4). Sigue el mismo patrón que
/// <see cref="IAlmacenApiClient"/>/<see cref="IProductoApiClient"/>: los 400/409/422 de la API
/// traen mensajes reales (texto seguro, en español) que <see cref="DiarioInventarioApiClient"/>
/// extrae tal cual para mostrarlos en la UI, sin inventar mensajes propios.
/// </summary>
public interface IDiarioInventarioApiClient
{
    /// <summary>Plantillas sembradas (sin CRUD): catálogo completo, siempre pocas filas.</summary>
    Task<IReadOnlyList<PlantillaDiarioResponse>> ListPlantillasAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<LoteDiarioResponse>> ListLotesAsync(LoteDiarioSearchCriteria? filter = null, PageRequest? paginacion = null, CancellationToken cancellationToken = default);

    Task<LoteDiarioResponse?> GetLoteByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<LoteDiarioOperationResult> CreateLoteAsync(LoteDiarioInput input, CancellationToken cancellationToken = default);

    /// <summary><paramref name="xmin"/> es el token de concurrencia optimista leído junto con el lote (409 si no coincide).</summary>
    Task<LoteDiarioOperationResult> UpdateLoteAsync(Guid id, LoteDiarioInput input, long xmin, CancellationToken cancellationToken = default);

    Task<DiarioOperationResult> DeleteLoteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Registra (postea) el lote. CanModify en la API (Administrador y Supervisor); un Ejecutor recibe 403.</summary>
    Task<RegistrarLoteOperationResult> RegistrarLoteAsync(Guid loteId, CancellationToken cancellationToken = default);

    /// <summary><see langword="null"/> = el lote no existe (404). Vacío = lote sin líneas capturadas.</summary>
    Task<IReadOnlyList<LineaDiarioResponse>?> ListLineasAsync(Guid loteId, CancellationToken cancellationToken = default);

    Task<LineaDiarioResponse?> GetLineaByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<LineaDiarioOperationResult> CreateLineaAsync(Guid loteId, LineaDiarioInput input, CancellationToken cancellationToken = default);

    /// <summary><paramref name="xmin"/> es el token de concurrencia optimista leído junto con la línea (409 si no coincide).</summary>
    Task<LineaDiarioOperationResult> UpdateLineaAsync(Guid id, LineaDiarioInput input, long xmin, CancellationToken cancellationToken = default);

    Task<DiarioOperationResult> DeleteLineaAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Cuerpo de alta/modificación de un lote. <c>SerieId</c> de la API NO viaja desde esta UI (siempre
/// se omite/deja "conservar"): el lote usa la serie de su plantilla. Elegir una serie distinta queda
/// fuera del alcance de la Task 4.4 (no hay un catálogo de series expuesto a Blazor todavía).
/// </summary>
public sealed record LoteDiarioInput(Guid PlantillaDiarioId, string Codigo, string Nombre, bool Bloqueado);

/// <summary>
/// Cuerpo de alta/modificación de una línea: reemplazo completo, igual que en la API (no hay
/// semántica "null = conservar" aquí: el formulario de un solo paso siempre reenvía todos los campos).
/// </summary>
public sealed record LineaDiarioInput(
    DateOnly FechaRegistro,
    DateOnly FechaDocumento,
    string? NumeroDocumento,
    TipoMovimientoInventario TipoMovimiento,
    Guid ProductoId,
    Guid AlmacenId,
    Guid? AlmacenDestinoId,
    Guid UnidadMedidaId,
    decimal Cantidad,
    decimal? CostoUnitario,
    string? Descripcion);

/// <summary>Resultado de una operación sin cuerpo de respuesta relevante (borrado). <c>Errors</c> son los mensajes reales de un 400.</summary>
public sealed record DiarioOperationResult(bool Succeeded, string Message, IReadOnlyList<string>? Errors = null);

public sealed record LoteDiarioOperationResult(bool Succeeded, string Message, LoteDiarioResponse? Valor = null, IReadOnlyList<string>? Errors = null);

public sealed record LineaDiarioOperationResult(bool Succeeded, string Message, LineaDiarioResponse? Valor = null, IReadOnlyList<string>? Errors = null);

public sealed record RegistrarLoteOperationResult(bool Succeeded, string Message, ResultadoRegistroLote? Valor = null, IReadOnlyList<string>? Errors = null);
