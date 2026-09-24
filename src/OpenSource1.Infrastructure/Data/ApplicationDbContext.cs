using Microsoft.EntityFrameworkCore;
using OpenSource1.Core.Entities;

namespace OpenSource1.Infrastructure.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<Entrada>    Entradas    => Set<Entrada>();
    public DbSet<Cliente>    Clientes    => Set<Cliente>();
    public DbSet<Producto>   Productos   => Set<Producto>();
    public DbSet<TerminoPago> TerminosPago => Set<TerminoPago>();
    public DbSet<UnidadMedida> UnidadesMedida => Set<UnidadMedida>();
    public DbSet<UnidadMedidaProducto> UnidadesMedidaProducto => Set<UnidadMedidaProducto>();
    public DbSet<Serie>      Series       => Set<Serie>();
    public DbSet<LineaSerie> LineasSerie  => Set<LineaSerie>();

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
            // Filtro parcial: excluye las filas borradas logicamente para que una Key pueda
            // reutilizarse tras un soft delete. Sin el filtro, el HasQueryFilter de abajo oculta
            // la fila fantasma de los chequeos de existencia (que sí respetan el filtro global),
            // pero el INSERT de un registro nuevo con la misma Key sigue chocando contra el
            // indice unico a nivel de Postgres, que no sabe nada del filtro de EF.
            entity.HasIndex(setting => setting.Key).IsUnique().HasFilter("\"IsDeleted\" = false");
            entity.HasIndex(setting => setting.CreatedAtUtc).HasDatabaseName("IX_AppSettings_CreatedAtUtc");
            entity.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();
            entity.Property(setting => setting.IsDeleted).HasDefaultValue(false);
            entity.Property(setting => setting.DeletedBy).HasMaxLength(100);
            entity.HasQueryFilter(setting => !setting.IsDeleted);
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
            entity.HasIndex(e => e.CreatedAtUtc).HasDatabaseName("IX_Entradas_CreatedAtUtc");
            entity.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.DeletedBy).HasMaxLength(100);
            entity.HasQueryFilter(e => !e.IsDeleted);
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
            entity.HasIndex(x => x.CreatedAtUtc).HasDatabaseName("IX_Clientes_CreatedAtUtc");
            entity.HasIndex(x => x.Email).HasDatabaseName("IX_Clientes_Email");
            entity.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();
            entity.Property(x => x.IsDeleted).HasDefaultValue(false);
            entity.Property(x => x.DeletedBy).HasMaxLength(100);
            entity.HasQueryFilter(x => !x.IsDeleted);

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
            // Mismo filtro parcial que AppSettings.Key: ver comentario de arriba.
            entity.HasIndex(x => x.Codigo).IsUnique().HasFilter("\"IsDeleted\" = false");
            entity.HasIndex(x => x.CreatedAtUtc).HasDatabaseName("IX_Productos_CreatedAtUtc");
            entity.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();
            entity.Property(x => x.IsDeleted).HasDefaultValue(false);
            entity.Property(x => x.DeletedBy).HasMaxLength(100);
            entity.HasQueryFilter(x => !x.IsDeleted);

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

        modelBuilder.Entity<TerminoPago>(entity =>
        {
            entity.ToTable("TerminosPago");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Codigo).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Descripcion).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PorcentajeDescuento).HasPrecision(9, 5);
            entity.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
            entity.Property(x => x.UpdatedBy).HasMaxLength(100);
            // Índice único parcial desde el primer momento (Hallazgo 1 de la revisión final de la
            // Fase 0-1): sin el filtro "IsDeleted = false", el HasQueryFilter de abajo oculta la
            // fila borrada lógicamente del chequeo de existencia, pero un INSERT posterior con el
            // mismo Código sigue chocando contra el índice único a nivel de Postgres.
            entity.HasIndex(x => x.Codigo).IsUnique().HasFilter("\"IsDeleted\" = false");
            entity.HasIndex(x => x.CreatedAtUtc).HasDatabaseName("IX_TerminosPago_CreatedAtUtc");
            entity.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();
            entity.Property(x => x.IsDeleted).HasDefaultValue(false);
            entity.Property(x => x.DeletedBy).HasMaxLength(100);
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<UnidadMedida>(entity =>
        {
            entity.ToTable("UnidadesMedida");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Codigo).HasMaxLength(10).IsRequired();
            entity.Property(x => x.Nombre).HasMaxLength(50).IsRequired();
            entity.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
            entity.Property(x => x.UpdatedBy).HasMaxLength(100);
            // Índice único parcial desde el primer momento (mismo patrón que TerminoPago): sin el
            // filtro "IsDeleted = false", el HasQueryFilter de abajo oculta la fila borrada
            // lógicamente del chequeo de existencia, pero un INSERT posterior con el mismo Código
            // sigue chocando contra el índice único a nivel de Postgres (500 en vez de 201).
            entity.HasIndex(x => x.Codigo).IsUnique().HasFilter("\"IsDeleted\" = false");
            entity.HasIndex(x => x.CreatedAtUtc).HasDatabaseName("IX_UnidadesMedida_CreatedAtUtc");
            entity.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();
            entity.Property(x => x.IsDeleted).HasDefaultValue(false);
            entity.Property(x => x.DeletedBy).HasMaxLength(100);
            entity.HasQueryFilter(x => !x.IsDeleted);

            // Catálogo inicial: el que vivía hardcodeado en UnidadMedidaLegado, para que Producto
            // (Task 2.9) tenga a qué apuntar al migrar sus filas existentes. Ids fijos para que
            // el seed sea determinista entre entornos y migraciones.
            entity.HasData(
                Semilla("a1000000-0000-0000-0000-000000000001", "UND", "Unidad", 0),
                Semilla("a1000000-0000-0000-0000-000000000002", "KG", "Kilogramo", 3),
                Semilla("a1000000-0000-0000-0000-000000000003", "GR", "Gramo", 0),
                Semilla("a1000000-0000-0000-0000-000000000004", "LT", "Litro", 3),
                Semilla("a1000000-0000-0000-0000-000000000005", "ML", "Mililitro", 0),
                Semilla("a1000000-0000-0000-0000-000000000006", "CJA", "Caja", 0),
                Semilla("a1000000-0000-0000-0000-000000000007", "DOC", "Docena", 0),
                Semilla("a1000000-0000-0000-0000-000000000008", "PAQ", "Paquete", 0),
                Semilla("a1000000-0000-0000-0000-000000000009", "MT", "Metro", 2),
                Semilla("a1000000-0000-0000-0000-000000000010", "LB", "Libra", 3));
        });

        modelBuilder.Entity<UnidadMedidaProducto>(entity =>
        {
            entity.ToTable("UnidadesMedidaProducto", t =>
                t.HasCheckConstraint("CK_UnidadMedidaProducto_Cantidad_Positiva", "\"CantidadPorUnidadMedida\" > 0"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CantidadPorUnidadMedida).HasPrecision(18, 6);
            entity.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
            entity.Property(x => x.UpdatedBy).HasMaxLength(100);
            entity.HasOne<Producto>().WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<UnidadMedida>().WithMany().HasForeignKey(x => x.UnidadMedidaId).OnDelete(DeleteBehavior.Restrict);
            // Parcial por la misma razón que el índice de UnidadMedida.Codigo: permite volver a
            // asociar una unidad a un producto tras borrar lógicamente la equivalencia anterior.
            entity.HasIndex(x => new { x.ProductoId, x.UnidadMedidaId }).IsUnique().HasFilter("\"IsDeleted\" = false");
            entity.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();
            entity.Property(x => x.IsDeleted).HasDefaultValue(false);
            entity.Property(x => x.DeletedBy).HasMaxLength(100);
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<Serie>(entity =>
        {
            entity.ToTable("Series");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Codigo).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Descripcion).HasMaxLength(200).IsRequired();
            entity.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
            entity.Property(x => x.UpdatedBy).HasMaxLength(100);
            // Mismo patrón de índice único parcial que TerminoPago/Producto: ver comentario allí.
            entity.HasIndex(x => x.Codigo).IsUnique().HasFilter("\"IsDeleted\" = false");
            entity.HasIndex(x => x.CreatedAtUtc).HasDatabaseName("IX_Series_CreatedAtUtc");
            entity.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();
            entity.Property(x => x.IsDeleted).HasDefaultValue(false);
            entity.Property(x => x.DeletedBy).HasMaxLength(100);
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<LineaSerie>(entity =>
        {
            entity.ToTable("LineasSerie");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.NumeroInicial).HasMaxLength(50).IsRequired();
            entity.Property(x => x.NumeroFinal).HasMaxLength(50).IsRequired();
            entity.Property(x => x.UltimoNumeroUsado).HasMaxLength(50).IsRequired();
            entity.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
            entity.Property(x => x.UpdatedBy).HasMaxLength(100);
            entity.HasIndex(x => x.CreatedAtUtc).HasDatabaseName("IX_LineasSerie_CreatedAtUtc");
            entity.HasIndex(x => x.SerieId).HasDatabaseName("IX_LineasSerie_SerieId");
            entity.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();
            entity.Property(x => x.IsDeleted).HasDefaultValue(false);
            entity.Property(x => x.DeletedBy).HasMaxLength(100);
            entity.HasQueryFilter(x => !x.IsDeleted);

            // Nunca se borra una Serie con líneas asociadas: Restrict en vez de Cascade.
            entity.HasOne<Serie>()
                .WithMany()
                .HasForeignKey(x => x.SerieId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static readonly DateTimeOffset FechaSemilla = new(2026, 9, 24, 0, 0, 0, TimeSpan.Zero);

    // HasData con tipo anónimo: Id tiene setter protegido en AggregateRoot, así que no se puede
    // asignar en un inicializador de objeto de la entidad.
    private static object Semilla(string id, string codigo, string nombre, short decimales) => new
    {
        Id = Guid.Parse(id),
        Codigo = codigo,
        Nombre = nombre,
        Decimales = decimales,
        CreatedAtUtc = FechaSemilla,
        CreatedBy = "system",
        IsDeleted = false
    };
}
