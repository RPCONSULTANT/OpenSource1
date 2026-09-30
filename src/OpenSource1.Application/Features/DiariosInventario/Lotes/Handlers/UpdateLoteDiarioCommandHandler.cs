using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Commands;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Inventario;

namespace OpenSource1.Application.Features.DiariosInventario.Lotes.Handlers;

/// <summary>
/// Modificación de un lote de diario. <c>PlantillaDiarioId</c> es inmutable (ver <see cref="Commands.CreateLoteDiarioCommand"/>).
/// </summary>
public sealed class UpdateLoteDiarioCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateLoteDiarioCommand, Result<LoteDiarioResponse>>
{
    public async Task<Result<LoteDiarioResponse>> Handle(UpdateLoteDiarioCommand request, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<LoteDiario>();

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
        if (serieId is { } serieIdInformada && serieId != entity.SerieId)
        {
            var serie = await unitOfWork.Repository<Serie>().FirstOrDefaultAsync(
                x => x.Id == serieIdInformada, cancellationToken: cancellationToken);
            if (serie is null || !LoteDiarioValidator.EsSerieDeDiarioValida(serie))
            {
                return Result<LoteDiarioResponse>.Fallo(new Error(
                    "diario.serie_invalida", "La serie indicada no existe, no es de diarios de inventario o está inactiva.", "SerieId"));
            }
        }

        repository.EstablecerVersionOriginal(entity, request.Xmin);

        entity.Codigo = LoteDiarioValidator.NormalizarCodigo(request.Codigo);
        entity.Nombre = request.Nombre.Trim();
        entity.SerieId = serieId;
        entity.Bloqueado = request.Bloqueado ?? entity.Bloqueado;

        repository.Update(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var numeroLineas = (await unitOfWork.Repository<LineaDiario>().ListAsync(
            x => x.LoteDiarioId == entity.Id, cancellationToken)).Count;

        return Result<LoteDiarioResponse>.Exito(
            CreateLoteDiarioCommandHandler.ToResponse(entity, numeroLineas, repository.ObtenerVersionActual(entity)));
    }
}
