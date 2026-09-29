using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.SetupsContables.Commands;
using OpenSource1.Application.Features.SetupsContables.Dtos;
using OpenSource1.Application.Features.SetupsContables.Queries;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.SetupsContables.Handlers;

/// <summary>Alta de una fila de <c>SetupsIva</c> (grupo de IVA de negocio × grupo de IVA de producto).</summary>
public sealed class CreateSetupIvaCommandHandler(IUnitOfWork unitOfWork, ISetupContableReadRepository readRepository)
    : IRequestHandler<CreateSetupIvaCommand, Result<SetupIvaResponse>>
{
    public async Task<Result<SetupIvaResponse>> Handle(CreateSetupIvaCommand request, CancellationToken cancellationToken)
    {
        var entity = new SetupIva();
        var aplicado = await SetupIvaReglas.ValidarYAplicarAsync(
            unitOfWork, entity, alta: true, request.GrupoIvaNegocioId, request.GrupoIvaProductoId, request.PorcentajeIva,
            request.CuentaIvaVentasId, SetupContableReglas.Comodin(request.CuentaIvaComprasId), request.IdentificadorIva,
            request.TipoCalculoIva, cancellationToken);
        if (aplicado.EsFallo)
        {
            return Result<SetupIvaResponse>.Fallo(aplicado);
        }

        await unitOfWork.Repository<SetupIva>().AddAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<SetupIvaResponse>.Exito((await readRepository.GetIvaAsync(entity.Id, cancellationToken))!);
    }
}

public sealed class UpdateSetupIvaCommandHandler(IUnitOfWork unitOfWork, ISetupContableReadRepository readRepository)
    : IRequestHandler<UpdateSetupIvaCommand, Result<SetupIvaResponse>>
{
    public async Task<Result<SetupIvaResponse>> Handle(UpdateSetupIvaCommand request, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<SetupIva>();
        var entity = await repository.FirstOrDefaultAsync(x => x.Id == request.Id, asTracking: true, cancellationToken: cancellationToken);
        if (entity is null)
        {
            return Result<SetupIvaResponse>.Fallo(SetupContableReglas.NoEncontrado(SetupIvaReglas.Nombre));
        }

        // null = conservar; Guid.Empty = quitar la cuenta de IVA en compras.
        var compras = request.CuentaIvaComprasId is null ? entity.CuentaIvaComprasId
            : request.CuentaIvaComprasId == Guid.Empty ? null : request.CuentaIvaComprasId;

        var aplicado = await SetupIvaReglas.ValidarYAplicarAsync(
            unitOfWork, entity, alta: false, request.GrupoIvaNegocioId, request.GrupoIvaProductoId, request.PorcentajeIva,
            request.CuentaIvaVentasId, compras, request.IdentificadorIva, request.TipoCalculoIva, cancellationToken,
            antesDeAplicar: () => repository.EstablecerVersionOriginal(entity, request.Xmin));
        if (aplicado.EsFallo)
        {
            return Result<SetupIvaResponse>.Fallo(aplicado);
        }

        repository.Update(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<SetupIvaResponse>.Exito((await readRepository.GetIvaAsync(entity.Id, cancellationToken))!);
    }
}

public sealed class GetSetupIvaByIdQueryHandler(ISetupContableReadRepository readRepository)
    : IRequestHandler<GetSetupIvaByIdQuery, Result<SetupIvaResponse>>
{
    public async Task<Result<SetupIvaResponse>> Handle(GetSetupIvaByIdQuery request, CancellationToken cancellationToken) =>
        await readRepository.GetIvaAsync(request.Id, cancellationToken) is { } item
            ? Result<SetupIvaResponse>.Exito(item)
            : Result<SetupIvaResponse>.Fallo(SetupContableReglas.NoEncontrado(SetupIvaReglas.Nombre));
}

public sealed class ListSetupsIvaQueryHandler(ISetupContableReadRepository readRepository)
    : IRequestHandler<ListSetupsIvaQuery, Result<PagedResult<SetupIvaResponse>>>
{
    public Task<Result<PagedResult<SetupIvaResponse>>> Handle(ListSetupsIvaQuery request, CancellationToken cancellationToken) =>
        readRepository.ListIvaAsync(request.Search, request.Paginacion, cancellationToken);
}

internal static class SetupIvaReglas
{
    public const string Nombre = "de IVA";

    /// <param name="cuentaIvaComprasId">Valor FINAL de la cuenta opcional (ya resuelto el convenio null/Guid.Empty del PUT).</param>
    public static async Task<Result> ValidarYAplicarAsync(
        IUnitOfWork unitOfWork, SetupIva entity, bool alta, Guid? grupoIvaNegocioId, Guid grupoIvaProductoId, decimal porcentajeIva,
        Guid cuentaIvaVentasId, Guid? cuentaIvaComprasId, string? identificadorIva, TipoCalculoIva tipoCalculoIva,
        CancellationToken cancellationToken, Action? antesDeAplicar = null)
    {
        var errores = SetupContableReglas.ValidarIva(porcentajeIva, tipoCalculoIva, identificadorIva);
        if (errores.Count > 0)
        {
            return Result.Fallo([.. errores]);
        }

        var negocio = SetupContableReglas.Comodin(grupoIvaNegocioId);
        var codigoNegocio = await SetupContableReglas.ValidarEjeAsync<GrupoIvaNegocio>(
            unitOfWork, negocio, alta ? null : entity.GrupoIvaNegocioId, principal: false, "GrupoIvaNegocioId", "grupo de IVA de negocio", g => g.Codigo, cancellationToken);
        if (!codigoNegocio.TryObtenerValor(out var negocioCodigo))
        {
            return codigoNegocio;
        }

        var codigoProducto = await SetupContableReglas.ValidarEjeAsync<GrupoIvaProducto>(
            unitOfWork, grupoIvaProductoId, alta ? null : entity.GrupoIvaProductoId, principal: true, "GrupoIvaProductoId", "grupo de IVA de producto", g => g.Codigo, cancellationToken);
        if (!codigoProducto.TryObtenerValor(out var productoCodigo))
        {
            return codigoProducto;
        }

        var cuentas = await SetupContableReglas.ValidarCuentasAsync(unitOfWork,
        [
            (cuentaIvaVentasId, alta ? null : entity.CuentaIvaVentasId, "CuentaIvaVentasId", "de IVA en ventas"),
            (cuentaIvaComprasId, alta ? null : entity.CuentaIvaComprasId, "CuentaIvaComprasId", "de IVA en compras"),
        ], cancellationToken);
        if (cuentas.EsFallo)
        {
            return cuentas;
        }

        var id = entity.Id;
        if (await SetupContableReglas.DuplicadoAsync<SetupIva>(
                unitOfWork, x => x.Id != id && x.GrupoIvaNegocioId == negocio && x.GrupoIvaProductoId == grupoIvaProductoId,
                $"{Nombre} para GrupoIvaNegocio={negocioCodigo} × GrupoIvaProducto={productoCodigo}", cancellationToken) is { } duplicado)
        {
            return Result.Fallo(duplicado);
        }

        antesDeAplicar?.Invoke();
        entity.GrupoIvaNegocioId = negocio;
        entity.GrupoIvaProductoId = grupoIvaProductoId;
        entity.PorcentajeIva = porcentajeIva;
        entity.CuentaIvaVentasId = cuentaIvaVentasId;
        entity.CuentaIvaComprasId = cuentaIvaComprasId;
        entity.IdentificadorIva = SetupContableReglas.NormalizarIdentificador(identificadorIva!);
        entity.TipoCalculoIva = tipoCalculoIva;
        return Result.Exito();
    }
}
