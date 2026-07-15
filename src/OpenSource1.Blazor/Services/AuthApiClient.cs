using System.Net;
using System.Net.Http.Json;
using OpenSource1.Application.Services.Auth.Dtos;

namespace OpenSource1.Blazor.Services;

public sealed class AuthApiClient(HttpClient httpClient, ILogger<AuthApiClient> logger) : IAuthApiClient
{
    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync("api/auth/login", request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken);
                return new AuthResult(authResponse, []);
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
            {
                var error = await response.Content.ReadFromJsonAsync<AuthErrorResponse>(cancellationToken);
                return new AuthResult(null, error?.Errors.Count > 0 ? error.Errors : ["Usuario o contraseña incorrectos."]);
            }

            logger.LogWarning("Authentication API returned unexpected status {StatusCode}.", response.StatusCode);
            return new AuthResult(null, ["No fue posible iniciar sesión. Intente nuevamente."]);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new AuthResult(null, ["El servicio tardó demasiado en responder. Intente nuevamente."]);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Authentication API is not available.");
            return new AuthResult(null, ["El servicio de autenticación no está disponible en este momento."]);
        }
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync("api/auth/register", request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken);
                return new AuthResult(authResponse, []);
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var error = await response.Content.ReadFromJsonAsync<AuthErrorResponse>(cancellationToken);
                return new AuthResult(null, error?.Errors.Count > 0 ? error.Errors : ["No fue posible completar el registro."]);
            }

            logger.LogWarning("Register API returned unexpected status {StatusCode}.", response.StatusCode);
            return new AuthResult(null, ["No fue posible completar el registro. Intente nuevamente."]);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new AuthResult(null, ["El servicio tardó demasiado en responder. Intente nuevamente."]);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Register API is not available.");
            return new AuthResult(null, ["El servicio de registro no está disponible en este momento."]);
        }
    }

    public async Task<PasswordActionResult> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default) =>
        await PostPasswordActionAsync("api/auth/forgot-password", request, "No fue posible generar la solicitud de recuperación.", cancellationToken);

    public async Task<PasswordActionResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default) =>
        await PostPasswordActionAsync("api/auth/reset-password", request, "No fue posible restablecer la contraseña.", cancellationToken);

    public async Task<PasswordActionResult> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default) =>
        await PostPasswordActionAsync("api/auth/change-password", request, "No fue posible cambiar la contraseña.", cancellationToken);

    public async Task<CurrentUserResponse?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("api/auth/me", cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CurrentUserResponse>(cancellationToken);
    }

    public async Task<(bool Success, IReadOnlyList<string> Errors)> UpdateProfileImageAsync(UpdateProfileImageRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("api/auth/profile-image", request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return (true, Array.Empty<string>());
        }

        var error = await response.Content.ReadFromJsonAsync<AuthErrorResponse>(cancellationToken);
        return (false, error?.Errors ?? ["No fue posible actualizar la imagen de perfil."]);
    }

    private async Task<PasswordActionResult> PostPasswordActionAsync<TRequest>(
        string url,
        TRequest request,
        string fallbackMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync(url, request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var actionResponse = await response.Content.ReadFromJsonAsync<PasswordActionResponse>(cancellationToken);
                return new PasswordActionResult(actionResponse, []);
            }

            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            {
                var error = await response.Content.ReadFromJsonAsync<AuthErrorResponse>(cancellationToken);
                return new PasswordActionResult(null, error?.Errors.Count > 0 ? error.Errors : [fallbackMessage]);
            }

            logger.LogWarning("Password action API {Url} returned unexpected status {StatusCode}.", url, response.StatusCode);
            return new PasswordActionResult(null, [fallbackMessage]);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new PasswordActionResult(null, ["El servicio tardó demasiado en responder. Intente nuevamente."]);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Password action API {Url} is not available.", url);
            return new PasswordActionResult(null, ["El servicio de autenticación no está disponible en este momento."]);
        }
    }
}
