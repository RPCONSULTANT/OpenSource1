namespace OpenSource1.Application.Services.Auth.Dtos;

/// <summary>Respuesta de acciones relacionadas con contraseñas.</summary>
public sealed record PasswordActionResponse(string Message, string? ResetToken = null);
