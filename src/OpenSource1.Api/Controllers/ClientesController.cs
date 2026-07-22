using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenSource1.Application.Features.Clientes;
using OpenSource1.Application.Features.Clientes.Commands;
using OpenSource1.Application.Features.Clientes.Dtos;
using OpenSource1.Application.Features.Clientes.Queries;
using OpenSource1.Application.Security;
using OpenSource1.Core.ValueObjects;

namespace OpenSource1.Api.Controllers;

[ApiController]
[Route("api/clientes")]
public sealed class ClientesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<IReadOnlyList<ClienteResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClienteResponse>>> List(
        [FromQuery] string? nombre,
        [FromQuery] string? apellido,
        [FromQuery] string? email,
        [FromQuery] string? telefono,
        [FromQuery] string? direccion,
        [FromQuery] string? sector,
        [FromQuery] string? pais,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ListClientesQuery(new ClienteSearchCriteria(nombre, apellido, email, telefono, direccion, sector, pais)),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<ClienteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClienteResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await sender.Send(new GetClienteByIdQuery(id), cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = ApplicationPolicies.CanAdd)]
    [ProducesResponseType<ClienteResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClienteResponse>> Create(CreateClienteRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre) ||
            string.IsNullOrWhiteSpace(request.Apellido) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            !EsDireccionValida(request.DireccionLinea1, request.DireccionLinea2) ||
            !EsPaisValido(request.PaisCodigo))
        {
            return BadRequest();
        }

        var result = await sender.Send(
            new CreateClienteCommand(request.Nombre, request.Apellido, request.Email, request.Telefono, request.DireccionLinea1, request.DireccionLinea2, request.Sector, request.PaisCodigo, request.ImagePath),
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanModify)]
    [ProducesResponseType<ClienteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClienteResponse>> Update(Guid id, UpdateClienteRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre) ||
            string.IsNullOrWhiteSpace(request.Apellido) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            !EsDireccionValida(request.DireccionLinea1, request.DireccionLinea2) ||
            !EsPaisValido(request.PaisCodigo))
        {
            return BadRequest();
        }

        var result = await sender.Send(
            new UpdateClienteCommand(id, request.Nombre, request.Apellido, request.Email, request.Telefono, request.DireccionLinea1, request.DireccionLinea2, request.Sector, request.PaisCodigo, request.ImagePath),
            cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await sender.Send(new DeleteClienteCommand(id), cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    private static bool EsDireccionValida(string? linea1, string? linea2) =>
        !string.IsNullOrWhiteSpace(linea1) || string.IsNullOrWhiteSpace(linea2);

    private static bool EsPaisValido(string? codigo) =>
        string.IsNullOrWhiteSpace(codigo) || Pais.EsCodigoValido(codigo);
}

public sealed record CreateClienteRequest(string Nombre, string Apellido, string Email, string? Telefono, string? DireccionLinea1, string? DireccionLinea2, string? Sector, string? PaisCodigo, string? ImagePath = null);
public sealed record UpdateClienteRequest(string Nombre, string Apellido, string Email, string? Telefono, string? DireccionLinea1, string? DireccionLinea2, string? Sector, string? PaisCodigo, string? ImagePath = null);
