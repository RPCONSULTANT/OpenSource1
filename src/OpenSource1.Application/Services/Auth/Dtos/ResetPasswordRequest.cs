using System.ComponentModel.DataAnnotations;

namespace OpenSource1.Application.Services.Auth.Dtos;

/// <summary>Restablece una contraseña usando un token emitido por Identity.</summary>
public sealed record ResetPasswordRequest
{
    [Required, MaxLength(256)]
    public required string UserNameOrEmail { get; init; }

    [Required]
    public required string Token { get; init; }

    [Required, DataType(DataType.Password), MaxLength(100)]
    public required string NewPassword { get; init; }
}
