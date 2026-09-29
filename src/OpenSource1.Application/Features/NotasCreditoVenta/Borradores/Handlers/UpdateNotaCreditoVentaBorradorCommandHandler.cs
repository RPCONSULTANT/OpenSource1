using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.NotasCreditoVenta.Borradores.Commands;
using OpenSource1.Application.Features.NotasCreditoVenta.Borradores.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Ventas;

namespace OpenSource1.Application.Features.NotasCreditoVenta.Borradores.Handlers;

/// <summary>
/// Modificación de las fechas y la descripción bajo el <c>FOR UPDATE</c> del borrador. Inexistente -&gt; 404; <c>Xmin</c>
/// desactualizado -&gt; 409; fecha de registro anterior a la de la factura -&gt; 400.
/// </summary>
public sealed class UpdateNotaCreditoVentaBorradorCommandHandler(
    IUnitOfWork unitOfWork, INotaCreditoVentaDatos datos, INotaCreditoVentaBorradorReadRepository readRepository)
    : IRequestHandler<UpdateNotaCreditoVentaBorradorCommand, Result<NotaCreditoVentaBorradorResponse>>
{
    public async Task<Result<NotaCreditoVentaBorradorResponse>> Handle(
        UpdateNotaCreditoVentaBorradorCommand request, CancellationToken cancellationToken)
    {
        string? descripcion = null;
        if (request.Descripcion is not null)
        {
            descripcion = NotaCreditoVentaErrores.Normalizar(request.Descripcion);
            if (NotaCreditoVentaErrores.ValidarDescripcion(descripcion) is { } errorDescripcion)
            {
                return Result<NotaCreditoVentaBorradorResponse>.Fallo(errorDescripcion);
            }
        }

        await using var transaccion = await unitOfWork.BeginTransactionAsync(cancellationToken);

        if (!await datos.BloquearBorradorAsync(request.Id, cancellationToken))
        {
            return Result<NotaCreditoVentaBorradorResponse>.Fallo(NotaCreditoVentaErrores.BorradorNoEncontrado());
        }

        var repository = unitOfWork.Repository<NotaCreditoVentaBorrador>();
        var entity = (await repository.FirstOrDefaultAsync(x => x.Id == request.Id, asTracking: true, cancellationToken: cancellationToken))!;

        var fechaRegistro = request.FechaRegistro ?? entity.FechaRegistro;
        var fechaDocumento = request.FechaDocumento ?? entity.FechaDocumento;
        var factura = await datos.ObtenerFacturaAsync(entity.FacturaVentaNumero, bloquear: false, cancellationToken);
        if (factura is not null && fechaRegistro < factura.FechaRegistro)
        {
            return Result<NotaCreditoVentaBorradorResponse>.Fallo(NotaCreditoVentaErrores.FechaAnteriorAFactura(factura.FechaRegistro));
        }

        if (fechaDocumento == default)
        {
            return Result<NotaCreditoVentaBorradorResponse>.Fallo(
                new Error("nota_credito.fecha_invalida", "La fecha de documento no es válida.", "FechaDocumento"));
        }

        repository.EstablecerVersionOriginal(entity, request.Xmin);
        entity.FechaRegistro = fechaRegistro;
        entity.FechaDocumento = fechaDocumento;
        if (request.Descripcion is not null)
        {
            entity.Descripcion = descripcion;
        }

        repository.Update(entity);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result<NotaCreditoVentaBorradorResponse>.Exito((await readRepository.GetByIdAsync(entity.Id, cancellationToken))!);
    }
}
