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
    public async Task<UserSummaryResponse?> GetByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return null;

        var roles = await userManager.GetRolesAsync(user);
        return new UserSummaryResponse(
            user.Id,
            user.FullName ?? user.UserName ?? string.Empty,
            user.Email ?? string.Empty,
            user.IsActive,
            roles.ToArray());
    }

    public async Task<(bool Success, IReadOnlyList<string> Errors)> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = new Usuario { UserName = request.Email.Trim(), Email = request.Email.Trim(), FullName = request.FullName.Trim(), IsActive = true };
        var result = await userManager.CreateAsync(user, request.Password);
        return result.Succeeded ? (true, Array.Empty<string>()) : (false, result.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<(bool Success, IReadOnlyList<string> Errors)> DeleteUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return (false, ["Usuario no encontrado."]);
        if (user.UserName == "admin") return (false, ["No se puede eliminar el usuario administrador principal."]);
        var result = await userManager.DeleteAsync(user);
        return result.Succeeded ? (true, Array.Empty<string>()) : (false, result.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<IReadOnlyList<UserSummaryResponse>> ListUsersAsync(
        string? search = null, string? role = null, bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var query = userManager.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u =>
                (u.FullName != null && EF.Functions.ILike(u.FullName, $"%{term}%")) ||
                (u.Email != null && EF.Functions.ILike(u.Email, $"%{term}%")));
        }

        if (isActive.HasValue)
        {
            query = query.Where(u => u.IsActive == isActive.Value);
        }

        var users = await query.OrderBy(u => u.FullName).ToListAsync(cancellationToken);

        var result = new List<UserSummaryResponse>(users.Count);
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            if (!string.IsNullOrWhiteSpace(role) && !roles.Contains(role, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(new UserSummaryResponse(
                user.Id,
                user.FullName ?? user.UserName ?? string.Empty,
                user.Email ?? string.Empty,
                user.IsActive,
                roles.ToArray()));
        }

        return result;
    }

    public async Task<(bool Success, IReadOnlyList<string> Errors)> UpdateUserAsync(
        string userId, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return (false, ["Usuario no encontrado."]);

        if (string.IsNullOrWhiteSpace(request.FullName))
            return (false, ["El nombre completo es requerido."]);

        user.FullName = request.FullName.Trim();
        var result = await userManager.UpdateAsync(user);
        return result.Succeeded
            ? (true, Array.Empty<string>())
            : (false, result.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<(bool Success, IReadOnlyList<string> Errors)> ResetPasswordAsync(
        string userId, AdminResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return (false, ["Usuario no encontrado."]);

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, request.NewPassword);
        return result.Succeeded
            ? (true, Array.Empty<string>())
            : (false, result.Errors.Select(e => e.Description).ToArray());
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
        if (user.UserName == "admin" && user.IsActive)
            return (false, ["No se puede desactivar el administrador principal."]);

        user.IsActive = !user.IsActive;
        var result = await userManager.UpdateAsync(user);
        return result.Succeeded
            ? (true, Array.Empty<string>())
            : (false, result.Errors.Select(e => e.Description).ToArray());
    }
}
