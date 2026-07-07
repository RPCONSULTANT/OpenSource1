using Microsoft.AspNetCore.Identity;

namespace OpenSource1.Infrastructure.Identity;

public class Usuario : IdentityUser
{
    public string? FullName { get; set; }
    public string? ProfileImagePath { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
