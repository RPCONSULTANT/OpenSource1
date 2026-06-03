using System.ComponentModel.DataAnnotations;

namespace OpenSource1.Application.Services.Auth.Dtos;

/// <summary>Payload used to register a local user account.</summary>
public sealed record RegisterRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public required string Email { get; init; }

    [Required, DataType(DataType.Password), MinLength(8), MaxLength(100)]
    public required string Password { get; init; }

    [Required, MaxLength(200)]
    public required string FullName { get; init; }
}
