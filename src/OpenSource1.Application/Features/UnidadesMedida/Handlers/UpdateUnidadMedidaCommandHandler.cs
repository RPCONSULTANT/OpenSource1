using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.UnidadesMedida.Commands;
using OpenSource1.Application.Features.UnidadesMedida.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;

namespace OpenSource1.Application.Features.UnidadesMedida.Handlers;

public sealed class UpdateUnidadMedidaCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateUnidadMedidaCommand, Result<UnidadMedidaResponse>>
{
    public async Task<Result<UnidadMedidaResponse>> Handle(UpdateUnidadMedidaCommand request, CancellationToken cancellationToken)
    {
        var errores = UnidadMedidaValidator.Validar(request.Codigo, request.Nombre, request.Decimales);

        if (errores.Count > 0)
        {
            return Result<UnidadMedidaResponse>.Fallo([.. errores]);
        }

        var repository = unitOfWork.Repository<UnidadMedida>();
        var entity = await repository.GetByIdAsync([request.Id], cancellationToken);

        if (entity is null)
        {
            return Result<UnidadMedidaResponse>.Fallo(new Error(
                "unidad_medida.no_encontrado", "No se encontró la unidad de medida solicitada.", "Id"));
        }

        // El Código identifica la unidad en conversiones e informes: cambiarlo mientras un producto la usa (como unidad base o
        // en una equivalencia) dejaría esos datos apuntando a un código distinto del que se registró.
        var nuevoCodigo = UnidadMedidaValidator.NormalizarCodigo(request.Codigo);
        if (nuevoCodigo != entity.Codigo && await UnidadMedidaUso.EstaEnUsoAsync(unitOfWork, entity.Id, cancellationToken))
        {
            return Result<UnidadMedidaResponse>.Fallo(new Error(
                "unidad_medida.en_uso.conflicto",
                "No se puede cambiar el código de la unidad de medida porque está asociada a uno o más productos.", "Codigo"));
        }

        entity.Codigo = nuevoCodigo;
        entity.Nombre = request.Nombre.Trim();
        entity.Decimales = request.Decimales;

        repository.Update(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<UnidadMedidaResponse>.Exito(CreateUnidadMedidaCommandHandler.ToResponse(entity));
    }
}
