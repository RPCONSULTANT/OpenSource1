using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.NotasCreditoVenta.Borradores.Commands;
using OpenSource1.Application.Features.NotasCreditoVenta.Borradores.Dtos;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Ventas;

namespace OpenSource1.Application.Features.NotasCreditoVenta.Borradores.Handlers;

/// <summary>
/// Alta de una línea bajo el <c>FOR UPDATE</c> del borrador de la RUTA (404 si no existe). La línea de factura debe ser de la
/// factura del borrador (<c>nota_credito.linea_factura_invalida</c>) y no estar ya en el borrador (<c>nota_credito.linea_duplicada</c>);
/// cantidad, devolución e importes con <see cref="LineaNotaCreditoVentaReglas"/> contra lo acreditado por notas posteadas.
/// </summary>
public sealed class CreateLineaNotaCreditoVentaBorradorCommandHandler(
    IUnitOfWork unitOfWork,
    INotaCreditoVentaDatos datos,
    IConversionUnidadMedidaService conversion,
    INotaCreditoVentaBorradorReadRepository readRepository)
    : IRequestHandler<CreateLineaNotaCreditoVentaBorradorCommand, Result<LineaNotaCreditoVentaBorradorResponse>>
{
    public async Task<Result<LineaNotaCreditoVentaBorradorResponse>> Handle(
        CreateLineaNotaCreditoVentaBorradorCommand request, CancellationToken cancellationToken)
    {
        await using var transaccion = await unitOfWork.BeginTransactionAsync(cancellationToken);

        if (!await datos.BloquearBorradorAsync(request.NotaCreditoVentaBorradorId, cancellationToken))
        {
            return Fallo(NotaCreditoVentaErrores.BorradorNoEncontrado("NotaCreditoVentaBorradorId"));
        }

        var borrador = (await unitOfWork.Repository<NotaCreditoVentaBorrador>().FirstOrDefaultAsync(
            x => x.Id == request.NotaCreditoVentaBorradorId, cancellationToken: cancellationToken))!;

        var original = (await datos.LineasFacturaAsync(borrador.FacturaVentaNumero, cancellationToken))
            .FirstOrDefault(l => l.Id == request.LineaFacturaVentaId);
        if (original is null)
        {
            return Fallo(new Error(
                "nota_credito.linea_factura_invalida",
                $"La línea de factura indicada no pertenece a la factura {borrador.FacturaVentaNumero} del borrador.",
                "LineaFacturaVentaId"));
        }

        var lineasRepo = unitOfWork.Repository<LineaNotaCreditoVentaBorrador>();
        if (await lineasRepo.FirstOrDefaultAsync(
                x => x.NotaCreditoVentaBorradorId == borrador.Id && x.LineaFacturaVentaId == original.Id,
                cancellationToken: cancellationToken) is not null)
        {
            return Fallo(new Error(
                "nota_credito.linea_duplicada",
                $"El borrador ya acredita la línea {original.NumeroLinea} de la factura: modifique esa línea.",
                "LineaFacturaVentaId"));
        }

        var acreditadas = await datos.CantidadesAcreditadasAsync(borrador.FacturaVentaNumero, cancellationToken);
        var calculo = await LineaNotaCreditoVentaReglas.ValidarAsync(
            unitOfWork, conversion, original, acreditadas.GetValueOrDefault(original.Id), request.Cantidad, request.DevolverInventario,
            cancellationToken);
        if (!calculo.TryObtenerValor(out var valores))
        {
            return Result<LineaNotaCreditoVentaBorradorResponse>.Fallo(calculo);
        }

        var entity = new LineaNotaCreditoVentaBorrador { NotaCreditoVentaBorradorId = borrador.Id };
        valores.Aplicar(entity);
        await lineasRepo.AddAsync(entity, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result<LineaNotaCreditoVentaBorradorResponse>.Exito((await readRepository.GetLineaByIdAsync(entity.Id, cancellationToken))!);
    }

    private static Result<LineaNotaCreditoVentaBorradorResponse> Fallo(Error error) =>
        Result<LineaNotaCreditoVentaBorradorResponse>.Fallo(error);
}

/// <summary>
/// Modificación de la cantidad y la devolución de una línea (404 si no existe) bajo el <c>FOR UPDATE</c> de su borrador; mismas
/// reglas que el alta. <c>Xmin</c> desactualizado -&gt; 409.
/// </summary>
public sealed class UpdateLineaNotaCreditoVentaBorradorCommandHandler(
    IUnitOfWork unitOfWork,
    INotaCreditoVentaDatos datos,
    IConversionUnidadMedidaService conversion,
    INotaCreditoVentaBorradorReadRepository readRepository)
    : IRequestHandler<UpdateLineaNotaCreditoVentaBorradorCommand, Result<LineaNotaCreditoVentaBorradorResponse>>
{
    public async Task<Result<LineaNotaCreditoVentaBorradorResponse>> Handle(
        UpdateLineaNotaCreditoVentaBorradorCommand request, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<LineaNotaCreditoVentaBorrador>();
        var entity = await repository.FirstOrDefaultAsync(x => x.Id == request.Id, asTracking: true, cancellationToken: cancellationToken);
        if (entity is null)
        {
            return Result<LineaNotaCreditoVentaBorradorResponse>.Fallo(NotaCreditoVentaErrores.LineaNoEncontrada());
        }

        await using var transaccion = await unitOfWork.BeginTransactionAsync(cancellationToken);

        if (!await datos.BloquearBorradorAsync(entity.NotaCreditoVentaBorradorId, cancellationToken))
        {
            return Result<LineaNotaCreditoVentaBorradorResponse>.Fallo(NotaCreditoVentaErrores.LineaNoEncontrada());
        }

        var borrador = (await unitOfWork.Repository<NotaCreditoVentaBorrador>().FirstOrDefaultAsync(
            x => x.Id == entity.NotaCreditoVentaBorradorId, cancellationToken: cancellationToken))!;
        var original = (await datos.LineasFacturaAsync(borrador.FacturaVentaNumero, cancellationToken)).Single(l => l.Id == entity.LineaFacturaVentaId);
        var acreditadas = await datos.CantidadesAcreditadasAsync(borrador.FacturaVentaNumero, cancellationToken);
        var calculo = await LineaNotaCreditoVentaReglas.ValidarAsync(
            unitOfWork, conversion, original, acreditadas.GetValueOrDefault(original.Id), request.Cantidad, request.DevolverInventario,
            cancellationToken);
        if (!calculo.TryObtenerValor(out var valores))
        {
            return Result<LineaNotaCreditoVentaBorradorResponse>.Fallo(calculo);
        }

        repository.EstablecerVersionOriginal(entity, request.Xmin);
        valores.Aplicar(entity);
        repository.Update(entity);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result<LineaNotaCreditoVentaBorradorResponse>.Exito((await readRepository.GetLineaByIdAsync(entity.Id, cancellationToken))!);
    }
}

/// <summary>Borrado lógico de una línea bajo el <c>FOR UPDATE</c> de su borrador.</summary>
public sealed class DeleteLineaNotaCreditoVentaBorradorCommandHandler(IUnitOfWork unitOfWork, INotaCreditoVentaDatos datos)
    : IRequestHandler<DeleteLineaNotaCreditoVentaBorradorCommand, Result>
{
    public async Task<Result> Handle(DeleteLineaNotaCreditoVentaBorradorCommand request, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<LineaNotaCreditoVentaBorrador>();
        var entity = await repository.GetByIdAsync([request.Id], cancellationToken);
        if (entity is null)
        {
            return Result.Fallo(NotaCreditoVentaErrores.LineaNoEncontrada());
        }

        await using var transaccion = await unitOfWork.BeginTransactionAsync(cancellationToken);

        if (!await datos.BloquearBorradorAsync(entity.NotaCreditoVentaBorradorId, cancellationToken))
        {
            return Result.Fallo(NotaCreditoVentaErrores.LineaNoEncontrada());
        }

        repository.Remove(entity);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Exito();
    }
}
