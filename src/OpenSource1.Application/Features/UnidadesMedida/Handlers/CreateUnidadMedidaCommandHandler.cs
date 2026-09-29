using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.UnidadesMedida.Commands;
using OpenSource1.Application.Features.UnidadesMedida.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;

namespace OpenSource1.Application.Features.UnidadesMedida.Handlers;

public sealed class CreateUnidadMedidaCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<CreateUnidadMedidaCommand, Result<UnidadMedidaResponse>>
{
    public async Task<Result<UnidadMedidaResponse>> Handle(CreateUnidadMedidaCommand request, CancellationToken cancellationToken)
    {
        var errores = UnidadMedidaValidator.Validar(request.Codigo, request.Nombre, request.Decimales);

        if (errores.Count > 0)
        {
            return Result<UnidadMedidaResponse>.Fallo([.. errores]);
        }

        var entity = new UnidadMedida
        {
            Codigo = UnidadMedidaValidator.NormalizarCodigo(request.Codigo),
            Nombre = request.Nombre.Trim(),
            Decimales = request.Decimales
        };

        await unitOfWork.Repository<UnidadMedida>().AddAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<UnidadMedidaResponse>.Exito(ToResponse(entity));
    }

    public static UnidadMedidaResponse ToResponse(UnidadMedida x) => new()
    {
        Id = x.Id,
        Codigo = x.Codigo,
        Nombre = x.Nombre,
        Decimales = x.Decimales,
        CreatedAtUtc = x.CreatedAtUtc.UtcDateTime,
        UpdatedAtUtc = x.UpdatedAtUtc?.UtcDateTime,
        CreatedBy = x.CreatedBy,
        UpdatedBy = x.UpdatedBy
    };
}
