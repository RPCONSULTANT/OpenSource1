using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.GruposClienteContable.Commands;
using OpenSource1.Application.Features.GruposClienteContable.Dtos;
using OpenSource1.Application.Features.GruposContables;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Contabilidad;

namespace OpenSource1.Application.Features.GruposClienteContable.Handlers;

public sealed class CreateGrupoClienteContableCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<CreateGrupoClienteContableCommand, Result<GrupoClienteContableResponse>>
{
    public async Task<Result<GrupoClienteContableResponse>> Handle(CreateGrupoClienteContableCommand request, CancellationToken cancellationToken)
    {
        var errores = GrupoContableValidator.Validar(request.Codigo, request.Descripcion, GrupoClienteContableReglas.Prefijo);
        if (errores.Count > 0)
        {
            return Result<GrupoClienteContableResponse>.Fallo([.. errores]);
        }

        var cxc = await GrupoClienteContableReglas.ValidarCuentaAsync(
            unitOfWork, request.CuentaCxCId, "CuentaCxCId", "por cobrar", cancellationToken);
        if (!cxc.TryObtenerValor(out var cuentaCxC))
        {
            return Result<GrupoClienteContableResponse>.Fallo(cxc);
        }

        CuentaContable? cuentaDescuento = null;
        if (request.CuentaDescuentoId is { } descuentoId)
        {
            var descuento = await GrupoClienteContableReglas.ValidarCuentaAsync(
                unitOfWork, descuentoId, "CuentaDescuentoId", "de descuentos", cancellationToken);
            if (!descuento.TryObtenerValor(out cuentaDescuento))
            {
                return Result<GrupoClienteContableResponse>.Fallo(descuento);
            }
        }

        CuentaContable? cuentaInteres = null;
        if (request.CuentaInteresId is { } interesId)
        {
            var interes = await GrupoClienteContableReglas.ValidarCuentaAsync(
                unitOfWork, interesId, "CuentaInteresId", "de intereses", cancellationToken);
            if (!interes.TryObtenerValor(out cuentaInteres))
            {
                return Result<GrupoClienteContableResponse>.Fallo(interes);
            }
        }

        var entity = new GrupoClienteContable
        {
            Codigo = GrupoContableValidator.NormalizarCodigo(request.Codigo),
            Descripcion = request.Descripcion.Trim(),
            CuentaCxCId = cuentaCxC.Id,
            CuentaDescuentoId = cuentaDescuento?.Id,
            CuentaInteresId = cuentaInteres?.Id
        };

        var repository = unitOfWork.Repository<GrupoClienteContable>();
        await repository.AddAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<GrupoClienteContableResponse>.Exito(GrupoClienteContableReglas.ToResponse(
            entity, cuentaCxC, cuentaDescuento, cuentaInteres, repository.ObtenerVersionActual(entity)));
    }
}
