using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Services.Clientes;

/// <summary>
/// Petición de alta de un movimiento del libro de clientes (spec 6.4) con su detalle inicial.
/// </summary>
/// <param name="SocioNegocioId">El cliente del movimiento: en una factura, el facturar-a (desviaciones de la Fase 6).</param>
/// <param name="ImporteOriginal">Con signo: + factura (debe el cliente), − pago/nota de crédito. Distinto de 0, 4 decimales como máximo.</param>
/// <param name="CuentaCxCId">Cuenta por cobrar YA derivada (<c>IDerivadorCuentas.CuentaCxCAsync</c>) y congelada en el movimiento.</param>
public sealed record MovimientoClienteSolicitud(
    Guid SocioNegocioId,
    DateOnly FechaRegistro,
    DateOnly FechaDocumento,
    DateOnly FechaVencimiento,
    TipoDocumentoCliente TipoDocumento,
    string NumeroDocumento,
    string? Descripcion,
    decimal ImporteOriginal,
    Guid GrupoClienteContableId,
    Guid CuentaCxCId,
    TipoOrigenMovimiento TipoOrigen,
    string ClaveOrigen);

/// <summary>Ids escritos: el movimiento y su fila de detalle inicial.</summary>
public sealed record MovimientoClienteRegistrado(long MovimientoClienteId, long DetalleId);

/// <summary>
/// Escritor ÚNICO del libro de clientes (<c>MovimientosCliente</c> + <c>MovimientosClienteDetalle</c>), mismo papel que
/// <c>IRegistroMovimientosInventario</c> en inventario e <c>IRegistroContable</c> en contabilidad. Solo hace <c>INSERT</c> (el
/// libro es append-only). La facturación (Task 6.4) registra facturas; la Task 6.5 lo amplía con pagos y aplicaciones.
/// </summary>
public interface IRegistroMovimientosCliente
{
    /// <summary>
    /// Inserta el movimiento y su detalle inicial: <see cref="TipoDetalleCliente.Pago"/> si el documento es un pago y
    /// <see cref="TipoDetalleCliente.ImporteInicial"/> en los demás, con <c>Importe = ImporteOriginal</c>. Requiere transacción
    /// activa (<c>clientes.sin_transaccion</c> si no la hay) y no hace commit. Un resultado fallido no escribe ninguna fila (las
    /// validaciones van antes de cualquier INSERT).
    /// </summary>
    Task<Result<MovimientoClienteRegistrado>> RegistrarAsync(MovimientoClienteSolicitud solicitud, CancellationToken ct = default);
}
