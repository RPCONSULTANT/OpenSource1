namespace OpenSource1.Application.Features.Productos;

public sealed record ProductoSearchCriteria(
    string? Codigo,
    string? Nombre,
    string? Categoria,
    string? Precio,
    string? Stock);
