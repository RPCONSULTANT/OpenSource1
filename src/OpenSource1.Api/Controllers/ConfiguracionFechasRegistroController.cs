using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenSource1.Api.Infrastructure;
using OpenSource1.Application.Features.FechasRegistro.Commands;
using OpenSource1.Application.Features.FechasRegistro.Dtos;
using OpenSource1.Application.Features.FechasRegistro.Queries;
using OpenSource1.Application.Security;

namespace OpenSource1.Api.Controllers;

/// <summary>
/// Fechas de registro permitidas (Task 8.5): rango general (fila única) y excepciones por usuario. Solo el Administrador
/// (<see cref="ApplicationPolicies.CanAdministrar"/>) consulta y configura; los posteos las aplican a todos los usuarios.
/// </summary>
[ApiController]
[Route("api/configuracion/fechas-registro")]
[Authorize(Policy = ApplicationPolicies.CanAdministrar)]
public sealed class ConfiguracionFechasRegistroController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<FechasRegistroGeneralResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGeneral(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetFechasRegistroGeneralQuery(), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpPut]
    [ProducesResponseType<FechasRegistroGeneralResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateGeneral(FechasRegistroRangoRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateFechasRegistroGeneralCommand(request.PermitirRegistroDesde, request.PermitirRegistroHasta), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpGet("usuarios")]
    [ProducesResponseType<IReadOnlyList<FechasRegistroUsuarioResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListUsuarios(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListFechasRegistroUsuariosQuery(), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpGet("usuarios/{id:guid}")]
    [ProducesResponseType<FechasRegistroUsuarioResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUsuario(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetFechasRegistroUsuarioByIdQuery(id), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpPost("usuarios")]
    [ProducesResponseType<FechasRegistroUsuarioResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateUsuario(CreateFechasRegistroUsuarioRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateFechasRegistroUsuarioCommand(request.UsuarioId, request.PermitirRegistroDesde, request.PermitirRegistroHasta),
            cancellationToken);
        return result.EsFallo
            ? result.ToActionResult()
            : CreatedAtAction(nameof(GetUsuario), new { id = result.Valor.Id }, result.Valor);
    }

    [HttpPut("usuarios/{id:guid}")]
    [ProducesResponseType<FechasRegistroUsuarioResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUsuario(Guid id, FechasRegistroRangoRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateFechasRegistroUsuarioCommand(id, request.PermitirRegistroDesde, request.PermitirRegistroHasta), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpDelete("usuarios/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUsuario(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteFechasRegistroUsuarioCommand(id), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : NoContent();
    }
}

public sealed record FechasRegistroRangoRequest(DateOnly? PermitirRegistroDesde, DateOnly? PermitirRegistroHasta);

public sealed record CreateFechasRegistroUsuarioRequest(Guid UsuarioId, DateOnly? PermitirRegistroDesde, DateOnly? PermitirRegistroHasta);
