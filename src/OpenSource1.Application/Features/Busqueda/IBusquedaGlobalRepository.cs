using OpenSource1.Application.Features.Busqueda.Dtos;

namespace OpenSource1.Application.Features.Busqueda;

/// <summary>
/// Búsqueda global de solo lectura (Fix-Features A3): hasta <paramref name="limite"/> resultados por tipo, <c>ILIKE</c> con
/// los metacaracteres del término escapados, sin registros con borrado lógico.
/// </summary>
public interface IBusquedaGlobalRepository
{
    Task<BusquedaGlobalResponse> BuscarAsync(string termino, int limite, CancellationToken cancellationToken = default);
}
