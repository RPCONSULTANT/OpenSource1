namespace OpenSource1.Application.Services.Auth.Dtos;

/// <summary>Represents an authentication error response.</summary>
public sealed record AuthErrorResponse(string Message, IReadOnlyList<string> Errors);
