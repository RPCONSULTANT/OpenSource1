using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.CuentasContables.Commands;
using OpenSource1.Application.Features.CuentasContables.Dtos;
using OpenSource1.Application.Services.Contabilidad;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.CuentasContables.Handlers;

/// <summary>
/// Modificación de una cuenta contable. Cambiar <c>TipoCuenta</c> de <see cref="TipoCuentaContable.Posteo"/> a otro
/// valor mientras la cuenta está en uso (movimientos contables, setups o grupos de cliente — Tasks 5.3-5.5) se
/// rechaza con 409 <c>cuenta_contable.conflicto</c>; sin uso, se permite.
/// </summary>
public sealed class UpdateCuentaContableCommandHandler(IUnitOfWork unitOfWork, ICuentaContableUsoService usoService)
    : IRequestHandler<UpdateCuentaContableCommand, Result<CuentaContableResponse>>
{
    public async Task<Result<CuentaContableResponse>> Handle(UpdateCuentaContableCommand request, CancellationToken cancellationToken)
    {
        // Misma transacción y bloqueo FOR UPDATE que el borrado (ver DeleteCuentaContableCommandHandler): sacar la cuenta de
        // Posteo no puede colarse entre la guarda de uso y un IRegistroContable concurrente.
        await using var transaccion = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await usoService.BloquearAsync(request.Id, cancellationToken);

        var repository = unitOfWork.Repository<CuentaContable>();

        // Consulta con seguimiento (no Find): defensa en profundidad, igual que Almacen/SocioNegocio -
        // siempre pasa por el filtro global de soft delete.
        var entity = await repository.FirstOrDefaultAsync(x => x.Id == request.Id, asTracking: true, cancellationToken: cancellationToken);
        if (entity is null)
        {
            return Result<CuentaContableResponse>.Fallo(new Error(
                "cuenta_contable.no_encontrado", "No se encontró la cuenta contable solicitada.", "Id"));
        }

        var errores = CuentaContableValidator.Validar(
            request.Numero, request.Nombre, request.TipoCuenta, request.TipoResultado,
            request.Sangria ?? entity.Sangria);

        if (errores.Count > 0)
        {
            return Result<CuentaContableResponse>.Fallo([.. errores]);
        }

        // Sale de Posteo (deja de poder recibir movimientos directos): si algo ya la usa, el cambio la dejaría
        // inconsistente con lo que ya la referencia.
        var dejaDeSerPosteo = entity.TipoCuenta == TipoCuentaContable.Posteo && request.TipoCuenta != TipoCuentaContable.Posteo;
        if (dejaDeSerPosteo && await usoService.EstaEnUsoAsync(entity.Id, cancellationToken))
        {
            return Result<CuentaContableResponse>.Fallo(new Error(
                "cuenta_contable.conflicto",
                "No se puede cambiar el tipo de la cuenta porque está en uso.",
                "TipoCuenta"));
        }

        repository.EstablecerVersionOriginal(entity, request.Xmin);

        entity.Numero = CuentaContableValidator.NormalizarNumero(request.Numero);
        entity.Nombre = request.Nombre.Trim();
        entity.TipoCuenta = request.TipoCuenta;
        entity.TipoResultado = request.TipoResultado;
        entity.PosteoDirecto = request.PosteoDirecto ?? entity.PosteoDirecto;
        entity.Bloqueada = request.Bloqueada ?? entity.Bloqueada;
        entity.Sangria = request.Sangria ?? entity.Sangria;

        repository.Update(entity);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result<CuentaContableResponse>.Exito(
            CreateCuentaContableCommandHandler.ToResponse(entity, repository.ObtenerVersionActual(entity)));
    }
}
