using Microsoft.AspNetCore.Identity;

namespace test.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
}
