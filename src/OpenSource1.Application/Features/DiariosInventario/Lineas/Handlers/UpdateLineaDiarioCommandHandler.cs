using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Commands;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Dtos;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Inventario;

namespace OpenSource1.Application.Features.DiariosInventario.Lineas.Handlers;

/// <summary>
/// Modificación de una línea de diario: reemplazo completo (ver <see cref="UpdateLineaDiarioCommand"/>).
/// <c>LoteDiarioId</c> y <c>NumeroLinea</c> se conservan de la fila existente (inmutables).
/// </summary>
public sealed class UpdateLineaDiarioCommandHandler(IUnitOfWork unitOfWork, IConversionUnidadMedidaService conversion)
    : IRequestHandler<UpdateLineaDiarioCommand, Result<LineaDiarioResponse>>
{
    public async Task<Result<LineaDiarioResponse>> Handle(UpdateLineaDiarioCommand request, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<LineaDiario>();
        var entity = await repository.FirstOrDefaultAsync(x => x.Id == request.Id, asTracking: true, cancellationToken: cancellationToken);
        if (entity is null)
        {
            return Result<LineaDiarioResponse>.Fallo(new Error(
                "diario_linea.no_encontrado", "No se encontró la línea de diario solicitada.", "Id"));
        }

        var datos = new LineaDiarioDatos(
            entity.LoteDiarioId, request.FechaRegistro, request.FechaDocumento, CreateLineaDiarioCommandHandler.Normalizar(request.NumeroDocumento),
            request.TipoMovimiento, request.ProductoId, request.AlmacenId, request.AlmacenDestinoId, request.UnidadMedidaId,
            request.Cantidad, request.CostoUnitario, CreateLineaDiarioCommandHandler.Normalizar(request.Descripcion));

        var calculo = await LineaDiarioReglas.ValidarYCalcularAsync(unitOfWork, conversion, datos, cancellationToken);
        if (!calculo.TryObtenerValor(out var valor))
        {
            return Result<LineaDiarioResponse>.Fallo(calculo);
        }

        repository.EstablecerVersionOriginal(entity, request.Xmin);

        entity.FechaRegistro = request.FechaRegistro;
        entity.FechaDocumento = request.FechaDocumento;
        entity.NumeroDocumento = datos.NumeroDocumento;
        entity.TipoMovimiento = request.TipoMovimiento;
        entity.ProductoId = request.ProductoId;
        entity.AlmacenId = request.AlmacenId;
        entity.AlmacenDestinoId = request.AlmacenDestinoId;
        entity.UnidadMedidaId = request.UnidadMedidaId;
        entity.CantidadPorUnidadMedida = valor.CantidadPorUnidadMedida;
        entity.Cantidad = request.Cantidad;
        entity.CostoUnitario = request.CostoUnitario;
        entity.ImporteCosto = valor.ImporteCosto;
        entity.Descripcion = datos.Descripcion;

        repository.Update(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<LineaDiarioResponse>.Exito(
            CreateLineaDiarioCommandHandler.ToResponse(entity, valor, repository.ObtenerVersionActual(entity)));
    }
}
