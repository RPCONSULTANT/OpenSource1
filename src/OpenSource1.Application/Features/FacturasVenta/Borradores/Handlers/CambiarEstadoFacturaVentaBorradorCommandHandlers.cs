using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.FacturasVenta.Borradores.Commands;
using OpenSource1.Application.Features.FacturasVenta.Borradores.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Ventas;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.FacturasVenta.Borradores.Handlers;

/// <summary>Abierta -&gt; Liberada, bajo el <c>FOR UPDATE</c> del borrador. Exige al menos una línea viva.</summary>
public sealed class LiberarFacturaVentaBorradorCommandHandler(
    IUnitOfWork unitOfWork, IFacturaVentaBorradorBloqueoService bloqueo, IFacturaVentaBorradorReadRepository readRepository)
    : IRequestHandler<LiberarFacturaVentaBorradorCommand, Result<FacturaVentaBorradorResponse>>
{
    public Task<Result<FacturaVentaBorradorResponse>> Handle(LiberarFacturaVentaBorradorCommand request, CancellationToken cancellationToken) =>
        CambioEstadoFacturaVentaBorrador.EjecutarAsync(
            unitOfWork, bloqueo, readRepository, request.Id, EstadoFacturaBorrador.Liberada, cancellationToken);
}

/// <summary>Liberada -&gt; Abierta, bajo el <c>FOR UPDATE</c> del borrador.</summary>
public sealed class ReabrirFacturaVentaBorradorCommandHandler(
    IUnitOfWork unitOfWork, IFacturaVentaBorradorBloqueoService bloqueo, IFacturaVentaBorradorReadRepository readRepository)
    : IRequestHandler<ReabrirFacturaVentaBorradorCommand, Result<FacturaVentaBorradorResponse>>
{
    public Task<Result<FacturaVentaBorradorResponse>> Handle(ReabrirFacturaVentaBorradorCommand request, CancellationToken cancellationToken) =>
        CambioEstadoFacturaVentaBorrador.EjecutarAsync(
            unitOfWork, bloqueo, readRepository, request.Id, EstadoFacturaBorrador.Abierta, cancellationToken);
}

internal static class CambioEstadoFacturaVentaBorrador
{
    public static async Task<Result<FacturaVentaBorradorResponse>> EjecutarAsync(
        IUnitOfWork unitOfWork,
        IFacturaVentaBorradorBloqueoService bloqueo,
        IFacturaVentaBorradorReadRepository readRepository,
        Guid id,
        EstadoFacturaBorrador nuevoEstado,
        CancellationToken cancellationToken)
    {
        await using var transaccion = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var estado = await bloqueo.BloquearYObtenerEstadoAsync(id, cancellationToken);
        if (estado is null)
        {
            return Result<FacturaVentaBorradorResponse>.Fallo(FacturaVentaBorradorErrores.BorradorNoEncontrado());
        }

        if (estado == nuevoEstado)
        {
            return Result<FacturaVentaBorradorResponse>.Fallo(nuevoEstado == EstadoFacturaBorrador.Liberada
                ? new Error("factura.liberada", "El borrador ya está liberado.", "Id")
                : new Error("factura.abierta", "El borrador ya está abierto.", "Id"));
        }

        if (nuevoEstado == EstadoFacturaBorrador.Liberada)
        {
            var linea = await unitOfWork.Repository<LineaFacturaVentaBorrador>().FirstOrDefaultAsync(
                x => x.FacturaVentaBorradorId == id, cancellationToken: cancellationToken);
            if (linea is null)
            {
                return Result<FacturaVentaBorradorResponse>.Fallo(new Error(
                    "factura.sin_lineas", "No se puede liberar un borrador sin líneas.", "Id"));
            }
        }

        var repository = unitOfWork.Repository<FacturaVentaBorrador>();
        var entity = await repository.FirstOrDefaultAsync(x => x.Id == id, asTracking: true, cancellationToken: cancellationToken);
        entity!.Estado = nuevoEstado;
        repository.Update(entity);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result<FacturaVentaBorradorResponse>.Exito((await readRepository.GetByIdAsync(id, cancellationToken))!);
    }
}
