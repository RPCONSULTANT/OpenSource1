using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Commands;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Inventario;

namespace OpenSource1.Application.Features.DiariosInventario.Lotes.Handlers;

public sealed class CreateLoteDiarioCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<CreateLoteDiarioCommand, Result<LoteDiarioResponse>>
{
    public async Task<Result<LoteDiarioResponse>> Handle(CreateLoteDiarioCommand request, CancellationToken cancellationToken)
    {
        var errores = LoteDiarioValidator.Validar(request.Codigo, request.Nombre);
        if (errores.Count > 0)
        {
            return Result<LoteDiarioResponse>.Fallo([.. errores]);
        }

        var plantilla = await unitOfWork.Repository<PlantillaDiario>().FirstOrDefaultAsync(
            x => x.Id == request.PlantillaDiarioId, cancellationToken: cancellationToken);
        if (plantilla is null)
        {
            // Código sin sufijo ".no_encontrado" a propósito: PlantillaDiarioId es un dato del cuerpo (400), no un
            // recurso de la URL inexistente.
            return Result<LoteDiarioResponse>.Fallo(new Error(
                "diario.plantilla_invalida", "La plantilla de diario indicada no existe.", "PlantillaDiarioId"));
        }

        if (request.SerieId is { } serieId && serieId != Guid.Empty)
        {
            var serie = await unitOfWork.Repository<Serie>().FirstOrDefaultAsync(
                x => x.Id == serieId, cancellationToken: cancellationToken);
            if (serie is null || !LoteDiarioValidator.EsSerieDeDiarioValida(serie.Codigo))
            {
                return Result<LoteDiarioResponse>.Fallo(new Error(
                    "diario.serie_invalida", "La serie indicada no existe o no es una serie de diarios de inventario.", "SerieId"));
            }
        }

        var entity = new LoteDiario
        {
            PlantillaDiarioId = request.PlantillaDiarioId,
            Codigo = LoteDiarioValidator.NormalizarCodigo(request.Codigo),
            Nombre = request.Nombre.Trim(),
            SerieId = request.SerieId == Guid.Empty ? null : request.SerieId,
            Bloqueado = request.Bloqueado
        };

        var repository = unitOfWork.Repository<LoteDiario>();
        await repository.AddAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<LoteDiarioResponse>.Exito(ToResponse(entity, numeroLineas: 0, repository.ObtenerVersionActual(entity)));
    }

    public static LoteDiarioResponse ToResponse(LoteDiario x, int numeroLineas, long xmin) => new()
    {
        Id = x.Id,
        PlantillaDiarioId = x.PlantillaDiarioId,
        Codigo = x.Codigo,
        Nombre = x.Nombre,
        SerieId = x.SerieId,
        Bloqueado = x.Bloqueado,
        NumeroLineas = numeroLineas,
        Xmin = xmin,
        CreatedAtUtc = x.CreatedAtUtc.UtcDateTime,
        UpdatedAtUtc = x.UpdatedAtUtc?.UtcDateTime,
        CreatedBy = x.CreatedBy,
        UpdatedBy = x.UpdatedBy
    };
}
