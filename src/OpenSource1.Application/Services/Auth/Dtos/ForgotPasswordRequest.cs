using System.ComponentModel.DataAnnotations;

namespace OpenSource1.Application.Services.Auth.Dtos;

/// <summary>Solicita un token de restablecimiento de contraseña.</summary>
public sealed record ForgotPasswordRequest
{
    [Required, MaxLength(256)]
    public required string UserNameOrEmail { get; init; }
}
