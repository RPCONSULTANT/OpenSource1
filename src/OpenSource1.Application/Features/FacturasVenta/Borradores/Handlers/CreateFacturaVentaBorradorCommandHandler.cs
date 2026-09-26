using MediatR;
using OpenSource1.Application.Data;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.FacturasVenta.Borradores.Commands;
using OpenSource1.Application.Features.FacturasVenta.Borradores.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Ventas;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.FacturasVenta.Borradores.Handlers;

/// <summary>
/// Alta de un borrador: snapshot del facturar-a y grupos congelados (<see cref="FacturaVentaBorradorReglas.TomarSnapshotAsync"/>),
/// vencimiento por el término de pago, almacén predeterminado, moneda DOP y número de la serie <c>FV-BORR</c> (dentro de la
/// transacción, que <see cref="IGeneradorNumeroDocumento"/> exige).
/// </summary>
public sealed class CreateFacturaVentaBorradorCommandHandler(
    IUnitOfWork unitOfWork, IGeneradorNumeroDocumento generadorNumero, IFacturaVentaBorradorReadRepository readRepository)
    : IRequestHandler<CreateFacturaVentaBorradorCommand, Result<FacturaVentaBorradorResponse>>
{
    public async Task<Result<FacturaVentaBorradorResponse>> Handle(CreateFacturaVentaBorradorCommand request, CancellationToken cancellationToken)
    {
        var descripcion = FacturaVentaBorradorReglas.Normalizar(request.Descripcion);
        if (FacturaVentaBorradorReglas.ValidarDescripcion(descripcion) is { } errorDescripcion)
        {
            return Result<FacturaVentaBorradorResponse>.Fallo(errorDescripcion);
        }

        var snapshot = await FacturaVentaBorradorReglas.TomarSnapshotAsync(
            unitOfWork, request.SocioNegocioId, request.SocioNegocioFacturarAId ?? request.SocioNegocioId, cancellationToken);
        if (!snapshot.TryObtenerValor(out var socios))
        {
            return Result<FacturaVentaBorradorResponse>.Fallo(snapshot);
        }

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var fechaRegistro = request.FechaRegistro ?? hoy;
        var fechaDocumento = request.FechaDocumento ?? fechaRegistro;
        var fechaVencimiento = request.FechaVencimiento
            ?? await FacturaVentaBorradorReglas.CalcularVencimientoAsync(unitOfWork, fechaDocumento, socios.TerminoPagoId, cancellationToken);
        if (FacturaVentaBorradorReglas.ValidarFechas(fechaRegistro, fechaDocumento, fechaVencimiento) is { } errorFecha)
        {
            return Result<FacturaVentaBorradorResponse>.Fallo(errorFecha);
        }

        var almacen = await FacturaVentaBorradorReglas.ResolverAlmacenAsync(unitOfWork, request.AlmacenId, cancellationToken);
        if (!almacen.TryObtenerValor(out var almacenId))
        {
            return Result<FacturaVentaBorradorResponse>.Fallo(almacen);
        }

        // Sin CommitAsync, salir del "await using" deshace la transacción (el número de FV-BORR no se consume).
        await using var transaccion = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var numero = await generadorNumero.SiguienteAsync(SerieFacturaVentaIds.CodigoBorrador, hoy, cancellationToken);
        if (!numero.TryObtenerValor(out var numeroBorrador))
        {
            return Result<FacturaVentaBorradorResponse>.Fallo(numero);
        }

        var entity = new FacturaVentaBorrador
        {
            Numero = numeroBorrador,
            NombreFacturacion = socios.NombreFacturacion,
            FechaRegistro = fechaRegistro,
            FechaDocumento = fechaDocumento,
            FechaVencimiento = fechaVencimiento,
            AlmacenId = almacenId,
            Estado = EstadoFacturaBorrador.Abierta,
            Moneda = SerieFacturaVentaIds.MonedaPorDefecto,
            Descripcion = descripcion
        };
        socios.Aplicar(entity);

        await unitOfWork.Repository<FacturaVentaBorrador>().AddAsync(entity, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result<FacturaVentaBorradorResponse>.Exito((await readRepository.GetByIdAsync(entity.Id, cancellationToken))!);
    }
}
