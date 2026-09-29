using OpenSource1.Core.Entities.Inventario;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.DiariosInventario.Registros;

/// <summary>
/// Acceso a datos del registro de un lote (Task 4.3), sobre la conexión y la transacción de <c>IDbSession</c>. Todas
/// las operaciones exigen una transacción activa (lanzan <see cref="InvalidOperationException"/> si no la hay).
/// </summary>
public interface IRegistroLoteDiarioDatos
{
    /// <summary>
    /// Líneas vivas del lote, ordenadas por <c>NumeroLinea</c>, con <c>SELECT ... FOR UPDATE</c>: dos registros del mismo
    /// lote se serializan aquí (además del bloqueo de la fila del lote) y una edición concurrente de una línea espera al
    /// commit y recibe 409 por <c>xmin</c>.
    /// </summary>
    Task<IReadOnlyList<LineaDiarioARegistrar>> BloquearLineasAsync(Guid loteDiarioId, CancellationToken cancellationToken = default);

    /// <summary>Borrado lógico de las líneas registradas (ya bloqueadas). Devuelve las filas afectadas.</summary>
    Task<int> BorrarLineasAsync(IReadOnlyCollection<Guid> lineaIds, string usuario, CancellationToken cancellationToken = default);

    /// <summary>Inserta el registro (append-only).</summary>
    Task InsertarRegistroAsync(RegistroDiario registro, CancellationToken cancellationToken = default);
}

/// <summary>Instantánea de una línea bloqueada para registrarla.</summary>
public sealed record LineaDiarioARegistrar(
    Guid Id,
    int NumeroLinea,
    DateOnly FechaRegistro,
    DateOnly FechaDocumento,
    string? NumeroDocumento,
    TipoMovimientoInventario TipoMovimiento,
    Guid ProductoId,
    Guid AlmacenId,
    Guid? AlmacenDestinoId,
    Guid UnidadMedidaId,
    decimal CantidadPorUnidadMedida,
    decimal Cantidad,
    decimal? CostoUnitario,
    string? Descripcion);
