using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenSource1.Api.Infrastructure;
using OpenSource1.Application.Features.SetupsContables;
using OpenSource1.Application.Features.SetupsContables.Commands;
using OpenSource1.Application.Features.SetupsContables.Dtos;
using OpenSource1.Application.Features.SetupsContables.Queries;
using OpenSource1.Application.Security;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Api.Controllers;

/// <summary>
/// Mantenimiento de los tres setups contables (Task 5.4): <c>api/setups-contables/general</c> (grupo de negocio × grupo de
/// producto), <c>/iva</c> (grupo de IVA de negocio × de producto) e <c>/inventario</c> (almacén × grupo de inventario). Un tipo
/// desconocido no casa ninguna ruta → 404. En cada uno el eje secundario ausente/<c>null</c> es el COMODÍN y los dos ejes son de
/// reemplazo completo en el PUT; <c>xmin</c> obligatorio. Combinación ya existente → 409 <c>setup_contable.conflicto</c>.
/// </summary>
[ApiController]
[Route("api/setups-contables")]
public sealed class SetupsContablesController(ISender sender) : ControllerBase
{
    // ── General ──

    [HttpGet("general")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<PagedResult<SetupGeneralResponse>>(StatusCodes.Status200OK)]
    public Task<IActionResult> ListGeneral(
        [FromQuery] Guid? grupoNegocioId, [FromQuery] Guid? grupoProductoId,
        [FromQuery] int pagina = 1, [FromQuery] int tamanoPagina = PageRequest.TamanoPorDefecto, CancellationToken cancellationToken = default) =>
        ListarAsync(new ListSetupsGeneralQuery(new SetupContableSearchCriteria(grupoNegocioId, grupoProductoId), Pagina(pagina, tamanoPagina)), cancellationToken);

