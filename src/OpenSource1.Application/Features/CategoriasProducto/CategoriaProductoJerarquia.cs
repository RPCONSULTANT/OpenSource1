using OpenSource1.Application.Data.Repositories;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;

namespace OpenSource1.Application.Features.CategoriasProducto;

/// <summary>Reglas de la jerarquía de categorías, compartidas entre Create y Update.</summary>
internal static class CategoriaProductoJerarquia
{
    /// <summary>
    /// Valida que <paramref name="padreId"/> sea un padre válido para la categoría
    /// <paramref name="categoriaId"/> (<c>null</c> al crear): que exista y no esté borrada (el
    /// filtro global de EF oculta las borradas), que no sea la propia categoría y que no
    /// cierre un ciclo. Para detectar ciclos recorre la cadena de ancestros del padre; si
    /// alguno de ellos tiene como padre a la categoría que se edita, asignarle ese padre
    /// crearía un ciclo (A→B→A, A→B→C→A, etc.).
    /// </summary>
    /// <returns>El padre resuelto (<c>null</c> si no se pidió padre) o el error de validación.</returns>
    public static async Task<(Error? Error, CategoriaProducto? Padre)> ValidarPadreAsync(
        IGenericRepository<CategoriaProducto> repository,
        Guid? categoriaId,
        Guid? padreId,
        CancellationToken cancellationToken)
    {
        if (padreId is null)
        {
            return (null, null);
        }

        const string campo = "CategoriaPadreId";

        if (categoriaId is not null && categoriaId == padreId)
        {
            return (new Error(
                "categoria_producto.padre_propio", "Una categoría no puede ser su propio padre.", campo), null);
        }

        var padre = await repository.GetByIdAsync([padreId.Value], cancellationToken);

        if (padre is null)
        {
            return (new Error(
                "categoria_producto.padre_no_encontrado", "La categoría padre indicada no existe.", campo), null);
        }

        // Una categoría nueva no tiene descendientes, así que no puede cerrar un ciclo.
        if (categoriaId is null)
        {
            return (null, padre);
        }

        // "visitados" evita un bucle infinito si la base ya contuviera un ciclo ajeno a esta categoría.
        var visitados = new HashSet<Guid> { padre.Id };
        var actual = padre;

        while (actual.CategoriaPadreId is { } ancestroId)
        {
            if (ancestroId == categoriaId)
            {
                return (new Error(
                    "categoria_producto.ciclo_jerarquia",
                    "No se puede asignar ese padre: crearía un ciclo en la jerarquía de categorías.", campo), null);
            }

            if (!visitados.Add(ancestroId))
            {
                break;
            }

            var ancestro = await repository.GetByIdAsync([ancestroId], cancellationToken);
            if (ancestro is null)
            {
                break;
            }

            actual = ancestro;
        }

        return (null, padre);
    }
}
