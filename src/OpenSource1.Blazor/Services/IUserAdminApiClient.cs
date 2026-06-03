using OpenSource1.Application.Features.Users.Dtos;

namespace OpenSource1.Blazor.Services;

public interface IUserAdminApiClient
{
    Task<(IReadOnlyList<UserSummaryResponse>? Users, string? Error)> ListUsersAsync(CancellationToken cancellationToken = default);
    Task<(UserSummaryResponse? User, string? Error)> GetByIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> AssignRoleAsync(string userId, string role, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> RemoveRoleAsync(string userId, string role, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> ToggleActiveAsync(string userId, CancellationToken cancellationToken = default);
}
