using OpenSource1.Application.Features.CategoriasProducto.Dtos;
using OpenSource1.Application.Features.UnidadesMedida.Dtos;
using OpenSource1.Blazor.Services;

namespace OpenSource1.Blazor.Components;

/// <summary>Categorías y unidades de medida elegibles para los <c>&lt;select&gt;</c> del formulario de producto y si su carga fue fiable.</summary>
public sealed record ProductoOpciones(
    IReadOnlyList<CategoriaProductoResponse> Categorias,
    IReadOnlyList<UnidadMedidaResponse> Unidades,
    bool CargaFallida)
{
    /// <summary>Tiempo máximo de la carga: un listado colgado no debe bloquear la página los 100 s del HttpClient.</summary>
    public static readonly TimeSpan TiempoMaximo = TimeSpan.FromSeconds(5);

    public static readonly ProductoOpciones SinCargar = new([], [], false);

    public const string MensajeNoDisponibles =
        "No fue posible cargar las categorías y unidades de medida. Por seguridad no se puede guardar la modificación hasta que carguen (la categoría y la unidad actuales no se tocarían); recargue la página.";

    public const string MensajeNoDisponiblesAlta =
        "No fue posible cargar las categorías y unidades de medida: puede guardar el producto con la categoría General y la unidad Unidad, y cambiarlas después.";

    /// <summary>
    /// Carga todas las categorías y unidades (todas las páginas) con un tiempo máximo COMÚN. Cualquier fallo (excepción, error HTTP,
    /// cuerpo cortado, tiempo agotado) marca <see cref="CargaFallida"/> en vez de propagarse.
    /// </summary>
    public static async Task<ProductoOpciones> CargarAsync(
        ICategoriaProductoApiClient categorias, IUnidadMedidaApiClient unidades, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TiempoMaximo);

        try
        {
            var tareaCategorias = categorias.ListAllAsync(cts.Token);
            var tareaUnidades = unidades.ListAllAsync(cts.Token);
            await Task.WhenAll(tareaCategorias, tareaUnidades);
            return new ProductoOpciones(await tareaCategorias, await tareaUnidades, false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load categorías/unidades de medida options from API.");
            return new ProductoOpciones([], [], true);
        }
    }

    /// <summary>
    /// ¿Se puede guardar una MODIFICACIÓN? No si la carga falló, ni si alguna lista vino vacía (respuesta incoherente: todo producto tiene
    /// categoría y unidad, así que un catálogo vacío significa que el selector no reflejaría la realidad).
    /// </summary>
    public bool ModificacionBloqueada => CargaFallida || Categorias.Count == 0 || Unidades.Count == 0;
}
