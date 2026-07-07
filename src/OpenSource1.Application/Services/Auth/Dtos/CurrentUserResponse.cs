namespace OpenSource1.Application.Services.Auth.Dtos;

/// <summary>Represents the current authenticated user and allowed operations.</summary>
public sealed record CurrentUserResponse(
    string UserId,
    string UserName,
    string Email,
    string? ProfileImagePath,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
