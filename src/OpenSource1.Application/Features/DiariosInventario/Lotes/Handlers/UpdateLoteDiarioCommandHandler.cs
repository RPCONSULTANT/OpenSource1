using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Commands;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Dtos;
using OpenSource1.Application.Features.Series;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Inventario;

namespace OpenSource1.Application.Features.DiariosInventario.Lotes.Handlers;

/// <summary>
/// Modificación de un lote de diario. <c>PlantillaDiarioId</c> es inmutable (ver <see cref="Commands.CreateLoteDiarioCommand"/>).
/// Una serie nueva se lee <c>FOR SHARE</c> dentro de la transacción antes de validar tipo y Activa (mismo bloqueo que el motor).
/// </summary>
public sealed class UpdateLoteDiarioCommandHandler(IUnitOfWork unitOfWork, ISerieReadRepository series)
    : IRequestHandler<UpdateLoteDiarioCommand, Result<LoteDiarioResponse>>
{
    public async Task<Result<LoteDiarioResponse>> Handle(UpdateLoteDiarioCommand request, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<LoteDiario>();

        // Sin CommitAsync, salir del "await using" deshace la transacción.
        await using var transaccion = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var entity = await repository.FirstOrDefaultAsync(x => x.Id == request.Id, asTracking: true, cancellationToken: cancellationToken);
        if (entity is null)
        {
            return Result<LoteDiarioResponse>.Fallo(new Error(
                "diario_lote.no_encontrado", "No se encontró el lote de diario solicitado.", "Id"));
        }

        var errores = LoteDiarioValidator.Validar(request.Codigo, request.Nombre);
        if (errores.Count > 0)
        {
            return Result<LoteDiarioResponse>.Fallo([.. errores]);
        }

        // null = conservar; Guid.Empty = limpiar (usar la de la plantilla); otro valor = usarlo (validado contra BD).
        var serieId = request.SerieId is null ? entity.SerieId : (request.SerieId == Guid.Empty ? null : request.SerieId);
        if (serieId is { } serieIdInformada && serieId != entity.SerieId
            && !LoteDiarioValidator.EsSerieDeDiarioValida(await series.LeerSerieCompartidaAsync(serieIdInformada, cancellationToken)))
        {
            return Result<LoteDiarioResponse>.Fallo(LoteDiarioValidator.SerieInvalida());
        }

        repository.EstablecerVersionOriginal(entity, request.Xmin);

        entity.Codigo = LoteDiarioValidator.NormalizarCodigo(request.Codigo);
        entity.Nombre = request.Nombre.Trim();
        entity.SerieId = serieId;
        entity.Bloqueado = request.Bloqueado ?? entity.Bloqueado;

        // Dentro de la transacción: tras el commit, EF sigue enlazado a la transacción ya completada.
        var numeroLineas = (await unitOfWork.Repository<LineaDiario>().ListAsync(
            x => x.LoteDiarioId == entity.Id, cancellationToken)).Count;

        repository.Update(entity);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result<LoteDiarioResponse>.Exito(
            CreateLoteDiarioCommandHandler.ToResponse(entity, numeroLineas, repository.ObtenerVersionActual(entity)));
    }
}
