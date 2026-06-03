using Microsoft.AspNetCore.Identity;

namespace test.Identity;

public class Usuario : IdentityUser
{
    public string? FullName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
