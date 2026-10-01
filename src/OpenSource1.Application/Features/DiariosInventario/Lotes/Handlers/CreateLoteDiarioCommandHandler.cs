using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Commands;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Dtos;
using OpenSource1.Application.Features.Series;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Inventario;

namespace OpenSource1.Application.Features.DiariosInventario.Lotes.Handlers;

/// <summary>
/// Alta de un lote de diario. La serie elegida se lee <c>FOR SHARE</c> dentro de la transacción del alta antes de validar tipo y
/// Activa (mismo bloqueo que el motor): un cambio de tipo o una desactivación concurrentes no se cuelan entre la validación y el
/// commit.
/// </summary>
public sealed class CreateLoteDiarioCommandHandler(IUnitOfWork unitOfWork, ISerieReadRepository series)
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

        // Sin CommitAsync, salir del "await using" deshace la transacción.
        await using var transaccion = await unitOfWork.BeginTransactionAsync(cancellationToken);
        if (request.SerieId is { } serieId && serieId != Guid.Empty
            && !LoteDiarioValidator.EsSerieDeDiarioValida(await series.LeerSerieCompartidaAsync(serieId, cancellationToken)))
        {
            return Result<LoteDiarioResponse>.Fallo(LoteDiarioValidator.SerieInvalida());
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
        await unitOfWork.CommitAsync(cancellationToken);

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
