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
}
