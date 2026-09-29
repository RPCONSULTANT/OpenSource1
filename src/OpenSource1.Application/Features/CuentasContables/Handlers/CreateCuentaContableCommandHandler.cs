using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.CuentasContables.Commands;
using OpenSource1.Application.Features.CuentasContables.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Contabilidad;

namespace OpenSource1.Application.Features.CuentasContables.Handlers;

public sealed class CreateCuentaContableCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<CreateCuentaContableCommand, Result<CuentaContableResponse>>
{
    public async Task<Result<CuentaContableResponse>> Handle(CreateCuentaContableCommand request, CancellationToken cancellationToken)
    {
        var errores = CuentaContableValidator.Validar(
            request.Numero, request.Nombre, request.TipoCuenta, request.TipoResultado, request.Sangria);

        if (errores.Count > 0)
        {
            return Result<CuentaContableResponse>.Fallo([.. errores]);
        }

        var entity = new CuentaContable
        {
            Numero = CuentaContableValidator.NormalizarNumero(request.Numero),
            Nombre = request.Nombre.Trim(),
            TipoCuenta = request.TipoCuenta,
            TipoResultado = request.TipoResultado,
            PosteoDirecto = request.PosteoDirecto,
            Bloqueada = request.Bloqueada,
            Sangria = request.Sangria
        };

        var repository = unitOfWork.Repository<CuentaContable>();
        await repository.AddAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CuentaContableResponse>.Exito(ToResponse(entity, repository.ObtenerVersionActual(entity)));
    }

    public static CuentaContableResponse ToResponse(CuentaContable x, long xmin) => new()
    {
        Id = x.Id,
        Numero = x.Numero,
        Nombre = x.Nombre,
        TipoCuenta = x.TipoCuenta,
        TipoResultado = x.TipoResultado,
        PosteoDirecto = x.PosteoDirecto,
        Bloqueada = x.Bloqueada,
        Sangria = x.Sangria,
        Xmin = xmin,
        CreatedAtUtc = x.CreatedAtUtc.UtcDateTime,
        UpdatedAtUtc = x.UpdatedAtUtc?.UtcDateTime,
        CreatedBy = x.CreatedBy,
        UpdatedBy = x.UpdatedBy
    };
}
