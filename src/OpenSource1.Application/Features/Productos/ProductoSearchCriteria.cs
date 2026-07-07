namespace OpenSource1.Application.Features.Productos;

public sealed record ProductoSearchCriteria(
    Guid? Id,
    string? Codigo,
    string? Nombre,
    string? Categoria,
    decimal? Precio,
    int? Stock);
