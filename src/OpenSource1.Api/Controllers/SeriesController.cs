using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenSource1.Api.Infrastructure;
using OpenSource1.Application.Features.Series;
using OpenSource1.Application.Features.Series.Commands;
using OpenSource1.Application.Features.Series.Dtos;
using OpenSource1.Application.Features.Series.Queries;
using OpenSource1.Application.Security;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Api.Controllers;

/// <summary>
/// Series de numeración y sus líneas (spec no-series, Parte 3). Consultar = CanConsult; crear, modificar y eliminar series y líneas =
/// <see cref="ApplicationPolicies.CanAdministrar"/>. <c>tipo</c> viaja como entero (1–8).
/// </summary>
[ApiController]
[Route("api/series")]
public sealed class SeriesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<PagedResult<SerieResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? codigo, [FromQuery] int? tipo, [FromQuery] bool? activa,
        [FromQuery] int pagina = 1, [FromQuery] int tamanoPagina = PageRequest.TamanoPorDefecto,
        [FromQuery] string? ordenarPor = null, [FromQuery] bool descendente = false, CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new ListSeriesQuery(
            new SerieSearchCriteria(codigo, (TipoDocumentoSerie?)tipo, activa),
            new PageRequest(pagina, tamanoPagina, ordenarPor ?? "Codigo", descendente)), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetSerieByIdQuery(id), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpGet("{id:guid}/lineas")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    public async Task<IActionResult> ListLineas(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetSerieByIdQuery(id), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor.Lineas);
    }

    [HttpGet("{id:guid}/proximo")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    public async Task<IActionResult> Proximo(Guid id, [FromQuery] DateOnly? fecha, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetProximoNumeroQuery(id, fecha), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpPost]
    [Authorize(Policy = ApplicationPolicies.CanAdministrar)]
    public async Task<IActionResult> Create(CreateSerieRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateSerieCommand(
            request.Codigo, request.Descripcion, request.TipoDocumento, request.PermiteHuecos, request.Activa), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : CreatedAtAction(nameof(GetById), new { id = result.Valor.Id }, result.Valor);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanAdministrar)]
    public async Task<IActionResult> Update(Guid id, UpdateSerieRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateSerieCommand(
            id, request.Codigo, request.Descripcion, request.TipoDocumento, request.PermiteHuecos, request.Activa, request.Xmin), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanAdministrar)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteSerieCommand(id), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : NoContent();
    }

    [HttpPost("{id:guid}/lineas")]
    [Authorize(Policy = ApplicationPolicies.CanAdministrar)]
    public async Task<IActionResult> CreateLinea(Guid id, CreateLineaSerieRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateLineaSerieCommand(
            id, request.NumeroInicial, request.NumeroFinal, request.NumeroAviso, request.UltimoNumeroUsado, request.FechaInicial,
            request.Incremento, request.Bloqueada), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : CreatedAtAction(nameof(GetById), new { id }, result.Valor);
    }

    [HttpPut("{id:guid}/lineas/{lineaId:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanAdministrar)]
    public async Task<IActionResult> UpdateLinea(Guid id, Guid lineaId, UpdateLineaSerieRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateLineaSerieCommand(
            id, lineaId, request.NumeroInicial, request.NumeroFinal, request.NumeroAviso, request.FechaInicial, request.Incremento,
            request.Bloqueada, request.Xmin), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpDelete("{id:guid}/lineas/{lineaId:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanAdministrar)]
    public async Task<IActionResult> DeleteLinea(Guid id, Guid lineaId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteLineaSerieCommand(id, lineaId), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : NoContent();
    }
}

public sealed record CreateSerieRequest(string Codigo, string Descripcion, TipoDocumentoSerie TipoDocumento, bool PermiteHuecos = false, bool Activa = true);

public sealed record UpdateSerieRequest(string Codigo, string Descripcion, TipoDocumentoSerie TipoDocumento, bool PermiteHuecos, bool Activa, long Xmin);

public sealed record CreateLineaSerieRequest(
    string NumeroInicial, string NumeroFinal, DateOnly FechaInicial, string? NumeroAviso = null, string? UltimoNumeroUsado = null,
    int Incremento = 1, bool Bloqueada = false);

public sealed record UpdateLineaSerieRequest(
    string NumeroInicial, string NumeroFinal, DateOnly FechaInicial, long Xmin, string? NumeroAviso = null, int Incremento = 1,
    bool Bloqueada = false);
