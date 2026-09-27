using MediatR;
using OpenSource1.Application.Data;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Services.Clientes;
using OpenSource1.Application.Services.Contabilidad;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Clientes;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.Cobros;

/// <summary>
/// Registro de un pago de cliente (Task 6.5). Todo en UNA transacción de <see cref="IUnitOfWork"/>: un <c>Result</c> fallido o una
/// excepción deshacen todo, incluido el número de la serie <c>COBRO</c>, que así no deja huecos.
/// <list type="number">
/// <item>Validación de la entrada (importe, fecha, descripción) sin transacción.</item>
/// <item>Socio (<c>FOR SHARE</c>): existe y no está bloqueado <c>Todo</c>; cuenta de caja/banco válida; CxC derivada del grupo de
/// cliente contable VIGENTE del socio (<see cref="IDerivadorCuentas.CuentaCxCAsync"/>, D4). Todos los errores juntos y ANTES de
/// escribir: un fallo no intenta un solo INSERT.</item>
/// <item>Número <c>COBRO</c>, movimiento Pago (<c>ImporteOriginal = −importe</c>, CxC y grupo congelados) con su detalle Pago y
/// asiento débito caja / crédito CxC (<see cref="TipoDocumentoContable.Cobro"/>, origen <see cref="TipoOrigenMovimiento.Cobro"/>,
/// clave = número COBRO, ambas patas con el socio del pago).</item>
/// </list>
/// <para>
/// Orden de locks (subconjunto del global): socio (<c>FOR SHARE</c>) → línea de serie <c>COBRO</c> → cuentas (<c>FOR SHARE</c>,
/// orden de Id, dentro de <see cref="IRegistroContable"/>) → línea de serie <c>CONTAB</c>. No bloquea movimientos de cliente
/// existentes (solo inserta uno nuevo).
/// </para>
/// </summary>
public sealed class RegistrarPagoClienteCommandHandler(
    IUnitOfWork unitOfWork,
    ICobroDatos datos,
    IDerivadorCuentas derivador,
    IRegistroMovimientosCliente registroClientes,
    IRegistroContable registroContable,
    IGeneradorNumeroDocumento generadorNumero)
    : IRequestHandler<RegistrarPagoClienteCommand, Result<ResultadoPagoCliente>>
{
    private const int LongitudNumero = 20;
    private const int LongitudDescripcion = 200;

    public async Task<Result<ResultadoPagoCliente>> Handle(RegistrarPagoClienteCommand request, CancellationToken cancellationToken)
    {
        if (unitOfWork.HayTransaccionActiva)
        {
            throw new InvalidOperationException(
                "RegistrarPagoCliente debe ejecutarse fuera de otra transacción: garantiza su propia atomicidad.");
        }

        var errores = new List<Error>();
        if (!CobroReglas.ImporteValido(request.Importe))
        {
            errores.Add(CobroReglas.ImporteInvalido("El importe del pago"));
        }

        if (request.FechaRegistro == default)
        {
            errores.Add(new Error("cobro.fecha_invalida", "La fecha de registro del pago es obligatoria.", "FechaRegistro"));
        }

        if (request.Descripcion is { Length: > LongitudDescripcion })
        {
            errores.Add(new Error(
                "cobro.descripcion_invalida", $"La descripción admite como máximo {LongitudDescripcion} caracteres.", "Descripcion"));
        }

        if (errores.Count > 0)
        {
            return Fallo([.. errores]);
        }

        // Sin CommitAsync, salir del "await using" deshace la transacción y libera los bloqueos.
        await using var transaccion = await unitOfWork.BeginTransactionAsync(cancellationToken);

        // Socio (compartido hasta el commit) y, antes de escribir, la cuenta de caja y la CxC.
        var socio = (await datos.BloquearSociosAsync([request.SocioNegocioId], cancellationToken)).SingleOrDefault();
        if (socio is null)
        {
            errores.Add(new Error("cobro.socio_invalido", "El cliente del pago no existe.", "SocioNegocioId"));
        }
        else if (socio.Bloqueado == BloqueoSocioNegocio.Todo)
        {
            errores.Add(new Error(
                "cobro.socio_bloqueado", "El cliente está bloqueado para todo: no se le pueden registrar cobros.", "SocioNegocioId"));
        }

        var cuentaCajaId = request.CuentaCajaId ?? CuentaContableIds.Caja;
        var caja = await unitOfWork.Repository<CuentaContable>().FirstOrDefaultAsync(x => x.Id == cuentaCajaId, cancellationToken: cancellationToken);
        if (caja is null || caja.TipoCuenta != TipoCuentaContable.Posteo || caja.Bloqueada || !caja.PosteoDirecto)
        {
            errores.Add(new Error(
                "cobro.cuenta_invalida",
                "La cuenta de caja/banco no existe o no admite captura directa: debe ser de posteo, no bloqueada y de posteo directo.",
                "CuentaCajaId"));
        }

        var cuentaCxCId = Guid.Empty;
        if (socio is not null && socio.Bloqueado != BloqueoSocioNegocio.Todo)
        {
            var cxc = await derivador.CuentaCxCAsync(socio.GrupoClienteContableId, cancellationToken);
            if (!cxc.TryObtenerValor(out cuentaCxCId))
            {
                errores.Add(cxc.Errores[0] with { Campo = "SocioNegocioId" });
            }
            else if (cuentaCxCId == cuentaCajaId)
            {
                errores.Add(new Error(
                    "cobro.cuenta_invalida", "La cuenta de caja/banco no puede ser la cuenta por cobrar del cliente.", "CuentaCajaId"));
            }
        }

        if (errores.Count > 0)
        {
            return Fallo([.. errores]);
        }

        // Número COBRO (FOR UPDATE de la línea de la serie, sin huecos: se deshace con todo lo demás).
        var numeroResultado = await generadorNumero.SiguienteAsync(
            SerieCobroIds.Codigo, DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken);
        if (!numeroResultado.TryObtenerValor(out var numero))
        {
            return Result<ResultadoPagoCliente>.Fallo(numeroResultado);
        }

        if (numero.Length > LongitudNumero)
        {
            return Fallo(new Error("cobro.serie_invalida", $"El número de cobro generado supera los {LongitudNumero} caracteres."));
        }

        var fechaDocumento = request.FechaDocumento ?? request.FechaRegistro;
        var descripcion = string.IsNullOrWhiteSpace(request.Descripcion) ? $"Cobro {numero}" : request.Descripcion.Trim();

        var movimiento = await registroClientes.RegistrarAsync(new MovimientoClienteSolicitud(
            socio!.Id,
            request.FechaRegistro,
            fechaDocumento,
            fechaDocumento,
            TipoDocumentoCliente.Pago,
            numero,
            descripcion,
            -request.Importe,
            socio.GrupoClienteContableId!.Value,
            cuentaCxCId,
            TipoOrigenMovimiento.Cobro,
            numero), cancellationToken);
        if (!movimiento.TryObtenerValor(out var registrado))
        {
            return Result<ResultadoPagoCliente>.Fallo(movimiento);
        }

        var asiento = await registroContable.RegistrarAsync(new AsientoContable(
            request.FechaRegistro,
            fechaDocumento,
            TipoDocumentoContable.Cobro,
            numero,
            descripcion,
            TipoOrigenMovimiento.Cobro,
            numero,
            [
                new LineaAsiento(cuentaCajaId, request.Importe, descripcion, socio.Id),
                new LineaAsiento(cuentaCxCId, -request.Importe, descripcion, socio.Id),
            ]), cancellationToken);
        if (!asiento.TryObtenerValor(out var asientoRegistrado))
        {
            return Result<ResultadoPagoCliente>.Fallo(asiento);
        }

        await unitOfWork.CommitAsync(cancellationToken);

        return Result<ResultadoPagoCliente>.Exito(
            new ResultadoPagoCliente(numero, registrado.MovimientoClienteId, asientoRegistrado.NumeroRegistro));
    }

    private static Result<ResultadoPagoCliente> Fallo(params Error[] errores) => Result<ResultadoPagoCliente>.Fallo(errores);
}
