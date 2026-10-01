using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.NotasCreditoVenta.Borradores.Commands;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Ventas;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.NotasCreditoVenta.Borradores.Handlers;

/// <summary>Borrado lógico del borrador y de todas sus líneas bajo su <c>FOR UPDATE</c>; un borrador Posteada no se borra (409).</summary>
public sealed class DeleteNotaCreditoVentaBorradorCommandHandler(IUnitOfWork unitOfWork, INotaCreditoVentaDatos datos, IUsuarioActual usuario)
    : IRequestHandler<DeleteNotaCreditoVentaBorradorCommand, Result>
{
    public async Task<Result> Handle(DeleteNotaCreditoVentaBorradorCommand request, CancellationToken cancellationToken)
    {
        await using var transaccion = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var estado = await datos.BloquearBorradorAsync(request.Id, cancellationToken);
        if (estado is null)
        {
            return Result.Fallo(NotaCreditoVentaErrores.BorradorNoEncontrado());
        }

        if (estado == EstadoNotaCreditoBorrador.Posteada)
        {
            return Result.Fallo(NotaCreditoVentaErrores.Posteada());
        }

        var repository = unitOfWork.Repository<NotaCreditoVentaBorrador>();
        var entity = (await repository.GetByIdAsync([request.Id], cancellationToken))!;

        var nombre = string.IsNullOrWhiteSpace(usuario.Nombre) ? "system" : usuario.Nombre;
        await datos.BorrarLineasAsync(request.Id, nombre.Length <= 100 ? nombre : nombre[..100], cancellationToken);

        repository.Remove(entity);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Exito();
    }
}
