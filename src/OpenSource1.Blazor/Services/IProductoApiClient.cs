using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Blazor.Services;

public interface IProductoApiClient
{
    Task<PagedResult<ProductoResponse>> ListAsync(ProductoSearchFilter? filter = null, PageRequest? paginacion = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recupera todos los productos que coincidan con el filtro, paginando internamente contra la
    /// API con el tamaño de página máximo. Uso reservado a consumidores que necesitan el conjunto
    /// completo (reportes crudos, paneles con agregados) y no a los listados paginados.
    /// </summary>
    Task<IReadOnlyList<ProductoResponse>> ListAllAsync(ProductoSearchFilter? filter = null, CancellationToken cancellationToken = default);

    Task<ProductoResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductoOperationResult> CreateAsync(ProductoInput input, CancellationToken cancellationToken = default);
    Task<ProductoOperationResult> UpdateAsync(Guid id, ProductoInput input, CancellationToken cancellationToken = default);
    Task<ProductoOperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record ProductoSearchFilter(
    string? Codigo,
    string? Nombre,
    string? CategoriaCodigo,
    string? CategoriaNombre,
    string? UnidadMedidaCodigo,
    string? UnidadMedidaNombre,
    string? PrecioVenta,
    string? Stock);

/// <summary>
/// Cuerpo de alta/modificación hacia la API. En el alta <c>CategoriaId</c>/<c>UnidadMedidaBaseId</c> nulos significan "por defecto"
/// (categoría GENERAL, unidad UND). En la modificación la API tiene semántica parcial para <c>PrecioVenta</c>, <c>Stock</c>,
/// <c>CategoriaId</c>, <c>UnidadMedidaBaseId</c>, <c>MetodoCosteo</c>, <c>CostoEstandar</c> y <c>Bloqueado</c> (null = conservar); el
/// formulario de edición muestra siempre el valor completo, así que los envía todos. <c>Nombre</c>, <c>Codigo</c> e <c>ImagePath</c> son de
/// reemplazo completo. <c>CostoUnitario</c> y <c>CostoAjustado</c> no viajan: los mantiene el sistema.
/// </summary>
public sealed record ProductoInput(
    string Codigo,
    string Nombre,
    decimal PrecioVenta,
    int Stock,
    Guid? CategoriaId,
    Guid? UnidadMedidaBaseId,
    MetodoCosteo MetodoCosteo = MetodoCosteo.Promedio,
    decimal CostoEstandar = 0m,
    BloqueoProducto Bloqueado = BloqueoProducto.Ninguno,
    string? ImagePath = null);

/// <summary>
/// Resultado de una operación. <c>Message</c> es el mensaje resumido; <c>Errors</c> trae los mensajes reales que devolvió la API
/// (400/409/422) para mostrarlos tal cual en la UI.
/// </summary>
public sealed record ProductoOperationResult(bool Succeeded, string Message, Guid? EntityId = null, IReadOnlyList<string>? Errors = null);
