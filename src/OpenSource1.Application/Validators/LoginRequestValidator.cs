using FluentValidation;
using OpenSource1.Application.Services.Auth.Dtos;

namespace OpenSource1.Application.Validators;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.UserNameOrEmail)
            .NotEmpty().WithMessage("Debe ingresar su usuario o correo electrónico.")
            .MaximumLength(256).WithMessage("El usuario no puede superar 256 caracteres.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Debe ingresar su contraseña.")
            .MaximumLength(100).WithMessage("La contraseña no puede superar 100 caracteres.");
    }
}
