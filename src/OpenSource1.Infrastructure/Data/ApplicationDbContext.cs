using Microsoft.EntityFrameworkCore;
using OpenSource1.Core.Entities;

namespace OpenSource1.Infrastructure.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<Entrada>    Entradas    => Set<Entrada>();

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
            entity.Property(setting => setting.CreatedBy).HasMaxLength(100).IsRequired();
            entity.Property(setting => setting.UpdatedBy).HasMaxLength(100);
            entity.HasIndex(setting => setting.Key).IsUnique();
        });

        modelBuilder.Entity<Entrada>(entity =>
        {
            entity.ToTable("Entradas");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Titulo).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Descripcion).HasMaxLength(1_000);
            entity.Property(e => e.Tipo).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Estado).HasMaxLength(50).IsRequired();
            entity.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
            entity.Property(e => e.UpdatedBy).HasMaxLength(100);
        });
    }
}
