using OpenSource1.Core.Entities.Ventas;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.FacturasVenta.Posteo;

/// <summary>Línea viva de un borrador, leída (y bloqueada) para postearla, con todos sus valores congelados.</summary>
public sealed record LineaFacturaAPostear(
    Guid Id,
    int NumeroLinea,
    TipoLineaFactura Tipo,
    Guid? ProductoId,
    Guid? CuentaContableId,
    string? Descripcion,
    Guid? AlmacenId,
    Guid? UnidadMedidaId,
    decimal CantidadPorUnidadMedida,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal PorcentajeDescuentoLinea,
    decimal ImporteDescuentoLinea,
    decimal ImporteLinea,
    Guid? GrupoProductoId,
    Guid? GrupoIvaProductoId,
    Guid? GrupoInventarioId,
    string? IdentificadorIva,
    decimal PorcentajeIva);

/// <summary>
/// Acceso a datos del posteo de una factura de venta (Task 6.4) con Dapper sobre la transacción de <c>IDbSession</c>, la misma
/// que enrola EF y que usan <c>IRegistroMovimientosInventario</c>, <c>IRegistroMovimientosCliente</c> e <c>IRegistroContable</c>.
/// Todos los métodos requieren una transacción activa (lanzan <see cref="InvalidOperationException"/> si no la hay).
/// </summary>
public interface IPosteoFacturaVentaDatos
{
    /// <summary>
    /// Líneas vivas del borrador, ordenadas por <c>NumeroLinea</c>, bloqueadas <c>FOR UPDATE</c>. Se llama con la fila del borrador
    /// ya bloqueada: un segundo posteo del mismo borrador espera en el bloqueo del borrador y después lo ve borrado.
    /// </summary>
    Task<IReadOnlyList<LineaFacturaAPostear>> BloquearLineasAsync(Guid facturaVentaBorradorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bloqueo compartido (<c>FOR SHARE</c>, orden de Id) de las filas de los socios del documento hasta el commit: el borrado del
    /// socio (que toma su fila <c>FOR UPDATE</c> antes de evaluar la guarda de uso) espera a este posteo y después ve la factura
    /// (409), o este posteo espera al borrado y ve el socio borrado (400). Nunca queda una factura de un socio borrado.
    /// </summary>
    Task BloquearSociosAsync(IEnumerable<Guid> socioNegocioIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserta la cabecera posteada, sus líneas y sus líneas de IVA (solo <c>INSERT</c>: las tres tablas son append-only).
    /// La cabecera ya lleva <c>RegistroContableId</c> (el asiento se registra antes; null en una factura de total 0, sin
    /// asiento) y cada línea de Producto, su
    /// <c>MovimientoProductoId</c>.
    /// </summary>
    Task InsertarFacturaAsync(
        FacturaVenta factura,
        IReadOnlyList<LineaFacturaVenta> lineas,
        IReadOnlyList<LineaIvaFacturaVenta> lineasIva,
        CancellationToken cancellationToken = default);
}
