using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenSource1.Api.Infrastructure;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Commands;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Dtos;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Queries;
using OpenSource1.Application.Features.DiariosInventario.Lotes;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Commands;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Dtos;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Queries;
using OpenSource1.Application.Features.DiariosInventario.Plantillas.Dtos;
using OpenSource1.Application.Features.DiariosInventario.Plantillas.Queries;
using OpenSource1.Application.Security;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Api.Controllers;

[ApiController]
[Route("api/diarios-inventario")]
public sealed class DiariosInventarioController(ISender sender) : ControllerBase
{
    // ----- Plantillas (sembradas, de solo lectura: sin CRUD) -----

    [HttpGet("plantillas")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<IReadOnlyList<PlantillaDiarioResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPlantillas(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListPlantillasDiarioQuery(), cancellationToken);
        return Ok(result);
    }

    // ----- Lotes -----

    [HttpGet("lotes")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<PagedResult<LoteDiarioResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListLotes(
        [FromQuery] Guid? plantillaId,
        [FromQuery] string? codigo,
        [FromQuery] string? nombre,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = PageRequest.TamanoPorDefecto,
        [FromQuery] string? ordenarPor = null,
        [FromQuery] bool descendente = true,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new ListLotesDiarioQuery(
                new LoteDiarioSearchCriteria(plantillaId, codigo, nombre),
                new PageRequest(pagina, tamanoPagina, ordenarPor, descendente)),
            cancellationToken);

        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpGet("lotes/{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<LoteDiarioResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLoteById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetLoteDiarioByIdQuery(id), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpPost("lotes")]
    [Authorize(Policy = ApplicationPolicies.CanAdd)]
    [ProducesResponseType<LoteDiarioResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateLote(CreateLoteDiarioRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateLoteDiarioCommand(request.PlantillaDiarioId, request.Codigo, request.Nombre, request.SerieId, request.Bloqueado),
            cancellationToken);

        return result.EsFallo
            ? result.ToActionResult()
            : CreatedAtAction(nameof(GetLoteById), new { id = result.Valor.Id }, result.Valor);
    }

    [HttpPut("lotes/{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanModify)]
    [ProducesResponseType<LoteDiarioResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateLote(Guid id, UpdateLoteDiarioRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateLoteDiarioCommand(id, request.Codigo, request.Nombre, request.SerieId, request.Bloqueado, request.Xmin),
            cancellationToken);

        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpDelete("lotes/{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteLote(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteLoteDiarioCommand(id), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : NoContent();
    }

    // ----- Líneas -----

    [HttpGet("lotes/{loteId:guid}/lineas")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<IReadOnlyList<LineaDiarioResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListLineas(Guid loteId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListLineasDiarioQuery(loteId), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpGet("lineas/{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<LineaDiarioResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLineaById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetLineaDiarioByIdQuery(id), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpPost("lotes/{loteId:guid}/lineas")]
    [Authorize(Policy = ApplicationPolicies.CanAdd)]
    [ProducesResponseType<LineaDiarioResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateLinea(Guid loteId, CreateLineaDiarioRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateLineaDiarioCommand(
                loteId, request.FechaRegistro, request.FechaDocumento, request.NumeroDocumento, request.TipoMovimiento,
                request.ProductoId, request.AlmacenId, request.AlmacenDestinoId, request.UnidadMedidaId, request.Cantidad,
                request.CostoUnitario, request.Descripcion),
            cancellationToken);

        return result.EsFallo
            ? result.ToActionResult()
            : CreatedAtAction(nameof(GetLineaById), new { id = result.Valor.Id }, result.Valor);
    }

    [HttpPut("lineas/{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanModify)]
    [ProducesResponseType<LineaDiarioResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateLinea(Guid id, UpdateLineaDiarioRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateLineaDiarioCommand(
                id, request.FechaRegistro, request.FechaDocumento, request.NumeroDocumento, request.TipoMovimiento,
                request.ProductoId, request.AlmacenId, request.AlmacenDestinoId, request.UnidadMedidaId, request.Cantidad,
                request.CostoUnitario, request.Descripcion, request.Xmin),
            cancellationToken);

        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpDelete("lineas/{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteLinea(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteLineaDiarioCommand(id), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : NoContent();
    }
}

public sealed record CreateLoteDiarioRequest(Guid PlantillaDiarioId, string Codigo, string Nombre, Guid? SerieId, bool Bloqueado);

public sealed record UpdateLoteDiarioRequest(string Codigo, string Nombre, Guid? SerieId, bool? Bloqueado, long Xmin);

public sealed record CreateLineaDiarioRequest(
    DateOnly FechaRegistro,
    DateOnly FechaDocumento,
    string? NumeroDocumento,
    TipoMovimientoInventario TipoMovimiento,
    Guid ProductoId,
    Guid AlmacenId,
    Guid? AlmacenDestinoId,
    Guid UnidadMedidaId,
    decimal Cantidad,
    decimal? CostoUnitario,
    string? Descripcion);

public sealed record UpdateLineaDiarioRequest(
    DateOnly FechaRegistro,
    DateOnly FechaDocumento,
    string? NumeroDocumento,
    TipoMovimientoInventario TipoMovimiento,
    Guid ProductoId,
    Guid AlmacenId,
    Guid? AlmacenDestinoId,
    Guid UnidadMedidaId,
    decimal Cantidad,
    decimal? CostoUnitario,
    string? Descripcion,
    long Xmin);
