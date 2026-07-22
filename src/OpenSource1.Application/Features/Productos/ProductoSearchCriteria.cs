namespace OpenSource1.Application.Features.Productos;

public sealed record ProductoSearchCriteria(
    string? Codigo,
    string? Nombre,
    string? CategoriaCodigo,
    string? CategoriaNombre,
    string? UnidadMedidaCodigo,
    string? UnidadMedidaNombre,
    string? Precio,
    string? Stock);
