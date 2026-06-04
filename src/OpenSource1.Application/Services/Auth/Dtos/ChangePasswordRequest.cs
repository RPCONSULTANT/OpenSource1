using System.ComponentModel.DataAnnotations;

namespace OpenSource1.Application.Services.Auth.Dtos;

/// <summary>Cambia la contraseña del usuario autenticado.</summary>
public sealed record ChangePasswordRequest
{
    [Required, DataType(DataType.Password), MaxLength(100)]
    public required string CurrentPassword { get; init; }

    [Required, DataType(DataType.Password), MaxLength(100)]
    public required string NewPassword { get; init; }
}
