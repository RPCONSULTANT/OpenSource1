using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Almacenes.Commands;
using OpenSource1.Application.Features.Almacenes.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;

namespace OpenSource1.Application.Features.Almacenes.Handlers;

/// <summary>
/// Modificación de un almacén. <c>Codigo</c> y <c>Nombre</c> son de reemplazo completo; los demás
/// campos se resuelven contra los valores guardados antes de validar (ver <see cref="UpdateAlmacenCommand"/>).
/// </summary>
public sealed class UpdateAlmacenCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateAlmacenCommand, Result<AlmacenResponse>>
{
    public async Task<Result<AlmacenResponse>> Handle(UpdateAlmacenCommand request, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<Almacen>();

        // Consulta con seguimiento (no Find): defensa en profundidad, igual que SocioNegocio -
        // siempre pasa por el filtro global de soft delete.
        var entity = await repository.FirstOrDefaultAsync(x => x.Id == request.Id, asTracking: true, cancellationToken: cancellationToken);
        if (entity is null)
        {
            return Result<AlmacenResponse>.Fallo(new Error(
                "almacen.no_encontrado", "No se encontró el almacén solicitado.", "Id"));
        }

        var direccionLinea1 = request.DireccionLinea1 is null ? entity.DireccionLinea1 : CreateAlmacenCommandHandler.Normalizar(request.DireccionLinea1);
        var direccionLinea2 = request.DireccionLinea2 is null ? entity.DireccionLinea2 : CreateAlmacenCommandHandler.Normalizar(request.DireccionLinea2);
        var ciudad = request.Ciudad is null ? entity.Ciudad : CreateAlmacenCommandHandler.Normalizar(request.Ciudad);
        var paisCodigo = request.PaisCodigo is null ? entity.PaisCodigo : CreateAlmacenCommandHandler.NormalizarPais(request.PaisCodigo);

        var errores = AlmacenValidator.Validar(request.Codigo, request.Nombre, direccionLinea1, direccionLinea2, ciudad, paisCodigo);
        if (errores.Count > 0)
        {
            return Result<AlmacenResponse>.Fallo([.. errores]);
        }

        // Siempre debe existir un predeterminado: no se puede desmarcar el actual sin marcar otro
        // (eso se hace con un PUT distinto que marque EsPredeterminado = true en OTRO almacén).
        if (entity.EsPredeterminado && request.EsPredeterminado == false)
        {
            return Result<AlmacenResponse>.Fallo(new Error(
                "almacen.predeterminado_requerido",
                "Siempre debe existir un almacén predeterminado; marque otro como predeterminado en su lugar.",
                "EsPredeterminado"));
        }

        entity.Codigo = AlmacenValidator.NormalizarCodigo(request.Codigo);
        entity.Nombre = request.Nombre.Trim();
        entity.DireccionLinea1 = direccionLinea1;
        entity.DireccionLinea2 = direccionLinea2;
        entity.Ciudad = ciudad;
        entity.PaisCodigo = paisCodigo;
        entity.Bloqueado = request.Bloqueado ?? entity.Bloqueado;

        var pasaAPredeterminado = !entity.EsPredeterminado && request.EsPredeterminado == true;

        if (pasaAPredeterminado)
        {
            // Mismo patrón que el alta: desmarcar el predeterminado actual en la misma transacción
            // antes de marcar este.
            await using var transaccion = await unitOfWork.BeginTransactionAsync(cancellationToken);

            var actual = await repository.FirstOrDefaultAsync(
                x => x.EsPredeterminado && x.Id != entity.Id, asTracking: true, cancellationToken: cancellationToken);
            if (actual is not null)
            {
                actual.EsPredeterminado = false;
                repository.Update(actual);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            entity.EsPredeterminado = true;
            repository.Update(entity);
            await unitOfWork.CommitAsync(cancellationToken);
        }
        else
        {
            repository.Update(entity);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result<AlmacenResponse>.Exito(CreateAlmacenCommandHandler.ToResponse(entity));
    }
}
