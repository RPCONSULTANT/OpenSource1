using OpenSource1.Core.Enums;

namespace OpenSource1.Core.Entities.Inventario;

/// <summary>
/// Plantilla de diario de inventario (Fase 4). Sembrada y de solo lectura: solo existen las dos filas de
/// <see cref="PlantillaDiarioIds"/> (<c>ARTICULO</c>/<c>RECLASIF</c>), sin CRUD — dos tipos fijos no
/// justifican un mantenimiento (ver "Desviaciones acordadas" de la Fase 4 en el spec).
/// </summary>
public sealed class PlantillaDiario : BaseEntity
{
    public required string Codigo { get; set; }
    public required string Nombre { get; set; }
    public TipoPlantillaDiario Tipo { get; set; }

    /// <summary>Serie de numeración por defecto de los lotes de esta plantilla (FK Restrict).</summary>
    public Guid SerieId { get; set; }
}

/// <summary>Ids fijos de las plantillas sembradas, para que otras migraciones/tasks (4.3+) puedan referenciarlas.</summary>
public static class PlantillaDiarioIds
{
    public static readonly Guid Articulo = Guid.Parse("d1000000-0000-0000-0000-000000000001");
    public static readonly Guid Reclasificacion = Guid.Parse("d1000000-0000-0000-0000-000000000002");
}

/// <summary>
/// Serie de numeración sembrada para los diarios de inventario (Task 4.2). A diferencia de SOCIOS (Task 2.9,
/// sembrada con SQL porque el contador dependía de filas ya migradas), esta no depende de datos existentes:
/// Id fijo sembrado con <c>HasData</c>, consumida por <c>IGeneradorNumeroDocumento</c> en el posteo (Task 4.3).
/// </summary>
public static class SerieDiarioInventarioIds
{
    public const string Codigo = "DIARIO-INV";
    public static readonly Guid SerieId = Guid.Parse("e1000000-0000-0000-0000-000000000001");
    public static readonly Guid LineaSerieId = Guid.Parse("e1000000-0000-0000-0000-000000000002");
}
