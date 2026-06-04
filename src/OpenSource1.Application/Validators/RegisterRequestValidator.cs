using FluentValidation;
using OpenSource1.Application.Services.Auth.Dtos;

namespace OpenSource1.Application.Validators;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("El nombre completo es obligatorio.")
            .MinimumLength(2).WithMessage("El nombre debe tener al menos 2 caracteres.")
            .MaximumLength(200).WithMessage("El nombre no puede superar 200 caracteres.")
            .Matches(@"^[\p{L}\s'-]+$").WithMessage("El nombre solo puede contener letras, espacios, guiones y apóstrofos.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio.")
            .EmailAddress().WithMessage("Debe ingresar un correo electrónico válido (ej. usuario@dominio.com).")
            .MaximumLength(256).WithMessage("El correo no puede superar 256 caracteres.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.")
            .MaximumLength(100).WithMessage("La contraseña no puede superar 100 caracteres.")
            .Matches("[A-Z]").WithMessage("La contraseña debe contener al menos una letra mayúscula.")
            .Matches("[a-z]").WithMessage("La contraseña debe contener al menos una letra minúscula.")
            .Matches("[0-9]").WithMessage("La contraseña debe contener al menos un dígito numérico.");
    }
}
