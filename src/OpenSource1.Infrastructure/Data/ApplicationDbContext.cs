using Microsoft.EntityFrameworkCore;
using OpenSource1.Core.Entities;

namespace OpenSource1.Infrastructure.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<Entrada>    Entradas    => Set<Entrada>();
    public DbSet<Cliente>    Clientes    => Set<Cliente>();
    public DbSet<Producto>   Productos   => Set<Producto>();

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

        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.ToTable("Clientes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Apellido).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Telefono).HasMaxLength(50);
            entity.Property(x => x.ImagePath).HasMaxLength(500);
            entity.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
            entity.Property(x => x.UpdatedBy).HasMaxLength(100);

            entity.ComplexProperty(x => x.Direccion, direccion =>
            {
                direccion.Property(d => d.Linea1).HasColumnName("DireccionLinea1").HasMaxLength(300);
                direccion.Property(d => d.Linea2).HasColumnName("DireccionLinea2").HasMaxLength(300);
            });
            entity.ComplexProperty(x => x.Pais, pais =>
            {
                pais.Property(p => p.Codigo).HasColumnName("PaisCodigo").HasMaxLength(2);
                pais.Property(p => p.Nombre).HasColumnName("PaisNombre").HasMaxLength(100);
            });
            entity.ComplexProperty(x => x.Sector, sector =>
            {
                sector.Property(s => s.Nombre).HasColumnName("Sector").HasMaxLength(100);
            });
        });

        modelBuilder.Entity<Producto>(entity =>
        {
            entity.ToTable("Productos");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Codigo).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ImagePath).HasMaxLength(500);
            entity.Property(x => x.Precio).HasPrecision(18, 2);
            entity.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
            entity.Property(x => x.UpdatedBy).HasMaxLength(100);
            entity.HasIndex(x => x.Codigo).IsUnique();

            entity.ComplexProperty(x => x.Categoria, categoria =>
            {
                categoria.Property(c => c.Codigo).HasColumnName("CategoriaCodigo").HasMaxLength(30).IsRequired();
                categoria.Property(c => c.Nombre).HasColumnName("CategoriaNombre").HasMaxLength(100).IsRequired();
            });
            entity.ComplexProperty(x => x.UnidadMedida, unidad =>
            {
                unidad.Property(u => u.Codigo).HasColumnName("UnidadMedidaCodigo").HasMaxLength(10).IsRequired();
                unidad.Property(u => u.Nombre).HasColumnName("UnidadMedidaNombre").HasMaxLength(50).IsRequired();
            });
        });
    }
}
