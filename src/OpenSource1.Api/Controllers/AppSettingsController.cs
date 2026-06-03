using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenSource1.Application.Features.AppSettings.Commands;
using OpenSource1.Application.Features.AppSettings.Dtos;
using OpenSource1.Application.Features.AppSettings.Queries;

namespace OpenSource1.Api.Controllers;

[ApiController]
[Route("api/app-settings")]
[Authorize]
public sealed class AppSettingsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AppSettingResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AppSettingResponse>>> List(CancellationToken cancellationToken)
    {
        var response = await sender.Send(new ListAppSettingsQuery(), cancellationToken);
        return Ok(response);
    }

    [HttpGet("{key}")]
    [ProducesResponseType<AppSettingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppSettingResponse>> GetByKey(string key, CancellationToken cancellationToken)
    {
        var response = await sender.Send(new GetAppSettingByKeyQuery(key), cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPut("{key}")]
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
}

/// <summary>Payload used to create or update an application setting.</summary>
public sealed record SetAppSettingRequest(string Value, string? Description);
