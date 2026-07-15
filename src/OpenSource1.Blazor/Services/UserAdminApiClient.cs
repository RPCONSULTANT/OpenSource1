using System.Net;
using System.Net.Http.Json;
using OpenSource1.Application.Features.Users.Dtos;
using OpenSource1.Application.Services.Auth.Dtos;

namespace OpenSource1.Blazor.Services;

public sealed class UserAdminApiClient(HttpClient httpClient, ILogger<UserAdminApiClient> logger) : IUserAdminApiClient
{
    public async Task<(IReadOnlyList<UserSummaryResponse>? Users, string? Error)> ListUsersAsync(
        string? search = null, string? role = null, bool? isActive = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new List<string>();
            if (!string.IsNullOrWhiteSpace(search)) parameters.Add($"search={Uri.EscapeDataString(search.Trim())}");
            if (!string.IsNullOrWhiteSpace(role)) parameters.Add($"role={Uri.EscapeDataString(role.Trim())}");
            if (isActive.HasValue) parameters.Add($"isActive={isActive.Value}");
            var url = parameters.Count == 0 ? "api/users" : $"api/users?{string.Join("&", parameters)}";

            using var response = await httpClient.GetAsync(url, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var users = await response.Content.ReadFromJsonAsync<IReadOnlyList<UserSummaryResponse>>(cancellationToken);
                return (users, null);
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
                return (null, "No tiene permisos para ver la lista de usuarios.");

            return (null, "No se pudo obtener la lista de usuarios.");
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            logger.LogWarning(ex, "Error calling users API.");
            return (null, "El servicio no está disponible en este momento.");
        }
    }

    public async Task<(UserSummaryResponse? User, string? Error)> GetByIdAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.GetAsync($"api/users/{userId}", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var user = await response.Content.ReadFromJsonAsync<UserSummaryResponse>(cancellationToken);
                return (user, null);
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return (null, "Usuario no encontrado.");

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                return (null, "No tiene permisos para ver este usuario.");

            return (null, "No se pudo obtener el usuario.");
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            logger.LogWarning(ex, "Error calling get user by id API.");
            return (null, "El servicio no está disponible en este momento.");
        }
    }

    public async Task<(bool Success, string? Error)> CreateAsync(
        string email, string fullName, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync("api/users", new CreateUserRequest(email, fullName, password), cancellationToken);
            if (response.IsSuccessStatusCode) return (true, null);
            var error = await TryReadError(response, cancellationToken);
            return (false, error);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            logger.LogWarning(ex, "Error creating user.");
            return (false, "El servicio no está disponible en este momento.");
        }
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(
        string userId, string fullName, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.PutAsJsonAsync($"api/users/{userId}", new UpdateUserRequest(fullName), cancellationToken);
            if (response.IsSuccessStatusCode) return (true, null);
            var error = await TryReadError(response, cancellationToken);
            return (false, error);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            logger.LogWarning(ex, "Error updating user.");
            return (false, "El servicio no está disponible en este momento.");
        }
    }

    public async Task<(bool Success, string? Error)> ResetPasswordAsync(
        string userId, string newPassword, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync($"api/users/{userId}/reset-password", new AdminResetPasswordRequest(newPassword), cancellationToken);
            if (response.IsSuccessStatusCode) return (true, null);
            var error = await TryReadError(response, cancellationToken);
            return (false, error);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            logger.LogWarning(ex, "Error resetting user password.");
            return (false, "El servicio no está disponible en este momento.");
        }
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.DeleteAsync($"api/users/{userId}", cancellationToken);
            if (response.IsSuccessStatusCode) return (true, null);
            var error = await TryReadError(response, cancellationToken);
            return (false, error);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            logger.LogWarning(ex, "Error deleting user.");
            return (false, "El servicio no está disponible en este momento.");
        }
    }

    public async Task<(bool Success, string? Error)> AssignRoleAsync(
        string userId, string role, CancellationToken cancellationToken = default)
        => await PostRoleActionAsync("api/users/assign-role", userId, role, cancellationToken);

    public async Task<(bool Success, string? Error)> RemoveRoleAsync(
        string userId, string role, CancellationToken cancellationToken = default)
        => await PostRoleActionAsync("api/users/remove-role", userId, role, cancellationToken);

    public async Task<(bool Success, string? Error)> ToggleActiveAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.PostAsync($"api/users/{userId}/toggle-active", null, cancellationToken);
            if (response.IsSuccessStatusCode) return (true, null);
            var error = await TryReadError(response, cancellationToken);
            return (false, error);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            logger.LogWarning(ex, "Error toggling user active state.");
            return (false, "El servicio no está disponible en este momento.");
        }
    }

    private async Task<(bool Success, string? Error)> PostRoleActionAsync(
        string endpoint, string userId, string role, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync(endpoint, new AssignRoleRequest(userId, role), cancellationToken);
            if (response.IsSuccessStatusCode) return (true, null);
            var error = await TryReadError(response, cancellationToken);
            return (false, error);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            logger.LogWarning(ex, "Error calling {Endpoint}.", endpoint);
            return (false, "El servicio no está disponible en este momento.");
        }
    }

    private static async Task<string> TryReadError(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var err = await response.Content.ReadFromJsonAsync<AuthErrorResponse>(ct);
            return err?.Errors.FirstOrDefault() ?? "Ocurrió un error inesperado.";
        }
        catch
        {
            return "Ocurrió un error inesperado.";
        }
    }
}
