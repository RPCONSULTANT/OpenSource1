using OpenSource1.Application.Features.Users.Dtos;

namespace OpenSource1.Application.Features.Users;

public interface IUserAdminService
{
    Task<IReadOnlyList<UserSummaryResponse>> ListUsersAsync(CancellationToken cancellationToken = default);
    Task<UserSummaryResponse?> GetByIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<(bool Success, IReadOnlyList<string> Errors)> AssignRoleAsync(AssignRoleRequest request, CancellationToken cancellationToken = default);
    Task<(bool Success, IReadOnlyList<string> Errors)> RemoveRoleAsync(AssignRoleRequest request, CancellationToken cancellationToken = default);
    Task<(bool Success, IReadOnlyList<string> Errors)> ToggleActiveAsync(string userId, CancellationToken cancellationToken = default);
}
