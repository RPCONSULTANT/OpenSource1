using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace OpenSource1.Infrastructure.Identity;

public sealed class AppIdentityDbContext(DbContextOptions<AppIdentityDbContext> options)
    : IdentityDbContext<Usuario>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Usuario>(entity =>
        {
            entity.Property(user => user.FullName).HasMaxLength(200);
            entity.Property(user => user.CreatedAtUtc).HasDefaultValueSql("SYSDATETIMEOFFSET()").IsRequired();
            entity.Property(user => user.IsActive).HasDefaultValue(true).IsRequired();
        });
    }
}
