using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.SetupsContables.Commands;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.SetupsContables.Handlers;

/// <summary>
/// Borrado lógico de una fila de setup. Nadie referencia un setup (los documentos guardan clasificadores, no filas de setup:
/// D4), así que no hay guarda de uso; la combinación queda libre para una fila nueva (índice único parcial).
/// </summary>
public sealed class DeleteSetupContableCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<DeleteSetupContableCommand, Result>
{
    public Task<Result> Handle(DeleteSetupContableCommand request, CancellationToken cancellationToken) => request.Tipo switch
    {
        TipoSetupContable.General => BorrarAsync<SetupContableGeneral>(request.Id, SetupGeneralReglas.Nombre, cancellationToken),
        TipoSetupContable.Iva => BorrarAsync<SetupIva>(request.Id, SetupIvaReglas.Nombre, cancellationToken),
        TipoSetupContable.Inventario => BorrarAsync<SetupInventario>(request.Id, SetupInventarioReglas.Nombre, cancellationToken),
        _ => Task.FromResult(Result.Fallo(new Error("setup_contable.tipo_invalido", "El tipo de setup contable no es válido.", "Tipo")))
    };

    private async Task<Result> BorrarAsync<TSetup>(Guid id, string nombre, CancellationToken cancellationToken) where TSetup : BaseEntity
    {
        var repository = unitOfWork.Repository<TSetup>();
        var entity = await repository.FirstOrDefaultAsync(x => x.Id == id, asTracking: true, cancellationToken: cancellationToken);
        if (entity is null)
        {
            return Result.Fallo(SetupContableReglas.NoEncontrado(nombre));
        }

        repository.Remove(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Exito();
    }
}
