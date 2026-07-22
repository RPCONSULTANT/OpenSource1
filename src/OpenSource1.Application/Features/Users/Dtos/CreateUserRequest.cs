namespace OpenSource1.Application.Features.Users.Dtos;

public sealed record CreateUserRequest(string Email, string FullName, string Password);
