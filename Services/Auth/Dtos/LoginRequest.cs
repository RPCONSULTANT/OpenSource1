using System.ComponentModel.DataAnnotations;

namespace test.Services.Auth.Dtos;

/// <summary>Payload used to authenticate a local user account.</summary>
public sealed record LoginRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public required string Email { get; init; }

    [Required, MaxLength(100)]
    public required string Password { get; init; }
}
