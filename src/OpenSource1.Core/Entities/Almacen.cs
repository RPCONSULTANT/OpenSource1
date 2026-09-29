namespace OpenSource1.Core.Entities;

/// <summary>
/// Almacén físico o lógico donde se guarda existencia (Fase 3, libro de inventario). Entidad
/// plana sin value objects, mismo patrón que <see cref="UnidadMedida"/>.
/// </summary>
public sealed class Almacen : BaseEntity
{
    public required string Codigo { get; set; }
    public required string Nombre { get; set; }
    public string? DireccionLinea1 { get; set; }
    public string? DireccionLinea2 { get; set; }
    public string? Ciudad { get; set; }

    /// <summary>Código ISO de país, validado con el value object <c>Pais</c> existente (sin mapear como ComplexProperty).</summary>
    public string? PaisCodigo { get; set; }

    public bool Bloqueado { get; set; }

    /// <summary>
    /// A lo sumo un almacén puede tener este valor en <see langword="true"/> (índice único parcial
    /// <c>IX_Almacenes_EsPredeterminado</c>). Siempre existe uno: la semilla <see cref="AlmacenIds.Principal"/>
    /// nace predeterminada y los handlers de alta/modificación mueven la marca en transacción en vez de
    /// dejarla desaparecer.
    /// </summary>
    public bool EsPredeterminado { get; set; }
}

/// <summary>Ids fijos de almacenes sembrados, para que otras migraciones (Task 3.6) puedan referenciarlos.</summary>
public static class AlmacenIds
{
    public static readonly Guid Principal = Guid.Parse("b1000000-0000-0000-0000-000000000001");
}
