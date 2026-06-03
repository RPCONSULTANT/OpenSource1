using System.ComponentModel.DataAnnotations;

namespace OpenSource1.Application.Services.Auth.Dtos;

/// <summary>Payload used to authenticate a local user account.</summary>
public sealed record LoginRequest
{
    [Required, MaxLength(256)]
    public required string UserNameOrEmail { get; init; }

    [Required, DataType(DataType.Password), MaxLength(100)]
    public required string Password { get; init; }
}
