using OpenSource1.Core.Enums;

namespace OpenSource1.Core.Entities.Contabilidad;

/// <summary>
/// Constancia de un asiento del libro contable (spec 5.5, equivalente a <c>G/L Register</c>): número de la serie
/// <c>CONTAB</c> (sin huecos) y rango de <see cref="MovimientoContable"/> generados. Append-only (mismo trigger que el libro),
/// sin <see cref="BaseEntity"/>, sin soft delete ni <c>xmin</c>. Invariante dura: la suma de <c>Importe</c> de sus movimientos
/// es 0 (la verifica <c>IRegistroContable</c> antes de escribir y otra vez con <c>SELECT SUM</c> tras insertar).
/// </summary>
public sealed class RegistroContable
{
    public long Id { get; set; }

    /// <summary>varchar(20), único. Número de la serie <c>CONTAB</c>.</summary>
    public required string NumeroRegistro { get; set; }

    /// <summary>Menor <c>MovimientosContables."Id"</c> del registro.</summary>
    public long DesdeMovimiento { get; set; }

    /// <summary>Mayor <c>MovimientosContables."Id"</c> del registro (rango contiguo: los escritores se serializan por la serie).</summary>
    public long HastaMovimiento { get; set; }

    public DateTimeOffset FechaCreacion { get; set; }

    /// <summary>varchar(100).</summary>
    public required string CreadoPor { get; set; }

    /// <summary>Referencia lógica al usuario (otra base de datos): sin FK.</summary>
    public Guid? UsuarioId { get; set; }

    public TipoOrigenMovimiento TipoOrigen { get; set; }

    /// <summary>varchar(50).</summary>
    public required string ClaveOrigen { get; set; }
}

/// <summary>
/// Serie de numeración de los registros contables (Task 5.5), sembrada con <c>HasData</c> e Id fijo igual que
/// <see cref="Inventario.SerieDiarioInventarioIds"/>. La consume <c>IRegistroContable</c> vía <c>IGeneradorNumeroDocumento</c>.
/// </summary>
public static class SerieContabilidadIds
{
    public const string Codigo = "CONTAB";
    public static readonly Guid SerieId = Guid.Parse("e1000000-0000-0000-0000-000000000003");
    public static readonly Guid LineaSerieId = Guid.Parse("e1000000-0000-0000-0000-000000000004");
}
