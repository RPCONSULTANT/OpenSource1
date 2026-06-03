using OpenSource1.Core.Abstractions;

namespace OpenSource1.Core.ValueObjects;

public sealed class Usuario : ValueObject
{
    public Usuario(string userName, string email, string fullName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);

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
