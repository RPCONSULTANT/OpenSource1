namespace OpenSource1.Application.Features.CategoriasProducto.Dtos;

/// <summary>
/// DTO de lectura para CategoriaProducto. Usa propiedades init (no record posicional)
/// para que Dapper pueda poblar la instancia sin requerir coincidencia exacta
/// del constructor con los tipos del DataReader de Npgsql.
/// </summary>
public sealed class CategoriaProductoResponse
{
    public Guid Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public Guid? CategoriaPadreId { get; init; }
    /// <summary>Nombre de la categoría padre; <c>null</c> si es una categoría raíz.</summary>
    public string? CategoriaPadreNombre { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string? UpdatedBy { get; init; }
}
