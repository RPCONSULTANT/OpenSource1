using Microsoft.EntityFrameworkCore;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Entities.Inventario;
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
    public DbSet<Almacen>    Almacenes    => Set<Almacen>();
    public DbSet<MovimientoProducto> MovimientosProducto => Set<MovimientoProducto>();
    public DbSet<MovimientoValor> MovimientosValor => Set<MovimientoValor>();
    public DbSet<AplicacionMovimientoProducto> AplicacionesMovimientoProducto => Set<AplicacionMovimientoProducto>();
    public DbSet<PlantillaDiario> PlantillasDiario => Set<PlantillaDiario>();
    public DbSet<LoteDiario> LotesDiario => Set<LoteDiario>();
    public DbSet<LineaDiario> LineasDiario => Set<LineaDiario>();
    public DbSet<RegistroDiario> RegistrosDiario => Set<RegistroDiario>();
    public DbSet<CuentaContable> CuentasContables => Set<CuentaContable>();
    public DbSet<GrupoNegocio> GruposNegocio => Set<GrupoNegocio>();
    public DbSet<GrupoProducto> GruposProducto => Set<GrupoProducto>();
    public DbSet<GrupoIvaNegocio> GruposIvaNegocio => Set<GrupoIvaNegocio>();
    public DbSet<GrupoIvaProducto> GruposIvaProducto => Set<GrupoIvaProducto>();
    public DbSet<GrupoInventario> GruposInventario => Set<GrupoInventario>();
    public DbSet<GrupoClienteContable> GruposClienteContable => Set<GrupoClienteContable>();

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
            // Clasificación contable (Task 5.3): FK nulables sin navegación; el borrado de grupos es lógico y su handler
            // rechaza el borrado en uso (409), Restrict es la red de seguridad ante un borrado físico.
            entity.HasOne<GrupoNegocio>().WithMany().HasForeignKey(x => x.GrupoNegocioId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<GrupoIvaNegocio>().WithMany().HasForeignKey(x => x.GrupoIvaNegocioId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<GrupoClienteContable>().WithMany().HasForeignKey(x => x.GrupoClienteContableId).OnDelete(DeleteBehavior.Restrict);
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
            entity.Property(x => x.PrecioVenta).HasPrecision(18, 4);
            entity.Property(x => x.CostoUnitario).HasPrecision(18, 4);
            entity.Property(x => x.CostoEstandar).HasPrecision(18, 4);
            // MetodoCosteo, Bloqueado (enums con underlying short: smallint) y CostoAjustado no llevan default de BD: los rellena
            // siempre el handler y la migración los puebla para las filas existentes (HasDefaultValue sobre un bool ignoraría
            // un false explícito, porque coincide con el valor CLR por defecto).
            // Restrict: el borrado del catálogo es lógico y los handlers de borrado de categoría/unidad rechazan el uso (409);
            // la FK es la red de seguridad ante un borrado físico.
            entity.HasOne<UnidadMedida>().WithMany().HasForeignKey(x => x.UnidadMedidaBaseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CategoriaProducto>().WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
            // Clasificación contable (Task 5.3): mismas reglas que las FK de SocioNegocio a sus grupos.
            entity.HasOne<GrupoProducto>().WithMany().HasForeignKey(x => x.GrupoProductoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<GrupoIvaProducto>().WithMany().HasForeignKey(x => x.GrupoIvaProductoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<GrupoInventario>().WithMany().HasForeignKey(x => x.GrupoInventarioId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
            entity.Property(x => x.UpdatedBy).HasMaxLength(100);
            // Mismo filtro parcial que AppSettings.Key: ver comentario de arriba.
            entity.HasIndex(x => x.Codigo).IsUnique().HasFilter("\"IsDeleted\" = false");
            entity.HasIndex(x => x.CreatedAtUtc).HasDatabaseName("IX_Productos_CreatedAtUtc");
            entity.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();
            entity.Property(x => x.IsDeleted).HasDefaultValue(false);
            entity.Property(x => x.DeletedBy).HasMaxLength(100);
            entity.HasQueryFilter(x => !x.IsDeleted);
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

            // Catálogo inicial (el que vivía hardcodeado antes de existir el catálogo administrable), para que
            // Producto tenga a qué apuntar al migrar sus filas existentes. Ids fijos para que el seed sea
            // determinista entre entornos y migraciones.
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
            // Máximos heredados de las columnas de texto libre que tuvo Producto (CategoriaCodigo/CategoriaNombre).
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

            // Categoría por defecto: destino de las categorías legadas de Producto sin coincidencia y valor por
            // defecto de un alta sin categoría. Id y fecha fijos para que el seed sea determinista.
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

            // Serie DIARIO-INV (Fase 4, Task 4.2): a diferencia de SOCIOS (Task 2.9, sembrada con SQL a mano en
            // ExtendSocioNegocio porque el contador dependía de filas ya migradas), esta serie no depende de datos
            // existentes: HasData con Id fijo, igual que UnidadesMedida/CategoriaProducto GENERAL.
            entity.HasData(new
            {
                Id = SerieDiarioInventarioIds.SerieId,
                Codigo = "DIARIO-INV",
                Descripcion = "Diarios de inventario",
                PermiteHuecos = false,
                PorDefecto = false,
                CreatedAtUtc = FechaSemilla,
                CreatedBy = "system",
                IsDeleted = false
            });
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

            // Línea vigente de DIARIO-INV desde 2020-01-01, contador en 000000 (siguiente: 000001). NUNCA editar
            // "UltimoNumeroUsado" aquí en una migración futura: EF recalcularía esta semilla en cada migración
            // subsiguiente comparándola contra el valor fijo de HasData, deshaciendo el avance real del contador. Lo
            // actualiza en runtime, por SQL directo, GeneradorNumeroDocumento.SiguienteAsync (UPDATE de una sola
            // columna dentro de la transacción del llamador) — nunca EF ni una migración.

            entity.HasData(new
            {
                Id = SerieDiarioInventarioIds.LineaSerieId,
                SerieId = SerieDiarioInventarioIds.SerieId,
                NumeroInicial = "000001",
                NumeroFinal = "999999",
                UltimoNumeroUsado = "000000",
                FechaInicial = new DateOnly(2020, 1, 1),
                Incremento = 1,
                Bloqueada = false,
                CreatedAtUtc = FechaSemilla,
                CreatedBy = "system",
                IsDeleted = false
            });
        });

        modelBuilder.Entity<Almacen>(entity =>
        {
            entity.ToTable("Almacenes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Codigo).HasMaxLength(10).IsRequired();
            entity.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
            entity.Property(x => x.DireccionLinea1).HasMaxLength(300);
            entity.Property(x => x.DireccionLinea2).HasMaxLength(300);
            entity.Property(x => x.Ciudad).HasMaxLength(100);
            entity.Property(x => x.PaisCodigo).HasMaxLength(2);
            entity.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
            entity.Property(x => x.UpdatedBy).HasMaxLength(100);
            // Mismo patrón de índice único parcial que TerminoPago/UnidadMedida: permite reutilizar
            // el Código de un almacén borrado lógicamente.
            entity.HasIndex(x => x.Codigo).IsUnique().HasFilter("\"IsDeleted\" = false");
            // A lo sumo un almacén predeterminado entre los no borrados. El índice parcial es la
            // red de seguridad final: los handlers de alta/modificación ya mueven la marca en
            // transacción, pero dos altas concurrentes con EsPredeterminado = true solo pueden
            // dejar una fila viva gracias a este índice (23505 en la perdedora -> 409).
            entity.HasIndex(x => x.EsPredeterminado)
                .IsUnique()
                .HasDatabaseName("IX_Almacenes_EsPredeterminado")
                .HasFilter("\"EsPredeterminado\" = true AND \"IsDeleted\" = false");
            entity.HasIndex(x => x.CreatedAtUtc).HasDatabaseName("IX_Almacenes_CreatedAtUtc");
            entity.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();
            entity.Property(x => x.IsDeleted).HasDefaultValue(false);
            entity.Property(x => x.DeletedBy).HasMaxLength(100);
            entity.HasQueryFilter(x => !x.IsDeleted);

            // Almacén principal: destino por defecto de la apertura del libro de inventario
            // (Task 3.6) y único predeterminado al migrar. Id y fecha fijos para que el seed sea
            // determinista entre entornos y migraciones.
            entity.HasData(new
            {
                Id = AlmacenIds.Principal,
                Codigo = "PRINCIPAL",
                Nombre = "Almacén principal",
                Bloqueado = false,
                EsPredeterminado = true,
                CreatedAtUtc = new DateTimeOffset(2026, 9, 25, 0, 0, 0, TimeSpan.Zero),
                CreatedBy = "system",
                IsDeleted = false
            });
        });

        modelBuilder.Entity<MovimientoProducto>(entity =>
        {
            entity.ToTable("MovimientosProducto", t =>
            {
                t.HasCheckConstraint("CK_MovimientosProducto_Cantidad_NoCero", "\"Cantidad\" <> 0");
                t.HasCheckConstraint(
                    "CK_MovimientosProducto_Restante",
                    "\"CantidadRestante\" IS NULL OR (\"CantidadRestante\" >= 0 AND \"CantidadRestante\" <= \"Cantidad\")");
                t.HasCheckConstraint("CK_MovimientosProducto_Factor_Positivo", "\"CantidadPorUnidadMedida\" > 0");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).UseIdentityAlwaysColumn();
            entity.Property(x => x.NumeroDocumento).HasMaxLength(20);
            entity.Property(x => x.Cantidad).HasPrecision(18, 6);
            entity.Property(x => x.CantidadRestante).HasPrecision(18, 6);
            entity.Property(x => x.CantidadFacturada).HasPrecision(18, 6);
            entity.Property(x => x.CantidadPorUnidadMedida).HasPrecision(18, 6);
            entity.Property(x => x.ClaveOrigen).HasMaxLength(50).IsRequired();
            entity.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();

            // Libro append-only: las tablas de este esquema no llevan HasQueryFilter propio, y las FK de
            // abajo hacia maestros con borrado lógico (Productos, Almacenes, UnidadesMedida, SociosNegocio)
            // se declaran SOLO como columna + HasForeignKey, sin navegación de ida ni de vuelta. Sin una
            // navegación que EF tenga que anular al aplicar el query filter del principal, no se dispara el
            // aviso de "required end with query filter" y un movimiento sigue siendo visible aunque el
            // producto/almacén al que apunta se borre lógicamente después.
            entity.HasOne<Producto>().WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Almacen>().WithMany().HasForeignKey(x => x.AlmacenId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<UnidadMedida>().WithMany().HasForeignKey(x => x.UnidadMedidaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<SocioNegocio>().WithMany().HasForeignKey(x => x.SocioNegocioId).OnDelete(DeleteBehavior.Restrict);

            // Aplicación FIFO: candidatas a consumir son las entradas vivas (CantidadRestante > 0) de un
            // producto en un almacén, en orden de fecha y luego de Id (desempate estable dentro del mismo día).
            entity.HasIndex(x => new { x.ProductoId, x.AlmacenId, x.FechaRegistro, x.Id })
                .HasDatabaseName("IX_MovimientosProducto_Fifo")
                .HasFilter("\"CantidadRestante\" > 0");

            // Guarda de borrado de almacén (DeleteAlmacenCommandHandler): "¿existe algún movimiento con este AlmacenId?".
            entity.HasIndex(x => x.AlmacenId).HasDatabaseName("IX_MovimientosProducto_AlmacenId");

            // Índices del spec que faltaban (revisión final de la Fase 3): localizar el/los movimientos de un
            // documento de origen (reversión, consulta de un documento ya posteado) y los de una clave de origen
            // (idempotencia del subsistema que lo generó). Sin datos que los usen todavía en esta fase, pero el
            // spec los pide igual y sin ellos cualquier consulta por estas columnas sería un Seq Scan completo.
            entity.HasIndex(x => new { x.TipoDocumento, x.NumeroDocumento })
                .HasDatabaseName("IX_MovimientosProducto_TipoDocumento_NumeroDocumento");
            entity.HasIndex(x => new { x.TipoOrigen, x.ClaveOrigen })
                .HasDatabaseName("IX_MovimientosProducto_TipoOrigen_ClaveOrigen");

            // Existencia derivada (Task 3.6, corrección de rendimiento): el único índice que arranca en ProductoId es el
            // parcial de FIFO (CantidadRestante > 0, arriba), así que GetByIdAsync/ /existencias/la agregación del listado
            // (SUM("Cantidad") por ProductoId, o por ProductoId+AlmacenId en ExistenciasPorAlmacenAsync) recorrían el libro
            // entero. Índice NO parcial (cubre entradas y salidas) con INCLUDE para que Postgres resuelva la suma con un
            // Index Only Scan sin volver al heap.
            entity.HasIndex(x => new { x.ProductoId, x.AlmacenId })
                .HasDatabaseName("IX_MovimientosProducto_Existencia")
                .IncludeProperties(x => new { x.Cantidad, x.FechaRegistro });
        });

        modelBuilder.Entity<MovimientoValor>(entity =>
        {
            entity.ToTable("MovimientosValor");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).UseIdentityAlwaysColumn();
            entity.Property(x => x.CantidadValorada).HasPrecision(18, 6);
            entity.Property(x => x.CantidadFacturada).HasPrecision(18, 6);
            entity.Property(x => x.ImporteCosto).HasPrecision(18, 4);
            entity.Property(x => x.CostoPorUnidad).HasPrecision(18, 4);
            entity.Property(x => x.ImporteVenta).HasPrecision(18, 4);
            entity.Property(x => x.ImporteCostoPosteadoContabilidad).HasPrecision(18, 4).HasDefaultValue(0m);
            entity.Property(x => x.NumeroDocumento).HasMaxLength(20);
            entity.Property(x => x.ClaveOrigen).HasMaxLength(50).IsRequired();
            entity.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();

            // Mismo motivo que en MovimientoProducto: FK a maestros sin navegación.
            entity.HasOne<Producto>().WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Almacen>().WithMany().HasForeignKey(x => x.AlmacenId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<MovimientoProducto>().WithMany().HasForeignKey(x => x.MovimientoProductoId).OnDelete(DeleteBehavior.Restrict);

            // Cola de posteo a contabilidad: filas cuyo importe de costo aún no coincide con lo posteado.
            entity.HasIndex(x => new { x.ProductoId, x.AlmacenId, x.FechaRegistro })
                .HasDatabaseName("IX_MovimientosValor_PendientePosteoContabilidad")
                .HasFilter("\"ImporteCosto\" <> \"ImporteCostoPosteadoContabilidad\"");

            // Índice del spec que faltaba (revisión final de la Fase 3): el índice de arriba es PARCIAL (solo filas
            // pendientes de posteo), así que no sirve para el filtro por (ProductoId, FechaRegistro) SIN esa
            // condición que usan CostoPromedioCalculadora.SumasSql (el costo promedio móvil, en cada posteo) y la
            // carga por producto de AjusteCostoInventario.AjustarProductoAsync (todos los movimientos de valor del
            // producto): sin este índice, ambas consultas hacían un Seq Scan de TODA la tabla. No parcial, para
            // cubrir las filas ya posteadas también.
            entity.HasIndex(x => new { x.ProductoId, x.FechaRegistro })
                .HasDatabaseName("IX_MovimientosValor_ProductoId_FechaRegistro");
        });

        modelBuilder.Entity<AplicacionMovimientoProducto>(entity =>
        {
            entity.ToTable("AplicacionesMovimientoProducto", t =>
                t.HasCheckConstraint("CK_Aplicaciones_Cantidad_Positiva", "\"Cantidad\" > 0"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).UseIdentityAlwaysColumn();
            entity.Property(x => x.Cantidad).HasPrecision(18, 6);

            // Dos FK al mismo libro (MovimientosProducto), sin navegación en ninguna dirección: EF las
            // distingue por la propiedad FK, no colisionan entre sí.
            entity.HasOne<MovimientoProducto>().WithMany().HasForeignKey(x => x.MovimientoEntradaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<MovimientoProducto>().WithMany().HasForeignKey(x => x.MovimientoSalidaId).OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.MovimientoEntradaId).HasDatabaseName("IX_AplicacionesMovimientoProducto_MovimientoEntradaId");
            entity.HasIndex(x => x.MovimientoSalidaId).HasDatabaseName("IX_AplicacionesMovimientoProducto_MovimientoSalidaId");
        });

        modelBuilder.Entity<PlantillaDiario>(entity =>
        {
            entity.ToTable("PlantillasDiario");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Codigo).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
            entity.Property(x => x.UpdatedBy).HasMaxLength(100);
            // Mismo patrón de índice único parcial que TerminoPago/Almacen, aunque en la práctica nunca se borra
            // (sembrada y de solo lectura, sin CRUD: ver "Desviaciones acordadas" de la Fase 4).
            entity.HasIndex(x => x.Codigo).IsUnique().HasFilter("\"IsDeleted\" = false");
            entity.HasIndex(x => x.CreatedAtUtc).HasDatabaseName("IX_PlantillasDiario_CreatedAtUtc");
            entity.HasOne<Serie>().WithMany().HasForeignKey(x => x.SerieId).OnDelete(DeleteBehavior.Restrict);
            entity.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();
            entity.Property(x => x.IsDeleted).HasDefaultValue(false);
            entity.Property(x => x.DeletedBy).HasMaxLength(100);
            entity.HasQueryFilter(x => !x.IsDeleted);

            // Las dos únicas plantillas (Task 4.2): ambas con la serie DIARIO-INV. Ids fijos (PlantillaDiarioIds) para
            // que la Task 4.3 (posteo) pueda referenciarlas sin volver a consultarlas por Código.
            entity.HasData(
                new
                {
                    Id = PlantillaDiarioIds.Articulo,
                    Codigo = "ARTICULO",
                    Nombre = "Diario de artículos",
                    Tipo = TipoPlantillaDiario.Articulo,
                    SerieId = SerieDiarioInventarioIds.SerieId,
                    CreatedAtUtc = FechaSemilla,
                    CreatedBy = "system",
                    IsDeleted = false
                },
                new
                {
                    Id = PlantillaDiarioIds.Reclasificacion,
                    Codigo = "RECLASIF",
                    Nombre = "Diario de reclasificación",
                    Tipo = TipoPlantillaDiario.Reclasificacion,
                    SerieId = SerieDiarioInventarioIds.SerieId,
                    CreatedAtUtc = FechaSemilla,
                    CreatedBy = "system",
                    IsDeleted = false
                });
        });

        modelBuilder.Entity<LoteDiario>(entity =>
        {
            entity.ToTable("LotesDiario");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Codigo).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
            entity.Property(x => x.UpdatedBy).HasMaxLength(100);
            // Único junto con PlantillaDiarioId (parcial: permite reutilizar el Código de un lote borrado lógicamente).
            entity.HasIndex(x => new { x.PlantillaDiarioId, x.Codigo }).IsUnique().HasFilter("\"IsDeleted\" = false");
            entity.HasIndex(x => x.CreatedAtUtc).HasDatabaseName("IX_LotesDiario_CreatedAtUtc");
            // PlantillaDiarioId es inmutable tras el alta (ver CreateLoteDiarioCommand): nunca se borra la plantilla
            // (sembrada, sin CRUD), así que Restrict es solo la red de seguridad final.
            entity.HasOne<PlantillaDiario>().WithMany().HasForeignKey(x => x.PlantillaDiarioId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Serie>().WithMany().HasForeignKey(x => x.SerieId).OnDelete(DeleteBehavior.Restrict);
            entity.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();
            entity.Property(x => x.IsDeleted).HasDefaultValue(false);
            entity.Property(x => x.DeletedBy).HasMaxLength(100);
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<LineaDiario>(entity =>
        {
            entity.ToTable("LineasDiario", t => t.HasCheckConstraint("CK_LineasDiario_Cantidad_Positiva", "\"Cantidad\" > 0"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.NumeroDocumento).HasMaxLength(20);
            entity.Property(x => x.CantidadPorUnidadMedida).HasPrecision(18, 6);
            entity.Property(x => x.Cantidad).HasPrecision(18, 6);
            entity.Property(x => x.CostoUnitario).HasPrecision(18, 4);
            entity.Property(x => x.ImporteCosto).HasPrecision(18, 4);
            entity.Property(x => x.Descripcion).HasMaxLength(200);
            entity.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
            entity.Property(x => x.UpdatedBy).HasMaxLength(100);

            // Único junto con LoteDiarioId (parcial, mismo patrón que arriba): lo asigna el sistema (máximo del
            // lote + 10000), pero dos altas concurrentes en el mismo lote podrían calcular el mismo máximo; el índice
            // es la red de seguridad final (23505 -> 409 genérico).
            entity.HasIndex(x => new { x.LoteDiarioId, x.NumeroLinea }).IsUnique().HasFilter("\"IsDeleted\" = false");
            entity.HasIndex(x => x.CreatedAtUtc).HasDatabaseName("IX_LineasDiario_CreatedAtUtc");

            // Sin navegación en ningún caso (mismo patrón que MovimientoProducto/AplicacionMovimientoProducto):
            // LoteDiarioId es inmutable; ProductoId/AlmacenId/UnidadMedidaId son borrado lógico (Restrict); dos FK a
            // Almacenes (origen y destino) se distinguen por la propiedad FK, sin colisionar.
            entity.HasOne<LoteDiario>().WithMany().HasForeignKey(x => x.LoteDiarioId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Producto>().WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Almacen>().WithMany().HasForeignKey(x => x.AlmacenId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Almacen>().WithMany().HasForeignKey(x => x.AlmacenDestinoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<UnidadMedida>().WithMany().HasForeignKey(x => x.UnidadMedidaId).OnDelete(DeleteBehavior.Restrict);

            entity.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();
            entity.Property(x => x.IsDeleted).HasDefaultValue(false);
            entity.Property(x => x.DeletedBy).HasMaxLength(100);
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<RegistroDiario>(entity =>
        {
            // Append-only (Task 4.3): mismo trigger que el libro (migración AddRegistrosDiario), sin xmin ni soft delete.
            entity.ToTable("RegistrosDiario");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).UseIdentityAlwaysColumn();
            entity.Property(x => x.NumeroRegistro).HasMaxLength(20).IsRequired();
            entity.Property(x => x.CreadoPor).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.NumeroRegistro).IsUnique();

            // Sin navegación (mismo motivo que en MovimientoProducto): el registro sigue visible aunque el lote se borre
            // lógicamente después. El índice de la FK sirve también al listado GET registros?loteId=.
            entity.HasOne<LoteDiario>().WithMany().HasForeignKey(x => x.LoteDiarioId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CuentaContable>(entity =>
        {
            entity.ToTable("CuentasContables");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Numero).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
            entity.Property(x => x.UpdatedBy).HasMaxLength(100);
            // Mismo patrón de índice único parcial que Almacen/TerminoPago/UnidadMedida: permite
            // reutilizar el Número de una cuenta borrada lógicamente.
            entity.HasIndex(x => x.Numero).IsUnique().HasFilter("\"IsDeleted\" = false");
            entity.HasIndex(x => x.CreatedAtUtc).HasDatabaseName("IX_CuentasContables_CreatedAtUtc");
            entity.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();
            entity.Property(x => x.IsDeleted).HasDefaultValue(false);
            entity.Property(x => x.DeletedBy).HasMaxLength(100);
            entity.HasQueryFilter(x => !x.IsDeleted);

            // Plan de cuentas semilla (Task 5.2): las Tasks 5.3-5.6 (setups contables, grupos de cliente, libro
            // contable) referencian estas cuentas por Id fijo (CuentaContableIds). Sangria = 0 para los
            // encabezados (Activos/Pasivos/Ingresos/Costos), 1 para sus cuentas de Posteo hijas.
            entity.HasData(
                SemillaCuenta(CuentaContableIds.Activos, "1", "Activos", TipoCuentaContable.Encabezado, TipoResultadoCuenta.Balance, false, 0),
                SemillaCuenta(CuentaContableIds.Caja, "1101", "Caja", TipoCuentaContable.Posteo, TipoResultadoCuenta.Balance, true, 1),
                SemillaCuenta(CuentaContableIds.CxC, "1201", "Cuentas por cobrar clientes", TipoCuentaContable.Posteo, TipoResultadoCuenta.Balance, false, 1),
                SemillaCuenta(CuentaContableIds.Inventario, "1301", "Inventario de mercancías", TipoCuentaContable.Posteo, TipoResultadoCuenta.Balance, false, 1),
                SemillaCuenta(CuentaContableIds.Pasivos, "2", "Pasivos", TipoCuentaContable.Encabezado, TipoResultadoCuenta.Balance, false, 0),
                SemillaCuenta(CuentaContableIds.IvaPorPagar, "2101", "ITBIS por pagar", TipoCuentaContable.Posteo, TipoResultadoCuenta.Balance, false, 1),
                SemillaCuenta(CuentaContableIds.Ingresos, "4", "Ingresos", TipoCuentaContable.Encabezado, TipoResultadoCuenta.Resultado, false, 0),
                SemillaCuenta(CuentaContableIds.Ventas, "4101", "Ventas", TipoCuentaContable.Posteo, TipoResultadoCuenta.Resultado, true, 1),
                SemillaCuenta(CuentaContableIds.DescuentoVentas, "4102", "Descuentos sobre ventas", TipoCuentaContable.Posteo, TipoResultadoCuenta.Resultado, true, 1),
                SemillaCuenta(CuentaContableIds.Costos, "5", "Costos", TipoCuentaContable.Encabezado, TipoResultadoCuenta.Resultado, false, 0),
                SemillaCuenta(CuentaContableIds.CostoVentas, "5101", "Costo de ventas", TipoCuentaContable.Posteo, TipoResultadoCuenta.Resultado, false, 1),
                SemillaCuenta(CuentaContableIds.AjusteInventario, "5201", "Ajustes de inventario", TipoCuentaContable.Posteo, TipoResultadoCuenta.Resultado, true, 1));
        });

        // Grupos contables (Task 5.3): cinco tablas simples con la misma forma, una por tipo (spec 5.2), más
        // GruposClienteContable, que además lleva cuentas. Semillas con Ids fijos (GrupoContableIds).
        ConfigurarGrupo<GrupoNegocio>(modelBuilder, "GruposNegocio",
            SemillaGrupo(GrupoContableIds.NegocioNacional, "NACIONAL", "Socios nacionales"),
            SemillaGrupo(GrupoContableIds.NegocioExterior, "EXTERIOR", "Socios del exterior"));
        ConfigurarGrupo<GrupoProducto>(modelBuilder, "GruposProducto",
            SemillaGrupo(GrupoContableIds.ProductoBienes, "BIENES", "Bienes"),
            SemillaGrupo(GrupoContableIds.ProductoServicios, "SERVICIOS", "Servicios"));
        ConfigurarGrupo<GrupoIvaNegocio>(modelBuilder, "GruposIvaNegocio",
            SemillaGrupo(GrupoContableIds.IvaNegocioItbis18, "ITBIS18", "Sujeto a ITBIS 18%"),
            SemillaGrupo(GrupoContableIds.IvaNegocioExento, "EXENTO", "Exento de ITBIS"));
        ConfigurarGrupo<GrupoIvaProducto>(modelBuilder, "GruposIvaProducto",
            SemillaGrupo(GrupoContableIds.IvaProductoItbis18, "ITBIS18", "Gravado con ITBIS 18%"),
            SemillaGrupo(GrupoContableIds.IvaProductoExento, "EXENTO", "Exento de ITBIS"));
        ConfigurarGrupo<GrupoInventario>(modelBuilder, "GruposInventario",
            SemillaGrupo(GrupoContableIds.InventarioGeneral, "GENERAL", "Inventario general"));

        modelBuilder.Entity<GrupoClienteContable>(entity =>
        {
            ConfigurarColumnasGrupo(entity, "GruposClienteContable");
            // Tres FK a CuentasContables sin navegación (se distinguen por la propiedad FK). Borrar una cuenta usada aquí
            // la rechaza la guarda de uso de cuentas (409); Restrict es la red de seguridad ante un borrado físico.
            entity.HasOne<CuentaContable>().WithMany().HasForeignKey(x => x.CuentaCxCId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CuentaContable>().WithMany().HasForeignKey(x => x.CuentaDescuentoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CuentaContable>().WithMany().HasForeignKey(x => x.CuentaInteresId).OnDelete(DeleteBehavior.Restrict);
            entity.HasData(new
            {
                Id = GrupoContableIds.ClienteContableGeneral,
                Codigo = "GENERAL",
                Descripcion = "Clientes en general",
                CuentaCxCId = CuentaContableIds.CxC,
                CuentaDescuentoId = (Guid?)CuentaContableIds.DescuentoVentas,
                CuentaInteresId = (Guid?)null,
                CreatedAtUtc = FechaSemilla,
                CreatedBy = "system",
                IsDeleted = false
            });
        });
    }

    private static void ConfigurarGrupo<TGrupo>(ModelBuilder modelBuilder, string tabla, params object[] semillas)
        where TGrupo : GrupoContable =>
        modelBuilder.Entity<TGrupo>(entity =>
        {
            ConfigurarColumnasGrupo(entity, tabla);
            entity.HasData(semillas);
        });

    private static void ConfigurarColumnasGrupo<TGrupo>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TGrupo> entity, string tabla)
        where TGrupo : GrupoContable
    {
        entity.ToTable(tabla);
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Codigo).HasMaxLength(20).IsRequired();
        entity.Property(x => x.Descripcion).HasMaxLength(100).IsRequired();
        entity.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        entity.Property(x => x.UpdatedBy).HasMaxLength(100);
        // Mismo patrón de índice único parcial que el resto de maestros: permite reutilizar el Código de un grupo borrado.
        entity.HasIndex(x => x.Codigo).IsUnique().HasFilter("\"IsDeleted\" = false");
        entity.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();
        entity.Property(x => x.IsDeleted).HasDefaultValue(false);
        entity.Property(x => x.DeletedBy).HasMaxLength(100);
        entity.HasQueryFilter(x => !x.IsDeleted);
    }

    private static object SemillaGrupo(Guid id, string codigo, string descripcion) => new
    {
        Id = id,
        Codigo = codigo,
        Descripcion = descripcion,
        CreatedAtUtc = FechaSemilla,
        CreatedBy = "system",
        IsDeleted = false
    };

    private static readonly DateTimeOffset FechaSemilla = new(2026, 9, 24, 0, 0, 0, TimeSpan.Zero);

    private static object SemillaCuenta(
        Guid id, string numero, string nombre, TipoCuentaContable tipoCuenta, TipoResultadoCuenta tipoResultado,
        bool posteoDirecto, int sangria) => new
    {
        Id = id,
        Numero = numero,
        Nombre = nombre,
        TipoCuenta = tipoCuenta,
        TipoResultado = tipoResultado,
        PosteoDirecto = posteoDirecto,
        Bloqueada = false,
        Sangria = sangria,
        CreatedAtUtc = FechaSemilla,
        CreatedBy = "system",
        IsDeleted = false
    };

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
