using OpenSource1.Core.Entities.Ventas;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.NotasCreditoVenta;

/// <summary>Movimiento de cliente de una factura posteada, con lo que la nota de crédito toma de él (la CxC CONGELADA).</summary>
public sealed record MovimientoClienteFactura(long Id, Guid SocioNegocioId, Guid GrupoClienteContableId, Guid CuentaCxCId);

/// <summary>
/// Costo VIGENTE de la salida de inventario de una línea de factura: <see cref="CantidadBase"/> (valor absoluto de la cantidad del
/// movimiento de producto, en unidad base) e <see cref="ImporteCosto"/> (Σ de sus movimientos de valor CostoDirecto, el original más
/// los ajustes de costo posteriores; negativo en una salida). Los movimientos de Redondeo no cuentan: son el residuo de un DÍA, no el
/// costo de esa salida (mismo criterio que la rutina de ajuste).
/// </summary>
public sealed record CostoSalidaVenta(decimal CantidadBase, decimal ImporteCosto)
{
    /// <summary>Costo unitario exacto (sin redondear) en unidad base: <c>−ImporteCosto / CantidadBase</c>.</summary>
    public decimal CostoUnitario => -ImporteCosto / CantidadBase;
}

/// <summary>
/// Lo ya acreditado de una línea de factura por notas POSTEADAS (Ruling FI): cantidad, importe y descuento acreditados; cantidad
/// devuelta al inventario y valor de costo devuelto (Σ CostoDirecto de sus entradas de devolución, positivo).
/// </summary>
public sealed record AcreditadoLinea(
    decimal Cantidad, decimal ImporteLinea, decimal ImporteDescuentoLinea, decimal CantidadDevuelta, decimal CostoDevuelto)
{
    public static readonly AcreditadoLinea Ninguno = new(0m, 0m, 0m, 0m, 0m);
}

/// <summary>Grupo de IVA de la factura: su IVA, su cuenta congelada y el IVA ya acreditado por notas POSTEADAS de la factura.</summary>
public sealed record IvaFacturaGrupo(decimal PorcentajeIva, decimal ImporteIva, Guid CuentaIvaId, decimal IvaAcreditado);

/// <summary>Línea viva de un borrador de nota, leída (y bloqueada) para postearla.</summary>
public sealed record LineaNotaAPostear(
    Guid Id,
    long LineaFacturaVentaId,
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
    decimal PorcentajeIva,
    bool DevolverInventario);

/// <summary>
/// Acceso a datos de las notas de crédito de venta (Task 8.6) con Dapper sobre la transacción de <c>IDbSession</c> (la misma que
/// enrola EF y que usan <c>IRegistroMovimientosInventario</c>, <c>IRegistroMovimientosCliente</c> e <c>IRegistroContable</c>). Los
/// métodos de bloqueo y de escritura requieren una transacción activa (lanzan <see cref="InvalidOperationException"/> si no la
/// hay); las lecturas no.
/// </summary>
public interface INotaCreditoVentaDatos
{
    /// <summary>
    /// <c>FOR UPDATE</c> del borrador vivo (serializa sus líneas, su modificación, su borrado y su posteo). <see langword="false"/>
    /// si no existe o está borrado lógicamente.
    /// </summary>
    Task<bool> BloquearBorradorAsync(Guid notaCreditoVentaBorradorId, CancellationToken cancellationToken = default);

    /// <summary>Líneas vivas del borrador, por <c>NumeroLinea</c>, <c>FOR UPDATE</c> (con el borrador ya bloqueado).</summary>
    Task<IReadOnlyList<LineaNotaAPostear>> BloquearLineasAsync(Guid notaCreditoVentaBorradorId, CancellationToken cancellationToken = default);

    /// <summary>Borrado lógico de todas las líneas vivas del borrador; devuelve cuántas.</summary>
    Task<int> BorrarLineasAsync(Guid notaCreditoVentaBorradorId, string usuario, CancellationToken cancellationToken = default);

    /// <summary><c>FOR SHARE</c> de los socios (orden de Id, sin repetir), contra su borrado concurrente.</summary>
    Task BloquearSociosAsync(IEnumerable<Guid> socioNegocioIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Factura posteada por número, o <see langword="null"/>. Con <paramref name="bloquear"/>, <c>FOR UPDATE</c> de su fila (requiere
    /// transacción): es el punto de serialización de TODAS las notas de la factura (también de las de una factura de total 0, que no
    /// tiene movimiento de cliente). Una fila append-only se puede bloquear: el trigger solo rechaza UPDATE/DELETE.
    /// </summary>
    Task<FacturaVenta?> ObtenerFacturaAsync(string numero, bool bloquear, CancellationToken cancellationToken = default);

    /// <summary>Líneas de la factura, por <c>NumeroLinea</c>.</summary>
    Task<IReadOnlyList<LineaFacturaVenta>> LineasFacturaAsync(string facturaNumero, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cantidad ya acreditada de cada línea de la factura por notas POSTEADAS (Σ <c>LineasNotaCreditoVenta.Cantidad</c>); las líneas
    /// sin notas no aparecen. Bajo el bloqueo de la factura es exacta: ninguna otra nota de la factura puede postearse a la vez.
    /// </summary>
    Task<IReadOnlyDictionary<long, decimal>> CantidadesAcreditadasAsync(string facturaNumero, CancellationToken cancellationToken = default);

    /// <summary>Movimiento de cliente (tipo Factura) de la factura; <see langword="null"/> en una factura de total 0.</summary>
    Task<MovimientoClienteFactura?> MovimientoClienteFacturaAsync(string facturaNumero, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lo acreditado de cada línea de la factura por notas POSTEADAS (ver <see cref="AcreditadoLinea"/>); las líneas sin notas no
    /// aparecen. Exacto bajo el bloqueo de la factura.
    /// </summary>
    Task<IReadOnlyDictionary<long, AcreditadoLinea>> AcreditadoPorLineaAsync(string facturaNumero, CancellationToken cancellationToken = default);

    /// <summary>Grupos de IVA de la factura por identificador (ordinal), con su cuenta congelada y lo acreditado por notas posteadas.</summary>
    Task<IReadOnlyDictionary<string, IvaFacturaGrupo>> IvaFacturaAsync(string facturaNumero, CancellationToken cancellationToken = default);

    /// <summary>Costo vigente de la salida <paramref name="movimientoProductoId"/> (ver <see cref="CostoSalidaVenta"/>), o null si no existe.</summary>
    Task<CostoSalidaVenta?> CostoSalidaAsync(long movimientoProductoId, CancellationToken cancellationToken = default);

    /// <summary>Inserta la cabecera posteada, sus líneas y sus líneas de IVA (solo <c>INSERT</c>: tablas append-only).</summary>
    Task InsertarNotaAsync(
        NotaCreditoVenta nota,
        IReadOnlyList<LineaNotaCreditoVenta> lineas,
        IReadOnlyList<LineaIvaNotaCreditoVenta> lineasIva,
        CancellationToken cancellationToken = default);
}
