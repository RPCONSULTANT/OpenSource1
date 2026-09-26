using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.SetupsContables.Commands;
using OpenSource1.Application.Features.SetupsContables.Dtos;
using OpenSource1.Application.Features.SetupsContables.Queries;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Contabilidad;

namespace OpenSource1.Application.Features.SetupsContables.Handlers;

/// <summary>Alta de una fila de <c>SetupsContableGeneral</c> (grupo de negocio × grupo de producto).</summary>
public sealed class CreateSetupGeneralCommandHandler(IUnitOfWork unitOfWork, ISetupContableReadRepository readRepository)
    : IRequestHandler<CreateSetupGeneralCommand, Result<SetupGeneralResponse>>
{
    public async Task<Result<SetupGeneralResponse>> Handle(CreateSetupGeneralCommand request, CancellationToken cancellationToken)
    {
        var entity = new SetupContableGeneral();
        var aplicado = await SetupGeneralReglas.ValidarYAplicarAsync(
            unitOfWork, entity, alta: true, request.GrupoNegocioId, request.GrupoProductoId, request.CuentaVentasId,
            request.CuentaCostoVentasId, request.CuentaDescuentoVentasId, request.CuentaAjusteInventarioId, cancellationToken);
        if (aplicado.EsFallo)
        {
            return Result<SetupGeneralResponse>.Fallo(aplicado);
        }

        await unitOfWork.Repository<SetupContableGeneral>().AddAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<SetupGeneralResponse>.Exito((await readRepository.GetGeneralAsync(entity.Id, cancellationToken))!);
    }
}

/// <summary>Modificación (reemplazo completo, ver el convenio de los comandos) con concurrencia optimista vía <c>Xmin</c>.</summary>
public sealed class UpdateSetupGeneralCommandHandler(IUnitOfWork unitOfWork, ISetupContableReadRepository readRepository)
    : IRequestHandler<UpdateSetupGeneralCommand, Result<SetupGeneralResponse>>
{
    public async Task<Result<SetupGeneralResponse>> Handle(UpdateSetupGeneralCommand request, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<SetupContableGeneral>();
        var entity = await repository.FirstOrDefaultAsync(x => x.Id == request.Id, asTracking: true, cancellationToken: cancellationToken);
        if (entity is null)
        {
            return Result<SetupGeneralResponse>.Fallo(SetupContableReglas.NoEncontrado(SetupGeneralReglas.Nombre));
        }

        var aplicado = await SetupGeneralReglas.ValidarYAplicarAsync(
            unitOfWork, entity, alta: false, request.GrupoNegocioId, request.GrupoProductoId, request.CuentaVentasId,
            request.CuentaCostoVentasId, request.CuentaDescuentoVentasId, request.CuentaAjusteInventarioId, cancellationToken,
            antesDeAplicar: () => repository.EstablecerVersionOriginal(entity, request.Xmin));
        if (aplicado.EsFallo)
        {
            return Result<SetupGeneralResponse>.Fallo(aplicado);
        }

        repository.Update(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<SetupGeneralResponse>.Exito((await readRepository.GetGeneralAsync(entity.Id, cancellationToken))!);
    }
}

public sealed class GetSetupGeneralByIdQueryHandler(ISetupContableReadRepository readRepository)
    : IRequestHandler<GetSetupGeneralByIdQuery, Result<SetupGeneralResponse>>
{
    public async Task<Result<SetupGeneralResponse>> Handle(GetSetupGeneralByIdQuery request, CancellationToken cancellationToken) =>
        await readRepository.GetGeneralAsync(request.Id, cancellationToken) is { } item
            ? Result<SetupGeneralResponse>.Exito(item)
            : Result<SetupGeneralResponse>.Fallo(SetupContableReglas.NoEncontrado(SetupGeneralReglas.Nombre));
}

public sealed class ListSetupsGeneralQueryHandler(ISetupContableReadRepository readRepository)
    : IRequestHandler<ListSetupsGeneralQuery, Result<PagedResult<SetupGeneralResponse>>>
{
    public Task<Result<PagedResult<SetupGeneralResponse>>> Handle(ListSetupsGeneralQuery request, CancellationToken cancellationToken) =>
        readRepository.ListGeneralAsync(request.Search, request.Paginacion, cancellationToken);
}

internal static class SetupGeneralReglas
{
    public const string Nombre = "general";

    public static async Task<Result> ValidarYAplicarAsync(
        IUnitOfWork unitOfWork, SetupContableGeneral entity, bool alta, Guid? grupoNegocioId, Guid grupoProductoId,
        Guid cuentaVentasId, Guid cuentaCostoVentasId, Guid cuentaDescuentoVentasId, Guid cuentaAjusteInventarioId,
        CancellationToken cancellationToken, Action? antesDeAplicar = null)
    {
        var negocio = SetupContableReglas.Comodin(grupoNegocioId);
        var codigoNegocio = await SetupContableReglas.ValidarEjeAsync<GrupoNegocio>(
            unitOfWork, negocio, alta ? null : entity.GrupoNegocioId, principal: false, "GrupoNegocioId", "grupo de negocio", g => g.Codigo, cancellationToken);
        if (!codigoNegocio.TryObtenerValor(out var negocioCodigo))
        {
            return codigoNegocio;
        }

        var codigoProducto = await SetupContableReglas.ValidarEjeAsync<GrupoProducto>(
            unitOfWork, grupoProductoId, alta ? null : entity.GrupoProductoId, principal: true, "GrupoProductoId", "grupo de producto", g => g.Codigo, cancellationToken);
        if (!codigoProducto.TryObtenerValor(out var productoCodigo))
        {
            return codigoProducto;
        }

        var cuentas = await SetupContableReglas.ValidarCuentasAsync(unitOfWork,
        [
            (cuentaVentasId, alta ? null : entity.CuentaVentasId, "CuentaVentasId", "de ventas"),
            (cuentaCostoVentasId, alta ? null : entity.CuentaCostoVentasId, "CuentaCostoVentasId", "de costo de ventas"),
            (cuentaDescuentoVentasId, alta ? null : entity.CuentaDescuentoVentasId, "CuentaDescuentoVentasId", "de descuentos sobre ventas"),
            (cuentaAjusteInventarioId, alta ? null : entity.CuentaAjusteInventarioId, "CuentaAjusteInventarioId", "de ajuste de inventario"),
        ], cancellationToken);
        if (cuentas.EsFallo)
        {
            return cuentas;
        }

        var id = entity.Id;
        if (await SetupContableReglas.DuplicadoAsync<SetupContableGeneral>(
                unitOfWork, x => x.Id != id && x.GrupoNegocioId == negocio && x.GrupoProductoId == grupoProductoId,
                $"{Nombre} para GrupoNegocio={negocioCodigo} × GrupoProducto={productoCodigo}", cancellationToken) is { } duplicado)
        {
            return Result.Fallo(duplicado);
        }

        antesDeAplicar?.Invoke();
        entity.GrupoNegocioId = negocio;
        entity.GrupoProductoId = grupoProductoId;
        entity.CuentaVentasId = cuentaVentasId;
        entity.CuentaCostoVentasId = cuentaCostoVentasId;
        entity.CuentaDescuentoVentasId = cuentaDescuentoVentasId;
        entity.CuentaAjusteInventarioId = cuentaAjusteInventarioId;
        return Result.Exito();
    }
}
