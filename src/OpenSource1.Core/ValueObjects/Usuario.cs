using OpenSource1.Core.Abstractions;
using OpenSource1.Core.Common;

namespace OpenSource1.Core.ValueObjects;

public sealed class Usuario : ValueObject
{
    public Usuario(string userName, string email, string fullName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            throw new ErroresDeDominioException(new Error(
                "usuario.username_requerido", "El nombre de usuario es obligatorio.", nameof(userName)));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ErroresDeDominioException(new Error(
                "usuario.email_requerido", "El correo electrónico es obligatorio.", nameof(email)));
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ErroresDeDominioException(new Error(
                "usuario.nombre_completo_requerido", "El nombre completo es obligatorio.", nameof(fullName)));
        }

        UserName = userName.Trim();
        Email = email.Trim().ToLowerInvariant();
        FullName = fullName.Trim();
    }

    public string UserName { get; }
    public string Email { get; }
    public string FullName { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return UserName.ToUpperInvariant();
        yield return Email;
        yield return FullName;
    }
}
