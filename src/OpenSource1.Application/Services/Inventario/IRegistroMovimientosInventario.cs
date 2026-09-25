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
}
