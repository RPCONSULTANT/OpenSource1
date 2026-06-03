using Microsoft.EntityFrameworkCore;
using OpenSource1.Core.Entities;

namespace OpenSource1.Infrastructure.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.ToTable("AppSettings");
            entity.HasKey(setting => setting.Id);
            entity.Property(setting => setting.Key).HasMaxLength(150).IsRequired();
            entity.Property(setting => setting.Value).HasMaxLength(1_000).IsRequired();
            entity.Property(setting => setting.Description).HasMaxLength(500);
            entity.HasIndex(setting => setting.Key).IsUnique();
        });
    }
}
