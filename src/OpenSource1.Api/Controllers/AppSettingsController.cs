using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenSource1.Application.Security;
using OpenSource1.Application.Features.AppSettings.Commands;
using OpenSource1.Application.Features.AppSettings.Dtos;
using OpenSource1.Application.Features.AppSettings.Queries;
using System;

namespace OpenSource1.Api.Controllers;

[ApiController]
[Route("api/app-settings")]
[Obsolete("Modulo de prueba obsoleto. No usar AppSettings para nuevos desarrollos.")]
public sealed class AppSettingsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<IReadOnlyList<AppSettingResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AppSettingResponse>>> List(CancellationToken cancellationToken)
    {
        var response = await sender.Send(new ListAppSettingsQuery(), cancellationToken);
        return Ok(response);
    }

    [HttpGet("{key}")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<AppSettingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppSettingResponse>> GetByKey(string key, CancellationToken cancellationToken)
    {
        var response = await sender.Send(new GetAppSettingByKeyQuery(key), cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPut("{key}")]
    [Authorize(Policy = ApplicationPolicies.CanModify)]
    [ProducesResponseType<AppSettingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AppSettingResponse>> Set(
        string key,
        SetAppSettingRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(request.Value))
        {
            return BadRequest();
        }

        var response = await sender.Send(new SetAppSettingCommand(key, request.Value, request.Description), cancellationToken);
        return Ok(response);
    }

    [HttpPost]
    [Authorize(Policy = ApplicationPolicies.CanAdd)]
    [ProducesResponseType<AppSettingResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AppSettingResponse>> Create(
        SetAppSettingRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.Value))
        {
            return BadRequest();
        }

        var existing = await sender.Send(new GetAppSettingByKeyQuery(request.Key), cancellationToken);
        if (existing is not null)
        {
            return Conflict("The application setting already exists.");
        }

        var response = await sender.Send(new SetAppSettingCommand(request.Key, request.Value, request.Description), cancellationToken);
        return CreatedAtAction(nameof(GetByKey), new { key = response.Key }, response);
    }

    [HttpDelete("{key}")]
    [Authorize(Policy = ApplicationPolicies.CanDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string key, CancellationToken cancellationToken)
    {
        var deleted = await sender.Send(new DeleteAppSettingCommand(key), cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}

/// <summary>Payload used to create or update an application setting.</summary>
public sealed record SetAppSettingRequest(string? Key, string Value, string? Description);
