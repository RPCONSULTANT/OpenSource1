namespace OpenSource1.Application.Services.Auth.Dtos;

/// <summary>JWT authentication response returned after successful login or registration.</summary>
public sealed record AuthResponse(
    string UserId,
    string Email,
    string FullName,
    string AccessToken,
    DateTimeOffset ExpiresAtUtc);
