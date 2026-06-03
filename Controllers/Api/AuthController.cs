using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using test.Services.Auth;
using test.Services.Auth.Dtos;

namespace test.Controllers.Api;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
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
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<AuthErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var response = await authService.LoginAsync(request, cancellationToken);

        return response is null
            ? Unauthorized(new AuthErrorResponse("Invalid credentials.", []))
            : Ok(response);
    }
}
