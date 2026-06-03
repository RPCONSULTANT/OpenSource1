namespace OpenSource1.Application.Features.Users.Dtos;

public sealed record UserSummaryResponse(
    string Id,
    string FullName,
    string Email,
    bool IsActive,
    IReadOnlyList<string> Roles);
