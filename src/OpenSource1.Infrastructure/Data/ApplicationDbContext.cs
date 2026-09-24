using Microsoft.EntityFrameworkCore;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Enums;

namespace OpenSource1.Infrastructure.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<Entrada>    Entradas    => Set<Entrada>();
    public DbSet<SocioNegocio> SociosNegocio => Set<SocioNegocio>();
    public DbSet<Producto>   Productos   => Set<Producto>();
    public DbSet<TerminoPago> TerminosPago => Set<TerminoPago>();
    public DbSet<UnidadMedida> UnidadesMedida => Set<UnidadMedida>();
    public DbSet<UnidadMedidaProducto> UnidadesMedidaProducto => Set<UnidadMedidaProducto>();
    public DbSet<CategoriaProducto> CategoriasProducto => Set<CategoriaProducto>();
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

        modelBuilder.Entity<SocioNegocio>(entity =>
        {
            entity.ToTable("SociosNegocio");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Codigo).HasMaxLength(20).IsRequired();
            // Tipo, TipoDocumentoFiscal y Bloqueado son enums con underlying short: EF los guarda como
            // smallint sin conversión. Tipo/TipoDocumentoFiscal no tienen default de BD: los rellena
            // siempre el handler y la migración los puebla para las filas existentes.
            entity.Property(x => x.NombreComercial).HasMaxLength(200).IsRequired();
            entity.Property(x => x.RazonSocial).HasMaxLength(200);
            entity.Property(x => x.NumeroDocumentoFiscal).HasMaxLength(20);
            // Email es opcional desde la Task 2.6 (Ruling M): dos socios pueden compartir correo, por
            // eso el índice sigue siendo NO único.
            entity.Property(x => x.Email).HasMaxLength(256);
            entity.Property(x => x.Telefono).HasMaxLength(50);
            entity.Property(x => x.Ciudad).HasMaxLength(100);
            entity.Property(x => x.LimiteCredito).HasPrecision(18, 4).HasDefaultValue(0m);
            entity.Property(x => x.Bloqueado).HasDefaultValue(BloqueoSocioNegocio.Ninguno);
            entity.Property(x => x.ImagePath).HasMaxLength(500);
            entity.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
            entity.Property(x => x.UpdatedBy).HasMaxLength(100);
            // Parciales por la misma razón que en TerminoPago: el código y el documento fiscal de un
            // socio borrado lógicamente deben poder reutilizarse.
            entity.HasIndex(x => x.Codigo).IsUnique().HasFilter("\"IsDeleted\" = false");
            entity.HasIndex(x => x.NumeroDocumentoFiscal).IsUnique()
                .HasFilter("\"IsDeleted\" = false AND \"NumeroDocumentoFiscal\" IS NOT NULL");
            entity.HasIndex(x => x.CreatedAtUtc).HasDatabaseName("IX_SociosNegocio_CreatedAtUtc");
            entity.HasIndex(x => x.Email).HasDatabaseName("IX_SociosNegocio_Email");
            entity.HasOne<TerminoPago>().WithMany().HasForeignKey(x => x.TerminoPagoId).OnDelete(DeleteBehavior.Restrict);
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

        modelBuilder.Entity<CategoriaProducto>(entity =>
        {
            entity.ToTable("CategoriasProducto");
            entity.HasKey(x => x.Id);
            // Máximos tomados del value object legado CategoriaProductoLegado (CategoriaCodigo/CategoriaNombre).
            entity.Property(x => x.Codigo).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
            entity.Property(x => x.UpdatedBy).HasMaxLength(100);
            // Índice único parcial desde el primer momento (mismo patrón que TerminoPago/UnidadMedida):
            // permite reutilizar el Código de una categoría borrada lógicamente.
            entity.HasIndex(x => x.Codigo).IsUnique().HasFilter("\"IsDeleted\" = false");
            entity.HasIndex(x => x.CreatedAtUtc).HasDatabaseName("IX_CategoriasProducto_CreatedAtUtc");
            entity.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();
            entity.Property(x => x.IsDeleted).HasDefaultValue(false);
            entity.Property(x => x.DeletedBy).HasMaxLength(100);
            entity.HasQueryFilter(x => !x.IsDeleted);

            // Jerarquía autorreferencial: nunca se borra en cascada (ni física ni lógicamente un
            // padre arrastra a sus hijos; el handler de borrado rechaza el borrado si hay hijos).
            entity.HasOne<CategoriaProducto>().WithMany().HasForeignKey(x => x.CategoriaPadreId).OnDelete(DeleteBehavior.Restrict);

            // Categoría por defecto: la Task 2.9 la usará como destino de las categorías legadas de
            // Producto sin coincidencia. Id y fecha fijos para que el seed sea determinista.
            entity.HasData(new
            {
                Id = Guid.Parse("c1000000-0000-0000-0000-000000000001"),
                Codigo = "GENERAL",
                Nombre = "General",
                CreatedAtUtc = FechaSemilla,
                CreatedBy = "system",
                IsDeleted = false
            });
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
