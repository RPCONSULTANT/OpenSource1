using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenSource1.Application.Features.Users;
using OpenSource1.Application.Features.Users.Dtos;
using OpenSource1.Application.Security;
using OpenSource1.Application.Services.Auth.Dtos;

namespace OpenSource1.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = ApplicationRoles.Administrator)]
public sealed class UsersController(IUserAdminService userAdminService) : ControllerBase
{
    /// <summary>Lista todos los usuarios del sistema con sus roles.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<UserSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserSummaryResponse>>> List(CancellationToken cancellationToken)
    {
        var users = await userAdminService.ListUsersAsync(cancellationToken);
        return Ok(users);
    }

    /// <summary>Asigna un rol a un usuario.</summary>
    [HttpPost("assign-role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AuthErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AssignRole(AssignRoleRequest request, CancellationToken cancellationToken)
    {
        var (success, errors) = await userAdminService.AssignRoleAsync(request, cancellationToken);
        if (!success)
            return BadRequest(new AuthErrorResponse("No se pudo asignar el rol.", errors));

        return NoContent();
    }

    /// <summary>Quita un rol de un usuario.</summary>
    [HttpPost("remove-role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AuthErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RemoveRole(AssignRoleRequest request, CancellationToken cancellationToken)
    {
        var (success, errors) = await userAdminService.RemoveRoleAsync(request, cancellationToken);
        if (!success)
            return BadRequest(new AuthErrorResponse("No se pudo quitar el rol.", errors));

        return NoContent();
    }

    /// <summary>Activa o desactiva una cuenta de usuario.</summary>
    [HttpPost("{userId}/toggle-active")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AuthErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ToggleActive(string userId, CancellationToken cancellationToken)
    {
        var (success, errors) = await userAdminService.ToggleActiveAsync(userId, cancellationToken);
        if (!success)
            return BadRequest(new AuthErrorResponse("No se pudo cambiar el estado del usuario.", errors));

        return NoContent();
    }
}
