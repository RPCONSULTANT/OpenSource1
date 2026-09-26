using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.CuentasContables.Commands;
using OpenSource1.Application.Services.Contabilidad;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Contabilidad;

namespace OpenSource1.Application.Features.CuentasContables.Handlers;

/// <summary>
/// Borrado lógico de una cuenta contable. Rechaza el borrado con 409 <c>cuenta_contable.conflicto</c> si la cuenta
/// está en uso (<see cref="ICuentaContableUsoService"/>: movimientos contables, setups o grupos de cliente —
/// guarda preparada en la 5.2 y ampliada por las Tasks 5.3-5.5; desde la 5.3 cubre los grupos de cliente).
/// </summary>
public sealed class DeleteCuentaContableCommandHandler(IUnitOfWork unitOfWork, ICuentaContableUsoService usoService)
    : IRequestHandler<DeleteCuentaContableCommand, Result>
{
    public async Task<Result> Handle(DeleteCuentaContableCommand request, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<CuentaContable>();
        var entity = await repository.GetByIdAsync([request.Id], cancellationToken);

        if (entity is null)
        {
            return Result.Fallo(new Error(
                "cuenta_contable.no_encontrado", "No se encontró la cuenta contable solicitada.", "Id"));
        }

        if (await usoService.EstaEnUsoAsync(request.Id, cancellationToken))
        {
            return Result.Fallo(new Error(
                "cuenta_contable.conflicto",
                "No se puede eliminar la cuenta contable porque está en uso.",
                "Id"));
        }

        repository.Remove(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Exito();
    }
}
