using OpenSource1.Core.Common;

namespace OpenSource1.Application.Services.Inventario;

/// <summary>
/// Único punto de escritura del libro de inventario (<c>MovimientosProducto</c>, <c>MovimientosValor</c>,
/// <c>AplicacionesMovimientoProducto</c>). Serializa por producto con un advisory lock transaccional.
/// </summary>
public interface IRegistroMovimientosInventario
{
    /// <summary>
    /// Requiere transacción activa (error <c>inventario.sin_transaccion</c> si no la hay, como
    /// <c>GeneradorNumeroDocumento</c>). No hace commit: el llamador confirma o deshace todo el documento.
    /// Un resultado fallido no escribe ninguna fila (las validaciones van antes de cualquier INSERT).
    /// </summary>
    Task<Result<MovimientoRegistrado>> RegistrarAsync(MovimientoInventarioSolicitud solicitud, CancellationToken ct = default);

    /// <summary>
    /// Toma, en orden ascendente de <see cref="Guid"/> y sin repetir, el bloqueo transaccional de cada producto (la misma
    /// clave que usa <see cref="RegistrarAsync"/>; es reentrante, así que registrar después no vuelve a esperar).
    /// <para>
    /// Un llamador que registra VARIAS líneas en una transacción (un documento con varios productos) DEBE invocarlo con
    /// todos los productos del documento ANTES del primer <see cref="RegistrarAsync"/>: si cada línea tomara su bloqueo
    /// al registrarse, dos documentos concurrentes con líneas A,B y B,A se interbloquearían (SQLSTATE 40P01).
    /// </para>
    /// Requiere transacción activa (lanza <see cref="InvalidOperationException"/> si no la hay: es un error de programación).
    /// </summary>
    Task BloquearProductosAsync(IEnumerable<Guid> productoIds, CancellationToken ct = default);
}
