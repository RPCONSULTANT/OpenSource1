namespace OpenSource1.Core.Entities.Inventario;

/// <summary>
/// Constancia del registro (posteo) de un <see cref="LoteDiario"/> (Task 4.3): número de la serie sin huecos y rango de
/// <c>MovimientosProducto</c> generados. Append-only como el libro (mismo trigger <c>libro_inventario_append_only()</c>):
/// sin <see cref="BaseEntity"/>, sin soft delete ni <c>xmin</c>. Lo escribe solo <c>PostearLoteDiarioCommandHandler</c>.
/// </summary>
public sealed class RegistroDiario
{
    public long Id { get; set; }

    /// <summary>varchar(20), único. También es la <c>ClaveOrigen</c> de los movimientos generados.</summary>
    public required string NumeroRegistro { get; set; }

    public Guid LoteDiarioId { get; set; }

    /// <summary>Menor <c>MovimientosProducto."Id"</c> generado por el registro.</summary>
    public long DesdeMovimientoProducto { get; set; }

    /// <summary>Mayor <c>MovimientosProducto."Id"</c> generado por el registro.</summary>
    public long HastaMovimientoProducto { get; set; }

    /// <summary>Número de líneas del lote registradas (una transferencia cuenta una vez aunque genere dos movimientos).</summary>
    public int Lineas { get; set; }

    public DateTimeOffset FechaCreacion { get; set; }

    /// <summary>varchar(100).</summary>
    public required string CreadoPor { get; set; }

    /// <summary>Referencia lógica al usuario (otra base de datos): sin FK.</summary>
    public Guid? UsuarioId { get; set; }
}
