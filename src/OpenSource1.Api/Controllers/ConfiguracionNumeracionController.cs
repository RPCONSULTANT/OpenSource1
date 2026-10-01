using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenSource1.Api.Infrastructure;
using OpenSource1.Application.Features.ConfiguracionNumeracion;
using OpenSource1.Application.Features.ConfiguracionNumeracion.Dtos;
using OpenSource1.Application.Security;
using OpenSource1.Core.Enums;

namespace OpenSource1.Api.Controllers;

/// <summary>
/// Serie predeterminada por tipo de documento (spec no-series). Consultar = CanConsult; cambiar =
/// <see cref="ApplicationPolicies.CanAdministrar"/>. <c>tipo</c> viaja como entero (1–8).
/// </summary>
[ApiController]
[Route("api/configuracion/numeracion")]
public sealed class ConfiguracionNumeracionController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<IReadOnlyList<ConfiguracionNumeracionResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListConfiguracionNumeracionQuery(), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpPut("{tipo:int}")]
    [Authorize(Policy = ApplicationPolicies.CanAdministrar)]
    [ProducesResponseType<ConfiguracionNumeracionResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int tipo, UpdateConfiguracionNumeracionRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateConfiguracionNumeracionCommand((TipoDocumentoSerie)tipo, request.SerieId, request.Xmin), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }
}

public sealed record UpdateConfiguracionNumeracionRequest(Guid SerieId, long Xmin);
