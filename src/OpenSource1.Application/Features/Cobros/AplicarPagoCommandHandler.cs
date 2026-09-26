using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Services.Clientes;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.Cobros;

/// <summary>
/// Aplicación de un pago a una factura (Task 6.5), en UNA transacción propia y sin asiento.
/// <list type="number">
/// <item>Entrada: Ids positivos y distintos, importe &gt; 0 con 2 decimales como máximo.</item>
/// <item>Bloqueo <c>FOR UPDATE</c> de los dos movimientos en orden de Id (<see cref="IRegistroMovimientosCliente.BloquearMovimientosAsync"/>):
/// inexistentes → 400 (<c>cobro.movimiento_invalido</c>, referencia del cuerpo, no 404).</item>
/// <item>Socio (<c>FOR SHARE</c>): un socio bloqueado <c>Todo</c> no admite aplicaciones (como no admite pagos); bloqueado solo para
/// <c>Facturacion</c>, sí.</item>
/// <item><see cref="IRegistroMovimientosCliente.AplicarAsync"/> valida con los restantes posteriores al bloqueo (mismo socio, signos,
/// importe ≤ mínimo de los restantes) antes de insertar las dos filas Aplicación.</item>
/// </list>
/// <para>
/// Orden de locks: movimientos de cliente (<c>FOR UPDATE</c>, orden de Id: ocupan el lugar del "documento", el primero del orden
/// global) → socios (<c>FOR SHARE</c>). El posteo de facturas y el pago bloquean socios <c>FOR SHARE</c> pero nunca esperan por un
/// movimiento de cliente existente (solo insertan movimientos nuevos), así que no se forma ningún ciclo.
/// </para>
/// </summary>
public sealed class AplicarPagoCommandHandler(
    IUnitOfWork unitOfWork,
    IRegistroMovimientosCliente registroClientes,
    ICobroDatos datos)
    : IRequestHandler<AplicarPagoCommand, Result<ResultadoAplicacionPago>>
{
    public async Task<Result<ResultadoAplicacionPago>> Handle(AplicarPagoCommand request, CancellationToken cancellationToken)
    {
        if (unitOfWork.HayTransaccionActiva)
        {
            throw new InvalidOperationException(
                "AplicarPago debe ejecutarse fuera de otra transacción: garantiza su propia atomicidad.");
        }

        var errores = new List<Error>();
        if (request.MovimientoFacturaId <= 0)
        {
            errores.Add(MovimientoInvalido("MovimientoFacturaId"));
        }

        if (request.MovimientoPagoId <= 0)
        {
            errores.Add(MovimientoInvalido("MovimientoPagoId"));
        }
        else if (request.MovimientoPagoId == request.MovimientoFacturaId)
        {
            errores.Add(new Error(
                "cobro.aplicacion_invalida", "El pago y el documento a pagar deben ser movimientos distintos.", "MovimientoPagoId"));
        }

        if (!CobroReglas.ImporteValido(request.Importe))
        {
            errores.Add(CobroReglas.ImporteInvalido("El importe a aplicar"));
        }

        if (errores.Count > 0)
        {
            return Fallo([.. errores]);
        }

        await using var transaccion = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var bloqueados = await registroClientes.BloquearMovimientosAsync(
            [request.MovimientoFacturaId, request.MovimientoPagoId], cancellationToken);
        var factura = bloqueados.FirstOrDefault(m => m.Id == request.MovimientoFacturaId);
        var pago = bloqueados.FirstOrDefault(m => m.Id == request.MovimientoPagoId);
        if (factura is null)
        {
            errores.Add(MovimientoInvalido("MovimientoFacturaId"));
        }

        if (pago is null)
        {
            errores.Add(MovimientoInvalido("MovimientoPagoId"));
        }

        if (factura is null || pago is null)
        {
            return Fallo([.. errores]);
        }

        // Con socios distintos, AplicarAsync lo rechaza; aquí solo se comprueba el bloqueo de los socios implicados.
        var sociosIds = new[] { factura.SocioNegocioId, pago.SocioNegocioId }.Distinct().ToList();
        var socios = await datos.BloquearSociosAsync(sociosIds, cancellationToken);
        if (socios.Count != sociosIds.Count || socios.Any(s => s.Bloqueado == BloqueoSocioNegocio.Todo))
        {
            return Fallo(new Error(
                "cobro.socio_bloqueado",
                "El cliente de los movimientos no existe o está bloqueado para todo: no se le pueden aplicar cobros.",
                "MovimientoFacturaId"));
        }

        var aplicacion = await registroClientes.AplicarAsync(new AplicacionClienteSolicitud(
            factura.Id,
            pago.Id,
            request.Importe,
            request.FechaRegistro ?? DateOnly.FromDateTime(DateTime.UtcNow),
            TipoOrigenMovimiento.Cobro,
            pago.NumeroDocumento), cancellationToken);
        if (!aplicacion.TryObtenerValor(out var registrada))
        {
            return Result<ResultadoAplicacionPago>.Fallo(aplicacion);
        }

        await unitOfWork.CommitAsync(cancellationToken);

        return Result<ResultadoAplicacionPago>.Exito(new ResultadoAplicacionPago(
            factura.Id, pago.Id, request.Importe, registrada.RestanteFactura, registrada.RestantePago));
    }

    private static Error MovimientoInvalido(string campo) =>
        new("cobro.movimiento_invalido", "El movimiento de cliente indicado no existe.", campo);

    private static Result<ResultadoAplicacionPago> Fallo(params Error[] errores) => Result<ResultadoAplicacionPago>.Fallo(errores);
}
