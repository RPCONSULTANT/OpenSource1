using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenSource1.Application.Security;
using OpenSource1.Application.Services.Auth;
using OpenSource1.Application.Services.Auth.Dtos;
using System.Security.Claims;

namespace OpenSource1.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<AuthErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var (response, errors) = await authService.RegisterAsync(request, cancellationToken);

        if (response is null)
        {
            return BadRequest(new AuthErrorResponse("User registration failed.", errors));
        }

        return CreatedAtAction(nameof(Register), new { userId = response.UserId }, response);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<AuthErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var (response, errors) = await authService.LoginAsync(request, cancellationToken);

        return response is null
            ? Unauthorized(new AuthErrorResponse("Login failed.", errors))
            : Ok(response);
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType<PasswordActionResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PasswordActionResponse>> ForgotPassword(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var response = await authService.ForgotPasswordAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType<PasswordActionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<AuthErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PasswordActionResponse>> ResetPassword(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var (response, errors) = await authService.ResetPasswordAsync(request, cancellationToken);

        return response is null
            ? BadRequest(new AuthErrorResponse("Password reset failed.", errors))
            : Ok(response);
    }

    [Authorize]
    [HttpPost("change-password")]
    [ProducesResponseType<PasswordActionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<AuthErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PasswordActionResponse>> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var (response, errors) = await authService.ChangePasswordAsync(userId, request, cancellationToken);

        return response is null
            ? BadRequest(new AuthErrorResponse("Password change failed.", errors))
            : Ok(response);
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    public ActionResult<CurrentUserResponse> Me()
    {
        var roles = User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();
        var permissions = User.FindAll("permission").Select(claim => claim.Value).ToArray();

        return Ok(new CurrentUserResponse(
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            User.Identity?.Name ?? string.Empty,
            User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email") ?? string.Empty,
            roles,
            permissions));
    }
}
