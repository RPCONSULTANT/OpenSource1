using OpenSource1.Core.Abstractions;
using OpenSource1.Core.Common;

namespace OpenSource1.Core.ValueObjects;

public sealed class Usuario : ValueObject
{
    private Usuario(string userName, string email, string fullName)
    {
        UserName = userName;
        Email = email;
        FullName = fullName;
    }

    public string UserName { get; }
    public string Email { get; }
    public string FullName { get; }

    /// <param name="userName">Nombre de usuario.</param>
    /// <param name="email">Correo electrónico.</param>
    /// <param name="fullName">Nombre completo.</param>
    /// <param name="nombreCampoUserName">
    /// Nombre del campo de <paramref name="userName"/> tal como lo conoce el llamante. Si no
    /// se indica se usa <c>nameof(userName)</c>.
    /// </param>
    /// <param name="nombreCampoEmail">Igual que <paramref name="nombreCampoUserName"/> pero para <paramref name="email"/>.</param>
    /// <param name="nombreCampoFullName">Igual que <paramref name="nombreCampoUserName"/> pero para <paramref name="fullName"/>.</param>
    public static Usuario Of(
        string userName,
        string email,
        string fullName,
        string? nombreCampoUserName = null,
        string? nombreCampoEmail = null,
        string? nombreCampoFullName = null)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            throw new ErroresDeDominioException(new Error(
                "usuario.username_requerido", "El nombre de usuario es obligatorio.", nombreCampoUserName ?? nameof(userName)));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ErroresDeDominioException(new Error(
                "usuario.email_requerido", "El correo electrónico es obligatorio.", nombreCampoEmail ?? nameof(email)));
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ErroresDeDominioException(new Error(
                "usuario.nombre_completo_requerido", "El nombre completo es obligatorio.", nombreCampoFullName ?? nameof(fullName)));
        }

        return new Usuario(userName.Trim(), email.Trim().ToLowerInvariant(), fullName.Trim());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return UserName.ToUpperInvariant();
        yield return Email;
        yield return FullName;
    }
}
