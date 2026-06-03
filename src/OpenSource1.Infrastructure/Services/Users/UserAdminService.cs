using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenSource1.Application.Features.Users;
using OpenSource1.Application.Features.Users.Dtos;
using OpenSource1.Application.Security;
using OpenSource1.Infrastructure.Identity;

namespace OpenSource1.Infrastructure.Services.Users;

public sealed class UserAdminService(
    UserManager<Usuario> userManager) : IUserAdminService
{
    public async Task<IReadOnlyList<UserSummaryResponse>> ListUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await userManager.Users
            .OrderBy(u => u.FullName)
            .ToListAsync(cancellationToken);

        var result = new List<UserSummaryResponse>(users.Count);
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            result.Add(new UserSummaryResponse(
                user.Id,
                user.FullName ?? user.UserName ?? string.Empty,
                user.Email ?? string.Empty,
                user.IsActive,
                roles.ToArray()));
        }

        return result;
    }

    public async Task<(bool Success, IReadOnlyList<string> Errors)> AssignRoleAsync(
        AssignRoleRequest request, CancellationToken cancellationToken = default)
    {
        if (!ApplicationRoles.All.Contains(request.Role, StringComparer.OrdinalIgnoreCase))
            return (false, [$"El rol '{request.Role}' no es válido."]);

        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null)
            return (false, ["Usuario no encontrado."]);

        if (await userManager.IsInRoleAsync(user, request.Role))
            return (false, [$"El usuario ya tiene el rol '{request.Role}'."]);

        var result = await userManager.AddToRoleAsync(user, request.Role);
        return result.Succeeded
            ? (true, Array.Empty<string>())
            : (false, result.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<(bool Success, IReadOnlyList<string> Errors)> RemoveRoleAsync(
        AssignRoleRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null)
            return (false, ["Usuario no encontrado."]);

        if (!await userManager.IsInRoleAsync(user, request.Role))
            return (false, [$"El usuario no tiene el rol '{request.Role}'."]);

        var result = await userManager.RemoveFromRoleAsync(user, request.Role);
        return result.Succeeded
            ? (true, Array.Empty<string>())
            : (false, result.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<(bool Success, IReadOnlyList<string> Errors)> ToggleActiveAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return (false, ["Usuario no encontrado."]);

        user.IsActive = !user.IsActive;
        var result = await userManager.UpdateAsync(user);
        return result.Succeeded
            ? (true, Array.Empty<string>())
            : (false, result.Errors.Select(e => e.Description).ToArray());
    }
}
