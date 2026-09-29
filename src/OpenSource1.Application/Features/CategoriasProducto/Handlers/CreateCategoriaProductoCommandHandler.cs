using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.CategoriasProducto.Commands;
using OpenSource1.Application.Features.CategoriasProducto.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;

namespace OpenSource1.Application.Features.CategoriasProducto.Handlers;

public sealed class CreateCategoriaProductoCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<CreateCategoriaProductoCommand, Result<CategoriaProductoResponse>>
{
    public async Task<Result<CategoriaProductoResponse>> Handle(CreateCategoriaProductoCommand request, CancellationToken cancellationToken)
    {
        var errores = CategoriaProductoValidator.Validar(request.Codigo, request.Nombre);

        if (errores.Count > 0)
        {
            return Result<CategoriaProductoResponse>.Fallo([.. errores]);
        }

        var repository = unitOfWork.Repository<CategoriaProducto>();
        var (error, padre) = await CategoriaProductoJerarquia.ValidarPadreAsync(
            repository, categoriaId: null, request.CategoriaPadreId, cancellationToken);

        if (error is not null)
        {
            return Result<CategoriaProductoResponse>.Fallo(error.Value);
        }

        var entity = new CategoriaProducto
        {
            Codigo = CategoriaProductoValidator.NormalizarCodigo(request.Codigo),
            Nombre = request.Nombre.Trim(),
            CategoriaPadreId = request.CategoriaPadreId
        };

        await repository.AddAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CategoriaProductoResponse>.Exito(ToResponse(entity, padre));
    }

    public static CategoriaProductoResponse ToResponse(CategoriaProducto x, CategoriaProducto? padre) => new()
    {
        Id = x.Id,
        Codigo = x.Codigo,
        Nombre = x.Nombre,
        CategoriaPadreId = x.CategoriaPadreId,
        CategoriaPadreNombre = padre?.Nombre,
        CreatedAtUtc = x.CreatedAtUtc.UtcDateTime,
        UpdatedAtUtc = x.UpdatedAtUtc?.UtcDateTime,
        CreatedBy = x.CreatedBy,
        UpdatedBy = x.UpdatedBy
    };
}