    [HttpGet("general/{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<SetupGeneralResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetGeneral(Guid id, CancellationToken cancellationToken) =>
        LeerAsync(new GetSetupGeneralByIdQuery(id), cancellationToken);

    [HttpPost("general")]
    [Authorize(Policy = ApplicationPolicies.CanAdd)]
    [ProducesResponseType<SetupGeneralResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateGeneral(CreateSetupGeneralRequest r, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateSetupGeneralCommand(
            r.GrupoNegocioId, r.GrupoProductoId, r.CuentaVentasId, r.CuentaCostoVentasId, r.CuentaDescuentoVentasId, r.CuentaAjusteInventarioId),
            cancellationToken);
        return result.EsFallo ? result.ToActionResult() : CreatedAtAction(nameof(GetGeneral), new { id = result.Valor.Id }, result.Valor);
    }

    [HttpPut("general/{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanModify)]
    [ProducesResponseType<SetupGeneralResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> UpdateGeneral(Guid id, UpdateSetupGeneralRequest r, CancellationToken cancellationToken) =>
        LeerAsync(new UpdateSetupGeneralCommand(
            id, r.GrupoNegocioId, r.GrupoProductoId, r.CuentaVentasId, r.CuentaCostoVentasId, r.CuentaDescuentoVentasId, r.CuentaAjusteInventarioId, r.Xmin),
            cancellationToken);

    [HttpDelete("general/{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> DeleteGeneral(Guid id, CancellationToken cancellationToken) =>
        BorrarAsync(TipoSetupContable.General, id, cancellationToken);

    // ── IVA ──

    [HttpGet("iva")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<PagedResult<SetupIvaResponse>>(StatusCodes.Status200OK)]
    public Task<IActionResult> ListIva(
        [FromQuery] Guid? grupoIvaNegocioId, [FromQuery] Guid? grupoIvaProductoId,
        [FromQuery] int pagina = 1, [FromQuery] int tamanoPagina = PageRequest.TamanoPorDefecto, CancellationToken cancellationToken = default) =>
        ListarAsync(new ListSetupsIvaQuery(new SetupContableSearchCriteria(grupoIvaNegocioId, grupoIvaProductoId), Pagina(pagina, tamanoPagina)), cancellationToken);

    [HttpGet("iva/{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<SetupIvaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetIva(Guid id, CancellationToken cancellationToken) =>
        LeerAsync(new GetSetupIvaByIdQuery(id), cancellationToken);

    [HttpPost("iva")]
    [Authorize(Policy = ApplicationPolicies.CanAdd)]
    [ProducesResponseType<SetupIvaResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateIva(CreateSetupIvaRequest r, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateSetupIvaCommand(
            r.GrupoIvaNegocioId, r.GrupoIvaProductoId, r.PorcentajeIva, r.CuentaIvaVentasId, r.CuentaIvaComprasId, r.IdentificadorIva, r.TipoCalculoIva),
            cancellationToken);
        return result.EsFallo ? result.ToActionResult() : CreatedAtAction(nameof(GetIva), new { id = result.Valor.Id }, result.Valor);
    }

    /// <summary><c>cuentaIvaComprasId</c>: ausente/<c>null</c> = conservar, Guid vacío = quitar. El resto es de reemplazo completo.</summary>
    [HttpPut("iva/{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanModify)]
    [ProducesResponseType<SetupIvaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> UpdateIva(Guid id, UpdateSetupIvaRequest r, CancellationToken cancellationToken) =>
        LeerAsync(new UpdateSetupIvaCommand(
            id, r.GrupoIvaNegocioId, r.GrupoIvaProductoId, r.PorcentajeIva, r.CuentaIvaVentasId, r.CuentaIvaComprasId, r.IdentificadorIva, r.TipoCalculoIva, r.Xmin),
            cancellationToken);

    [HttpDelete("iva/{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> DeleteIva(Guid id, CancellationToken cancellationToken) =>
        BorrarAsync(TipoSetupContable.Iva, id, cancellationToken);

    // ── Inventario ──

    [HttpGet("inventario")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<PagedResult<SetupInventarioResponse>>(StatusCodes.Status200OK)]
    public Task<IActionResult> ListInventario(
        [FromQuery] Guid? almacenId, [FromQuery] Guid? grupoInventarioId,
        [FromQuery] int pagina = 1, [FromQuery] int tamanoPagina = PageRequest.TamanoPorDefecto, CancellationToken cancellationToken = default) =>
        ListarAsync(new ListSetupsInventarioQuery(new SetupContableSearchCriteria(almacenId, grupoInventarioId), Pagina(pagina, tamanoPagina)), cancellationToken);

    [HttpGet("inventario/{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<SetupInventarioResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetInventario(Guid id, CancellationToken cancellationToken) =>
        LeerAsync(new GetSetupInventarioByIdQuery(id), cancellationToken);

    [HttpPost("inventario")]
    [Authorize(Policy = ApplicationPolicies.CanAdd)]
    [ProducesResponseType<SetupInventarioResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateInventario(CreateSetupInventarioRequest r, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateSetupInventarioCommand(
            r.AlmacenId, r.GrupoInventarioId, r.CuentaInventarioId, r.CuentaAjusteInventarioId, r.CuentaVariacionCostoId),
            cancellationToken);
        return result.EsFallo ? result.ToActionResult() : CreatedAtAction(nameof(GetInventario), new { id = result.Valor.Id }, result.Valor);
    }

    [HttpPut("inventario/{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanModify)]
    [ProducesResponseType<SetupInventarioResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> UpdateInventario(Guid id, UpdateSetupInventarioRequest r, CancellationToken cancellationToken) =>
        LeerAsync(new UpdateSetupInventarioCommand(
            id, r.AlmacenId, r.GrupoInventarioId, r.CuentaInventarioId, r.CuentaAjusteInventarioId, r.CuentaVariacionCostoId, r.Xmin),
            cancellationToken);

    [HttpDelete("inventario/{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> DeleteInventario(Guid id, CancellationToken cancellationToken) =>
        BorrarAsync(TipoSetupContable.Inventario, id, cancellationToken);

    // ── Comunes ──

    // Orden fijo en el repositorio (eje principal, luego secundario con el comodín primero): no se expone ordenarPor.
    private static PageRequest Pagina(int pagina, int tamanoPagina) => new(pagina, tamanoPagina, null, Descendente: false);

    private async Task<IActionResult> ListarAsync<T>(IRequest<Result<PagedResult<T>>> query, CancellationToken cancellationToken)
    {
        var result = await sender.Send(query, cancellationToken);
        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    private async Task<IActionResult> LeerAsync<T>(IRequest<Result<T>> request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    private async Task<IActionResult> BorrarAsync(TipoSetupContable tipo, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteSetupContableCommand(tipo, id), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : NoContent();
    }
}

public sealed record CreateSetupGeneralRequest(
    Guid? GrupoNegocioId, Guid GrupoProductoId, Guid CuentaVentasId, Guid CuentaCostoVentasId, Guid CuentaDescuentoVentasId, Guid CuentaAjusteInventarioId);

public sealed record UpdateSetupGeneralRequest(
    Guid? GrupoNegocioId, Guid GrupoProductoId, Guid CuentaVentasId, Guid CuentaCostoVentasId, Guid CuentaDescuentoVentasId, Guid CuentaAjusteInventarioId, long Xmin);

public sealed record CreateSetupIvaRequest(
    Guid? GrupoIvaNegocioId, Guid GrupoIvaProductoId, decimal PorcentajeIva, Guid CuentaIvaVentasId, Guid? CuentaIvaComprasId, string IdentificadorIva,
    TipoCalculoIva TipoCalculoIva = TipoCalculoIva.Normal);

public sealed record UpdateSetupIvaRequest(
    Guid? GrupoIvaNegocioId, Guid GrupoIvaProductoId, decimal PorcentajeIva, Guid CuentaIvaVentasId, Guid? CuentaIvaComprasId, string IdentificadorIva,
    TipoCalculoIva TipoCalculoIva, long Xmin);

public sealed record CreateSetupInventarioRequest(
    Guid? AlmacenId, Guid GrupoInventarioId, Guid CuentaInventarioId, Guid CuentaAjusteInventarioId, Guid CuentaVariacionCostoId);

public sealed record UpdateSetupInventarioRequest(
    Guid? AlmacenId, Guid GrupoInventarioId, Guid CuentaInventarioId, Guid CuentaAjusteInventarioId, Guid CuentaVariacionCostoId, long Xmin);
