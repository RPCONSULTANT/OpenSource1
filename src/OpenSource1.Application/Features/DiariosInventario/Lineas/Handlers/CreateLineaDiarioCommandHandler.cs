using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Commands;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Dtos;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Inventario;

namespace OpenSource1.Application.Features.DiariosInventario.Lineas.Handlers;

public sealed class CreateLineaDiarioCommandHandler(IUnitOfWork unitOfWork, IConversionUnidadMedidaService conversion)
    : IRequestHandler<CreateLineaDiarioCommand, Result<LineaDiarioResponse>>
{
    /// <summary>Tope de líneas por lote (brief 4.2): un lote es un borrador chico, no un libro.</summary>
    public const int MaximoLineasPorLote = 1000;

    public async Task<Result<LineaDiarioResponse>> Handle(CreateLineaDiarioCommand request, CancellationToken cancellationToken)
    {
        var datos = new LineaDiarioDatos(
            request.LoteDiarioId, request.FechaRegistro, request.FechaDocumento, Normalizar(request.NumeroDocumento),
            request.TipoMovimiento, request.ProductoId, request.AlmacenId, request.AlmacenDestinoId, request.UnidadMedidaId,
            request.Cantidad, request.CostoUnitario, Normalizar(request.Descripcion));

        var calculo = await LineaDiarioReglas.ValidarYCalcularAsync(unitOfWork, conversion, datos, cancellationToken);
        if (!calculo.TryObtenerValor(out var valor))
        {
            return Result<LineaDiarioResponse>.Fallo(calculo);
        }

        var lineasRepo = unitOfWork.Repository<LineaDiario>();
        var lineasDelLote = await lineasRepo.ListAsync(x => x.LoteDiarioId == request.LoteDiarioId, cancellationToken);
        if (lineasDelLote.Count >= MaximoLineasPorLote)
        {
            return Result<LineaDiarioResponse>.Fallo(new Error(
                "diario.lote_limite_lineas", $"El lote ya tiene el máximo de {MaximoLineasPorLote} líneas.", "LoteDiarioId"));
        }

        var numeroLinea = (lineasDelLote.Count == 0 ? 0 : lineasDelLote.Max(x => x.NumeroLinea)) + 10000;

        var entity = new LineaDiario
        {
            LoteDiarioId = request.LoteDiarioId,
            NumeroLinea = numeroLinea,
            FechaRegistro = request.FechaRegistro,
            FechaDocumento = request.FechaDocumento,
            NumeroDocumento = datos.NumeroDocumento,
            TipoMovimiento = request.TipoMovimiento,
            ProductoId = request.ProductoId,
            AlmacenId = request.AlmacenId,
            AlmacenDestinoId = request.AlmacenDestinoId,
            UnidadMedidaId = request.UnidadMedidaId,
            CantidadPorUnidadMedida = valor.CantidadPorUnidadMedida,
            Cantidad = request.Cantidad,
            CostoUnitario = request.CostoUnitario,
            ImporteCosto = valor.ImporteCosto,
            Descripcion = datos.Descripcion
        };

        await lineasRepo.AddAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<LineaDiarioResponse>.Exito(ToResponse(entity, valor, lineasRepo.ObtenerVersionActual(entity)));
    }

    public static LineaDiarioResponse ToResponse(LineaDiario x, LineaDiarioCalculo datos, long xmin) => new()
    {
        Id = x.Id,
        LoteDiarioId = x.LoteDiarioId,
        NumeroLinea = x.NumeroLinea,
        FechaRegistro = x.FechaRegistro,
        FechaDocumento = x.FechaDocumento,
        NumeroDocumento = x.NumeroDocumento,
        TipoMovimiento = x.TipoMovimiento,
        ProductoId = x.ProductoId,
        ProductoCodigo = datos.ProductoCodigo,
        ProductoNombre = datos.ProductoNombre,
        AlmacenId = x.AlmacenId,
        AlmacenCodigo = datos.AlmacenCodigo,
        AlmacenDestinoId = x.AlmacenDestinoId,
        AlmacenDestinoCodigo = datos.AlmacenDestinoCodigo,
        UnidadMedidaId = x.UnidadMedidaId,
        UnidadMedidaCodigo = datos.UnidadMedidaCodigo,
        CantidadPorUnidadMedida = x.CantidadPorUnidadMedida,
        Cantidad = x.Cantidad,
        CostoUnitario = x.CostoUnitario,
        ImporteCosto = x.ImporteCosto,
        Descripcion = x.Descripcion,
        Xmin = xmin,
        CreatedAtUtc = x.CreatedAtUtc.UtcDateTime,
        UpdatedAtUtc = x.UpdatedAtUtc?.UtcDateTime,
        CreatedBy = x.CreatedBy,
        UpdatedBy = x.UpdatedBy
    };

    internal static string? Normalizar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
