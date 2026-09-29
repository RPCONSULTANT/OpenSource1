using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.SetupsContables.Commands;
using OpenSource1.Application.Features.SetupsContables.Dtos;
using OpenSource1.Application.Features.SetupsContables.Queries;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Contabilidad;

namespace OpenSource1.Application.Features.SetupsContables.Handlers;

/// <summary>Alta de una fila de <c>SetupsInventario</c> (almacén × grupo de inventario).</summary>
public sealed class CreateSetupInventarioCommandHandler(IUnitOfWork unitOfWork, ISetupContableReadRepository readRepository)
    : IRequestHandler<CreateSetupInventarioCommand, Result<SetupInventarioResponse>>
{
    public async Task<Result<SetupInventarioResponse>> Handle(CreateSetupInventarioCommand request, CancellationToken cancellationToken)
    {
        var entity = new SetupInventario();
        var aplicado = await SetupInventarioReglas.ValidarYAplicarAsync(
            unitOfWork, entity, alta: true, request.AlmacenId, request.GrupoInventarioId, request.CuentaInventarioId,
            request.CuentaAjusteInventarioId, request.CuentaVariacionCostoId, cancellationToken);
        if (aplicado.EsFallo)
        {
            return Result<SetupInventarioResponse>.Fallo(aplicado);
        }

        await unitOfWork.Repository<SetupInventario>().AddAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<SetupInventarioResponse>.Exito((await readRepository.GetInventarioAsync(entity.Id, cancellationToken))!);
    }
}

public sealed class UpdateSetupInventarioCommandHandler(IUnitOfWork unitOfWork, ISetupContableReadRepository readRepository)
    : IRequestHandler<UpdateSetupInventarioCommand, Result<SetupInventarioResponse>>
{
    public async Task<Result<SetupInventarioResponse>> Handle(UpdateSetupInventarioCommand request, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<SetupInventario>();
        var entity = await repository.FirstOrDefaultAsync(x => x.Id == request.Id, asTracking: true, cancellationToken: cancellationToken);
        if (entity is null)
        {
            return Result<SetupInventarioResponse>.Fallo(SetupContableReglas.NoEncontrado(SetupInventarioReglas.Nombre));
        }

        var aplicado = await SetupInventarioReglas.ValidarYAplicarAsync(
            unitOfWork, entity, alta: false, request.AlmacenId, request.GrupoInventarioId, request.CuentaInventarioId,
            request.CuentaAjusteInventarioId, request.CuentaVariacionCostoId, cancellationToken,
            antesDeAplicar: () => repository.EstablecerVersionOriginal(entity, request.Xmin));
        if (aplicado.EsFallo)
        {
            return Result<SetupInventarioResponse>.Fallo(aplicado);
        }

        repository.Update(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<SetupInventarioResponse>.Exito((await readRepository.GetInventarioAsync(entity.Id, cancellationToken))!);
    }
}

public sealed class GetSetupInventarioByIdQueryHandler(ISetupContableReadRepository readRepository)
    : IRequestHandler<GetSetupInventarioByIdQuery, Result<SetupInventarioResponse>>
{
    public async Task<Result<SetupInventarioResponse>> Handle(GetSetupInventarioByIdQuery request, CancellationToken cancellationToken) =>
        await readRepository.GetInventarioAsync(request.Id, cancellationToken) is { } item
            ? Result<SetupInventarioResponse>.Exito(item)
            : Result<SetupInventarioResponse>.Fallo(SetupContableReglas.NoEncontrado(SetupInventarioReglas.Nombre));
}

public sealed class ListSetupsInventarioQueryHandler(ISetupContableReadRepository readRepository)
    : IRequestHandler<ListSetupsInventarioQuery, Result<PagedResult<SetupInventarioResponse>>>
{
    public Task<Result<PagedResult<SetupInventarioResponse>>> Handle(ListSetupsInventarioQuery request, CancellationToken cancellationToken) =>
        readRepository.ListInventarioAsync(request.Search, request.Paginacion, cancellationToken);
}

internal static class SetupInventarioReglas
{
    public const string Nombre = "de inventario";

    public static async Task<Result> ValidarYAplicarAsync(
        IUnitOfWork unitOfWork, SetupInventario entity, bool alta, Guid? almacenId, Guid grupoInventarioId,
        Guid cuentaInventarioId, Guid cuentaAjusteInventarioId, Guid cuentaVariacionCostoId,
        CancellationToken cancellationToken, Action? antesDeAplicar = null)
    {
        var almacen = SetupContableReglas.Comodin(almacenId);
        var codigoAlmacen = await SetupContableReglas.ValidarEjeAsync<Almacen>(
            unitOfWork, almacen, alta ? null : entity.AlmacenId, principal: false, "AlmacenId", "almacén", a => a.Codigo, cancellationToken);
        if (!codigoAlmacen.TryObtenerValor(out var almacenCodigo))
        {
            return codigoAlmacen;
        }

        var codigoGrupo = await SetupContableReglas.ValidarEjeAsync<GrupoInventario>(
            unitOfWork, grupoInventarioId, alta ? null : entity.GrupoInventarioId, principal: true, "GrupoInventarioId", "grupo de inventario", g => g.Codigo, cancellationToken);
        if (!codigoGrupo.TryObtenerValor(out var grupoCodigo))
        {
            return codigoGrupo;
        }

        var cuentas = await SetupContableReglas.ValidarCuentasAsync(unitOfWork,
        [
            (cuentaInventarioId, alta ? null : entity.CuentaInventarioId, "CuentaInventarioId", "de inventario"),
            (cuentaAjusteInventarioId, alta ? null : entity.CuentaAjusteInventarioId, "CuentaAjusteInventarioId", "de ajuste de inventario"),
            (cuentaVariacionCostoId, alta ? null : entity.CuentaVariacionCostoId, "CuentaVariacionCostoId", "de variación de costo"),
        ], cancellationToken);
        if (cuentas.EsFallo)
        {
            return cuentas;
        }

        var id = entity.Id;
        if (await SetupContableReglas.DuplicadoAsync<SetupInventario>(
                unitOfWork, x => x.Id != id && x.AlmacenId == almacen && x.GrupoInventarioId == grupoInventarioId,
                $"{Nombre} para Almacen={almacenCodigo} × GrupoInventario={grupoCodigo}", cancellationToken) is { } duplicado)
        {
            return Result.Fallo(duplicado);
        }

        antesDeAplicar?.Invoke();
        entity.AlmacenId = almacen;
        entity.GrupoInventarioId = grupoInventarioId;
        entity.CuentaInventarioId = cuentaInventarioId;
        entity.CuentaAjusteInventarioId = cuentaAjusteInventarioId;
        entity.CuentaVariacionCostoId = cuentaVariacionCostoId;
        return Result.Exito();
    }
}
