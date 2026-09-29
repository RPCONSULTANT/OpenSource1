# Fase 2 — Dominio maestro · Plan de implementación

> **Para ejecutores:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development.
> Pasos con checkbox (`- [ ]`).

**Goal:** convertir `Cliente` en `SocioDeNegocio` (cliente+proveedor unificado), dar a
`UnidadMedida` un catálogo administrable con conversión, extender `Producto` con los
campos que necesita inventario/facturación, y montar la numeración de documentos sin
huecos que usarán las Fases 4 y 6.

**Architecture:** cada tabla maestra nueva sigue el patrón CQRS-lite ya establecido:
EF Core para escritura vía `IUnitOfWork`, Dapper vía `IDbSession` para lectura paginada,
MediatR para orquestar, `Result`/`ErroresDeDominioException` para errores, Blazor Static
SSR para la UI. El renombre de `Cliente` replica exactamente esa estructura, campo por
campo, sobre el nuevo esquema.

**Tech Stack:** .NET 10, EF Core 10 + Npgsql, Dapper, MediatR 13, xUnit, Docker/Postgres real.

**Spec:** [`docs/superpowers/specs/2026-09-12-axionerp-erp-modules-design.md`](../specs/2026-09-12-axionerp-erp-modules-design.md), sección "Fase 2 — Dominio maestro"

## Global Constraints

- TFM `net10.0`; nullable e implicit usings habilitados. Solución `test.slnx`.
- `PackageReference` directo por proyecto, sin CPM.
- Blazor Static SSR: sin `@rendermode`, sin `@onclick`, sin handlers interactivos.
- Nombres en español. Precisión: cantidades `numeric(18,6)`, importes `numeric(18,4)`,
  porcentajes `numeric(9,5)`.
- Commits locales en `feat/erp-fase-2-dominio-maestro` (ramificada desde
  `feat/erp-fase-0-1-kernel`, que ya tiene PR abierto). Prohibido merge/push a `main` o a
  la rama base. Mensajes convencionales, sin líneas de atribución.
- Docker/Postgres está disponible: toda verificación corre contra base real. Filtro de
  tests preciso: `dotnet test test.slnx --filter "FullyQualifiedName!~OpenSource1.SmokeTests.Api."`
  para excluir solo los de integración cuando haga falta; por defecto correr la suite
  completa sin filtro.
- Ya existen y NO se redefinen: `Result`/`Result<T>`/`Error`/`ErroresDeDominioException`
  (`src/OpenSource1.Core/Common/`), `PageRequest`/`PagedResult<T>`, `IDbSession`,
  `ColumnasPermitidas`/`FilterExpressionBuilder`, `IUnitOfWork` con API de transacción,
  los 3 pipeline behaviors de MediatR (`ICommand<T>`/`IQuery<T>` existen pero **ningún**
  comando/query los usa todavía — este plan tampoco los adopta; el spec dice que el
  Fase 2 es donde `Cliente` se reescribe, así que es la primera oportunidad natural, pero
  adoptarlos AHORA es opcional y queda fuera de este plan salvo que un task lo diga
  explícitamente).
- **Patrón de value object obligatorio para todo lo nuevo:** constructor **privado** +
  factory estático `Of(...)` con parámetro `nombreCampo` opcional al final — el mismo
  patrón que ya usan `Pais`/`UnidadMedida` y al que se migraron `DireccionCliente`/
  `CategoriaProducto`/`Sector`/`Usuario` tras el incidente del Lote D2. **Nunca** un
  constructor público con parámetros que EF Core deba enlazar si el VO se mapea con
  `ComplexProperty` — es la causa exacta de esa emergencia.
- **Toda migración se genera con `dotnet ef migrations add`, nunca a mano**, salvo que se
  tope con el mismo tipo de bug de Npgsql que forzó editar `AddSoftDelete` — en ese caso,
  documentar igual que se documentó allí, y verificar con una migración de prueba después
  que sale vacía.
- **`ApplicationDbContextModelTests` es la red de seguridad no negociable.** Confirmarla
  en verde después de CADA cambio a `OnModelCreating`, no solo al final de la fase.

---

## Task 2.1 — `TerminosPago`

Tabla maestra pequeña e independiente, prerrequisito de `SocioDeNegocio`.

**Files:**
- Create: `src/OpenSource1.Core/Entities/TerminoPago.cs`
- Modify: `src/OpenSource1.Infrastructure/Data/ApplicationDbContext.cs` (nuevo `DbSet`, mapeo)
- Create: migración `AddTerminosPago`
- Create: `src/OpenSource1.Application/Features/TerminosPago/` completo (Commands, Queries,
  Handlers, Dtos, `ITerminoPagoReadRepository`) — sigue el patrón de
  `src/OpenSource1.Application/Features/AppSettings/` (es la feature más simple existente:
  sin relaciones, CRUD llano) en vez del de Clientes/Productos.
- Create: `src/OpenSource1.Infrastructure/Data/Queries/DapperTerminoPagoReadRepository.cs`
- Create: `src/OpenSource1.Api/Controllers/TerminosPagoController.cs`
- Modify: `src/OpenSource1.Infrastructure/Data/DependencyInjection.cs` (registrar el repo)
- Test: `tests/OpenSource1.SmokeTests/Features/TerminosPago/` + `tests/OpenSource1.SmokeTests/Api/TerminosPagoApiTests.cs`

**Interfaces:**
- Produces: `TerminoPago` entity con `Id uuid`, `Codigo string` (UNIQUE, parcial por
  `IsDeleted`), `Descripcion string`, `DiasVencimiento int`, `DiasDescuento int`,
  `PorcentajeDescuento decimal`. Consumida por la Task 2.5 (`SocioDeNegocio.TerminoPagoId`).

- [ ] **Step 1: Entidad**

```csharp
namespace OpenSource1.Core.Entities;

public sealed class TerminoPago : BaseEntity
{
    public required string Codigo { get; set; }
    public required string Descripcion { get; set; }
    public int DiasVencimiento { get; set; }
    public int DiasDescuento { get; set; }
    public decimal PorcentajeDescuento { get; set; }
}
```

- [ ] **Step 2: Mapeo EF**

En `OnModelCreating`, siguiendo exactamente el patrón de `AppSetting` (índice único
parcial por `IsDeleted`, índice en `CreatedAtUtc`, `xmin`, soft delete, filtro global):

```csharp
modelBuilder.Entity<TerminoPago>(entity =>
{
    entity.ToTable("TerminosPago");
    entity.HasKey(x => x.Id);
    entity.Property(x => x.Codigo).HasMaxLength(20).IsRequired();
    entity.Property(x => x.Descripcion).HasMaxLength(200).IsRequired();
    entity.Property(x => x.PorcentajeDescuento).HasPrecision(9, 5);
    entity.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
    entity.Property(x => x.UpdatedBy).HasMaxLength(100);
    entity.HasIndex(x => x.Codigo).IsUnique().HasFilter("\"IsDeleted\" = false");
    entity.HasIndex(x => x.CreatedAtUtc).HasDatabaseName("IX_TerminosPago_CreatedAtUtc");
    entity.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();
    entity.Property(x => x.IsDeleted).HasDefaultValue(false);
    entity.Property(x => x.DeletedBy).HasMaxLength(100);
    entity.HasQueryFilter(x => !x.IsDeleted);
});
```

Nota: aplica el índice único **parcial** desde el primer momento (no como parche
posterior) — es la lección del Hallazgo 1 de la revisión final de la Fase 0-1.

- [ ] **Step 3: Migración**

```bash
dotnet ef migrations add AddTerminosPago --project src/OpenSource1.Infrastructure --startup-project src/OpenSource1.Api --output-dir Data/Migrations/Application
```

Verificar `ApplicationDbContextModelTests` en verde.

- [ ] **Step 4: Application — CRUD completo**

Sigue el patrón de `AppSettings` (`Features/AppSettings/`) adaptado:
`CreateTerminoPagoCommand`/`UpdateTerminoPagoCommand`/`DeleteTerminoPagoCommand`,
`GetTerminoPagoByIdQuery`/`ListTerminosPagoQuery` con `PageRequest`/`Result<PagedResult<T>>`
(usa el mismo patrón que `IClienteReadRepository.ListAsync` — con `Result`, no la versión
antigua sin paginar). Los handlers de escritura devuelven `Result<TerminoPagoResponse>`
(no el `TerminoPagoResponse` directo sin envolver, que es como está hoy `Cliente` —
aquí SÍ usa `Result` porque es código nuevo, no una migración de código existente).

- [ ] **Step 5: Infrastructure — repositorio Dapper**

Sigue el patrón de `DapperClienteReadRepository` (visto arriba): `ColumnasPermitidas`
estática (`"Codigo"`, `"Descripcion"`, `"CreatedAtUtc"`), `GetByIdAsync` +
`ListAsync(criterios, paginacion)` con `COUNT` + `SELECT ... ORDER BY {col} {dir}, "Id" ASC LIMIT/OFFSET`.

- [ ] **Step 6: Api — controller**

Sigue el patrón de `ClientesController` pero con las 5 acciones estándar (`List`,
`GetById`, `Create`, `Update`, `Delete`) usando `ResultExtensions.ToActionResult()` para
las respuestas de `Result` (no las validaciones manuales con `if` que tiene
`ClientesController` hoy — esas son deuda vieja, no las repliques).

- [ ] **Step 7: Verificar**

```bash
dotnet build test.slnx
dotnet test test.slnx
```

- [ ] **Step 8: Commit**

---

## Task 2.2 — Numeración de documentos (`Series`, `LineasSerie`, `IGeneradorNumeroDocumento`)

Independiente de `SocioDeNegocio`/`Producto`. Es la pieza más delicada de esta fase:
gapless implica bloqueo de fila real bajo concurrencia.

**Files:**
- Create: `src/OpenSource1.Core/Entities/Serie.cs`, `src/OpenSource1.Core/Entities/LineaSerie.cs`
- Modify: `ApplicationDbContext.cs` (2 `DbSet`, mapeo, relación `LineaSerie → Serie`)
- Create: migración `AddSeriesNumeracion`
- Create: `src/OpenSource1.Application/Data/IGeneradorNumeroDocumento.cs`
- Create: `src/OpenSource1.Infrastructure/Data/GeneradorNumeroDocumento.cs`
- Modify: `src/OpenSource1.Infrastructure/Data/DependencyInjection.cs` (registrar scoped)
- Test: `tests/OpenSource1.SmokeTests/Infrastructure/GeneradorNumeroDocumentoTests.cs`
  (requiere Postgres real — concurrencia genuina, no simulable con mocks)

**Interfaces:**
- Produces:
  ```csharp
  public interface IGeneradorNumeroDocumento
  {
      Task<Result<string>> SiguienteAsync(string codigoSerie, DateOnly fecha, CancellationToken cancellationToken = default);
  }
  ```
  Consumida por las Fases 4 y 6 (diarios de inventario, facturas) — no tiene
  consumidores en esta fase, solo la infraestructura.

- [ ] **Step 1: Entidades**

```csharp
namespace OpenSource1.Core.Entities;

public sealed class Serie : BaseEntity
{
    public required string Codigo { get; set; }
    public required string Descripcion { get; set; }
    public bool PermiteHuecos { get; set; }
    public bool PorDefecto { get; set; }
}

public sealed class LineaSerie : BaseEntity
{
    public Guid SerieId { get; set; }
    public required string NumeroInicial { get; set; }
    public required string NumeroFinal { get; set; }
    public required string UltimoNumeroUsado { get; set; }
    public DateOnly FechaInicial { get; set; }
    public int Incremento { get; set; } = 1;
    public bool Bloqueada { get; set; }
}
```

- [ ] **Step 2: Mapeo EF**

`Serie.Codigo` único parcial por `IsDeleted`. `LineaSerie` con FK a `Serie` (`HasOne`/
`WithMany`, `OnDelete(DeleteBehavior.Restrict)` — nunca se borra una serie con líneas).
Ambas con `xmin`, soft delete, índice en `CreatedAtUtc`, siguiendo el patrón ya
establecido.

- [ ] **Step 3: Migración**

```bash
dotnet ef migrations add AddSeriesNumeracion --project src/OpenSource1.Infrastructure --startup-project src/OpenSource1.Api --output-dir Data/Migrations/Application
```

Verificar `ApplicationDbContextModelTests`.

- [ ] **Step 4: `IGeneradorNumeroDocumento` — la parte delicada**

El número se compone de `NumeroInicial` incrementado por `UltimoNumeroUsado`. Estrategia:
`UltimoNumeroUsado` guarda el **último entero emitido** como string (para permitir
prefijos alfabéticos en `NumeroInicial`/`NumeroFinal` si se necesitan después, aunque hoy
solo se emite el componente numérico). El siguiente número es
`ParseNumero(UltimoNumeroUsado) + Incremento`, formateado con el mismo ancho de ceros que
`NumeroInicial`.

```csharp
public sealed class GeneradorNumeroDocumento(IDbSession session) : IGeneradorNumeroDocumento
{
    public async Task<Result<string>> SiguienteAsync(string codigoSerie, DateOnly fecha, CancellationToken cancellationToken = default)
    {
        await session.EnsureOpenAsync(cancellationToken);

        // Requiere una transacción activa: FOR UPDATE sin transacción no retiene el bloqueo
        // más allá del statement. El llamante (un handler de posteo en Fases 4/6) debe estar
        // dentro de un IUnitOfWork.BeginTransactionAsync.
        if (session.CurrentTransaction is null)
        {
            return Result<string>.Fallo(new Error(
                "numeracion.sin_transaccion",
                "La generación de números de documento requiere una transacción activa."));
        }

        const string selectSerieSql = """
            SELECT "Id", "PermiteHuecos" FROM "Series"
            WHERE "Codigo" = @Codigo AND "IsDeleted" = false
            """;
        var serie = await session.Connection.QuerySingleOrDefaultAsync<(Guid Id, bool PermiteHuecos)?>(
            new CommandDefinition(selectSerieSql, new { Codigo = codigoSerie }, session.CurrentTransaction, cancellationToken: cancellationToken));

        if (serie is null)
        {
            return Result<string>.Fallo(new Error("numeracion.serie_inexistente", $"No existe la serie '{codigoSerie}'.", nameof(codigoSerie)));
        }

        // FOR UPDATE reserva la fila hasta el commit/rollback de la transacción actual.
        // Sin PermiteHuecos, dos transacciones concurrentes sobre la misma serie se serializan
        // aquí: la segunda espera a que la primera haga commit o rollback.
        const string selectLineaSql = """
            SELECT "Id", "NumeroInicial", "NumeroFinal", "UltimoNumeroUsado", "Incremento", "Bloqueada"
            FROM "LineasSerie"
            WHERE "SerieId" = @SerieId AND "FechaInicial" <= @Fecha AND "Bloqueada" = false AND "IsDeleted" = false
            ORDER BY "FechaInicial" DESC
            LIMIT 1
            FOR UPDATE
            """;
        var linea = await session.Connection.QuerySingleOrDefaultAsync<LineaSerieRow?>(
            new CommandDefinition(selectLineaSql, new { SerieId = serie.Value.Id, Fecha = fecha }, session.CurrentTransaction, cancellationToken: cancellationToken));

        if (linea is null)
        {
            return Result<string>.Fallo(new Error("numeracion.sin_linea_vigente", $"La serie '{codigoSerie}' no tiene una línea vigente para la fecha {fecha}.", nameof(fecha)));
        }

        var ultimo = long.Parse(linea.Value.UltimoNumeroUsado);
        var siguiente = ultimo + linea.Value.Incremento;
        var maximo = long.Parse(linea.Value.NumeroFinal);

        if (siguiente > maximo)
        {
            return Result<string>.Fallo(new Error("numeracion.serie_agotada", $"La serie '{codigoSerie}' agotó su rango numérico.", nameof(codigoSerie)));
        }

        var numeroFormateado = siguiente.ToString().PadLeft(linea.Value.NumeroInicial.Length, '0');

        const string updateSql = """
            UPDATE "LineasSerie" SET "UltimoNumeroUsado" = @Nuevo WHERE "Id" = @Id
            """;
        await session.Connection.ExecuteAsync(
            new CommandDefinition(updateSql, new { Nuevo = siguiente.ToString(), Id = linea.Value.Id }, session.CurrentTransaction, cancellationToken: cancellationToken));

        return Result<string>.Exito(numeroFormateado);
    }

    private sealed record LineaSerieRow(Guid Id, string NumeroInicial, string NumeroFinal, string UltimoNumeroUsado, int Incremento, bool Bloqueada);
}
```

Con `PermiteHuecos = true` en la `Serie`, el llamante puede optar por no envolver la
llamada en transacción — pero el diseño de esta tarea no distingue el camino sin
bloqueo todavía (se resuelve en la Fase 4/6 cuando haya un consumidor real). Por ahora,
`SiguienteAsync` **siempre** exige transacción activa; documentar esto en el XML doc de
la interfaz.

- [ ] **Step 2: Test de concurrencia real — obligatorio, requiere Postgres**

```csharp
[Fact]
public async Task SiguienteAsync_ConcurrenciaReal_NoProduceNumerosDuplicados()
{
    // Sembrar una Serie + LineaSerie con NumeroInicial="00001", NumeroFinal="00100", UltimoNumeroUsado="00000".
    // Lanzar 10 tareas en paralelo, cada una con su propio scope/IDbSession/transacción,
    // cada una llamando SiguienteAsync + commit.
    // Assert: los 10 números devueltos son todos distintos y consecutivos (00001..00010),
    // sin huecos y sin duplicados.
}
```

Este test es la verificación real de que `FOR UPDATE` serializa correctamente — un mock
no lo puede probar.

- [ ] **Step 3: Verificar**

```bash
dotnet build test.slnx
dotnet test test.slnx
```

- [ ] **Step 4: Commit**

---

## Task 2.3 — `UnidadesMedida` administrable + conversión

Reemplaza el VO estático `UnidadMedida` (hoy un diccionario hardcodeado en Core) por
tablas reales con factor de conversión.

**Files:**
- Create: `src/OpenSource1.Core/Entities/UnidadMedida.cs` (entidad EF, no VO — el nombre
  choca con el VO existente; **renombrar el VO existente a `UnidadMedidaCodigo`** si algo
  todavía lo necesita como VO puro, o eliminarlo si su único uso era `Producto.UnidadMedida`,
  que se reemplaza en la Task 2.9)
- Create: `src/OpenSource1.Core/Entities/UnidadMedidaProducto.cs`
- Delete: `src/OpenSource1.Core/ValueObjects/UnidadMedida.cs` (el catálogo estático)
- Modify: `ApplicationDbContext.cs`
- Create: migración `AddUnidadesMedida`
- Create: `src/OpenSource1.Application/Features/UnidadesMedida/` (CRUD del catálogo,
  patrón de la Task 2.1)
- Create: `src/OpenSource1.Application/Services/Inventario/IConversionUnidadMedidaService.cs`
- Create: `src/OpenSource1.Infrastructure/Services/Inventario/ConversionUnidadMedidaService.cs`
- Test: `tests/OpenSource1.SmokeTests/Services/ConversionUnidadMedidaServiceTests.cs`

**Interfaces:**
- Produces: `UnidadMedida` (entidad, `Id uuid`, `Codigo` único, `Nombre`, `Decimales smallint`);
  `UnidadMedidaProducto` (`Id`, `ProductoId`, `UnidadMedidaId`, `CantidadPorUnidadMedida decimal`);
  `IConversionUnidadMedidaService.ConvertirABase(Guid productoId, Guid unidadMedidaId, decimal cantidad) : Task<Result<decimal>>`.
  Consumida por la Task 2.9 (`Producto.UnidadMedidaBaseId`) y por las Fases 3/4/6.

- [ ] **Step 1: Entidades**

```csharp
namespace OpenSource1.Core.Entities;

public sealed class UnidadMedida : BaseEntity
{
    public required string Codigo { get; set; }
    public required string Nombre { get; set; }
    public short Decimales { get; set; }
}

public sealed class UnidadMedidaProducto : BaseEntity
{
    public Guid ProductoId { get; set; }
    public Guid UnidadMedidaId { get; set; }
    public decimal CantidadPorUnidadMedida { get; set; }
}
```

- [ ] **Step 2: Mapeo EF**

`UnidadMedida.Codigo` único parcial. `UnidadMedidaProducto`: FK a `Producto` y a
`UnidadMedida` (`Restrict`), único compuesto `(ProductoId, UnidadMedidaId)`,
`CantidadPorUnidadMedida` con `HasPrecision(18, 6)` y un `CHECK` (`> 0`) —
`entity.ToTable(t => t.HasCheckConstraint("CK_UnidadMedidaProducto_Cantidad_Positiva", "\"CantidadPorUnidadMedida\" > 0"))`.

- [ ] **Step 3: Datos semilla**

En la misma migración (`migrationBuilder.InsertData` o `Sql`), sembrar el catálogo que
hoy vive hardcodeado en el VO: `UND`/Unidad, `KG`/Kilogramo, `GR`/Gramo, `LT`/Litro,
`ML`/Mililitro, `CJA`/Caja, `DOC`/Docena, `PAQ`/Paquete, `MT`/Metro, `LB`/Libra — para no
perder los datos que el catálogo estático ya representaba, y que `Producto` (Task 2.9)
tenga a qué apuntar al migrar sus filas existentes.

- [ ] **Step 4: `IConversionUnidadMedidaService`**

```csharp
public interface IConversionUnidadMedidaService
{
    Task<Result<decimal>> ConvertirABaseAsync(Guid productoId, Guid unidadMedidaId, decimal cantidad, CancellationToken cancellationToken = default);
}
```

Implementación: busca la fila de `UnidadMedidaProducto` para `(productoId, unidadMedidaId)`;
si no existe, `Result.Fallo(new Error("conversion.unidad_no_asociada", ...))`; si existe,
`cantidad * CantidadPorUnidadMedida`, redondeado a `UnidadMedida.Decimales` del producto
base (no de la unidad de entrada).

- [ ] **Step 5: CRUD del catálogo `UnidadesMedida`**

Sigue el patrón de la Task 2.1 (`TerminosPago`): Application + Dapper repo + controller.
No hace falta CRUD separado para `UnidadMedidaProducto` en esta tarea — se gestiona como
parte del alta/edición de `Producto` en la Task 2.9.

- [ ] **Step 6: Verificar**

```bash
dotnet build test.slnx
dotnet test test.slnx
```

- [ ] **Step 7: Commit**

---

## Task 2.4 — `CategoriasProducto`

**Files:**
- Create: `src/OpenSource1.Core/Entities/CategoriaProducto.cs` (entidad EF — choca de
  nombre con el VO `CategoriaProducto` existente; **eliminar el VO** en esta tarea, ya
  que su único uso es `Producto.Categoria`, reemplazado en la Task 2.9)
- Delete: `src/OpenSource1.Core/ValueObjects/CategoriaProducto.cs`
- Modify: `ApplicationDbContext.cs`
- Create: migración `AddCategoriasProducto`
- Create: `src/OpenSource1.Application/Features/CategoriasProducto/` (CRUD, patrón Task 2.1)

**Interfaces:**
- Produces: `CategoriaProducto` (`Id`, `Codigo` único, `Nombre`, `CategoriaPadreId Guid?`
  autorreferencial). Consumida por la Task 2.9 (`Producto.CategoriaId`).

- [ ] **Step 1: Entidad**

```csharp
namespace OpenSource1.Core.Entities;

public sealed class CategoriaProducto : BaseEntity
{
    public required string Codigo { get; set; }
    public required string Nombre { get; set; }
    public Guid? CategoriaPadreId { get; set; }
}
```

- [ ] **Step 2: Mapeo EF**

`Codigo` único parcial. Relación autorreferencial `HasOne().WithMany().HasForeignKey(x => x.CategoriaPadreId).OnDelete(DeleteBehavior.Restrict)`
(nunca borrar en cascada una jerarquía de categorías).

- [ ] **Step 3: Migración + datos semilla**

```bash
dotnet ef migrations add AddCategoriasProducto --project src/OpenSource1.Infrastructure --startup-project src/OpenSource1.Api --output-dir Data/Migrations/Application
```

Sembrar una categoría `GENERAL`/"General" en la misma migración, para que los `Productos`
existentes tengan a qué apuntar al migrar en la Task 2.9.

- [ ] **Step 4: CRUD**

Patrón de la Task 2.1.

- [ ] **Step 5: Verificar y commit**

---

## Task 2.5 — Renombre mecánico `Cliente` → `SocioDeNegocio` (SIN campos nuevos)

**Reestructurada (Ruling K).** El plan original mezclaba renombre y extensión de campos en una sola
migración con transformación de datos, y pedía quitar `Cliente` del modelo mientras 22 directorios aún
compilaban contra él. Se separa en *expand/contract*: esta tarea es un renombre PURO, verificable por
compilación y suite, sin cambio de comportamiento; la 2.6 añade los campos y migra los datos.

**Alcance (mecánico, en TODAS las capas a la vez, para que la solución compile en cada commit):**
- Core: `Cliente` → `SocioDeNegocio` (mismas propiedades: Nombre, Apellido, Email, Telefono, Direccion,
  Pais, Sector, ImagePath); VO `DireccionCliente` → `DireccionFiscal` (constructor privado + `Of`; columnas
  EF `DireccionLinea1/2` intactas).
- Infrastructure: `DbSet` `Clientes` → `SociosNegocio`; `ToTable("SociosNegocio")`; repositorio
  `DapperClienteReadRepository` → `DapperSocioNegocioReadRepository` (SQL sobre `"SociosNegocio"`).
  **Migración `RenameClienteToSocioNegocio`: EF generará `DropTable`+`CreateTable` porque cambia el tipo de la
  entidad. Debes EDITARLA A MANO para que use `RenameTable` + `RenameIndex`** (`IX_Clientes_CreatedAtUtc` →
  `IX_SociosNegocio_CreatedAtUtc`, `IX_Clientes_Email` → `IX_SociosNegocio_Email`) y documentar el motivo en
  la migración. Verificación no negociable: aplicar la migración anterior con 2–3 `Clientes` sembrados y
  comprobar que sobreviven tras aplicar la nueva; una migración de prueba posterior debe salir VACÍA.
- Application: `Features/Clientes` → `Features/SociosNegocio` (carpetas, comandos, queries, handlers, DTO
  `SocioNegocioResponse`, `SocioNegocioSearchCriteria`, `ISocioNegocioReadRepository`).
- Api: `ClientesController` → `SociosNegocioController`, ruta `api/socios-negocio`. Comportamiento y
  validaciones IGUALES (no las refactorices aquí).
- Blazor: `IClienteApiClient`/`ClienteApiClient` → `ISocioNegocioApiClient`/`SocioNegocioApiClient` apuntando a
  `api/socios-negocio`, con los DTO renombrados. **La interfaz de usuario NO se renombra**: rutas `/clientes`,
  etiquetas "Clientes" y nombres de páginas (`Clientes.razor`, `ClienteNew.razor`, `ClienteDetail.razor`,
  `ClientesDashboard.razor`) se conservan; en la UI un socio de tipo cliente sigue siendo un cliente y
  renombrar rutas arriesga enlaces, exports y reportes sin beneficio. Reportería/Excel/PDF/dashboards solo
  cambian tipos y nombres de DTO.
- Tests: carpetas, archivos y clases renombrados; `ClientesApiTests` → `SociosNegocioApiTests` con la ruta nueva.
- NO cambia: columnas, validaciones, permisos, ni el JSON de respuesta salvo el nombre de la ruta.

**Verificación:** build + suite completa; `ApplicationDbContextModelTests` en verde; migración con datos
(ver arriba); y **EN EJECUCIÓN** (API + Blazor + Postgres de Docker, login `admin`/`Password123`): pega los
estados HTTP reales del flujo de clientes ya existente — listado con filtros y paginación, alta
(multipart), detalle, edición, eliminación, dashboard de clientes, exportación Excel y reporte PDF de
clientes, y la página de inicio (`Home`) que los usa.

## Task 2.6 — Extender `SocioDeNegocio`: campos, migración de datos y numeración

**Reestructurada (Rulings K, M).** Sobre el modelo ya renombrado de la 2.5.

**Campos nuevos** (sección 2.1 del spec, salvo los grupos contables, que llegan en la Fase 5):
`Codigo` (`varchar(20)`), `Tipo` (Cliente=1/Proveedor=2/Ambos=3), `NombreComercial` (`varchar(200)`),
`RazonSocial` (`varchar(200)`, opcional), `TipoDocumentoFiscal` (Rnc=1/Cedula=2/Pasaporte=3/SinDocumento=9),
`NumeroDocumentoFiscal` (`varchar(20)`, opcional), `Ciudad` (`varchar(100)`), `TerminoPagoId` (FK opcional a
`TerminosPago`, `Restrict`), `LimiteCredito` (`numeric(18,4)`, default 0), `Bloqueado`
(Ninguno=0/Facturacion=1/Todo=2, `smallint`, default 0). Enums guardados como `smallint`. `Nombre` y
`Apellido` se sustituyen por `NombreComercial` (dato migrado).

**Decisiones:**
- **`Email` NO pasa a único** (desvío del spec, Ruling M): dos clientes pueden compartir correo legítimamente
  (un contacto contable) y crear el índice único puede fallar con datos existentes duplicados. Se conserva el
  índice NO único `IX_SociosNegocio_Email`. `Email` pasa a ser OPCIONAL.
- `NumeroDocumentoFiscal`: índice único PARCIAL `WHERE "IsDeleted" = false AND "NumeroDocumentoFiscal" IS NOT NULL`.
- `Codigo`: índice único PARCIAL `WHERE "IsDeleted" = false`. Lo asigna el sistema, nunca el cliente HTTP, y
  no se puede cambiar en `Update`.

**Migración con transformación de datos** (genera con `dotnet ef`, edita a mano lo necesario y documéntalo):
1. Añadir columnas nullable o con default; 2. `UPDATE`: `NombreComercial = TRIM("Nombre" || ' ' || "Apellido")`,
   `Tipo = 1`, `TipoDocumentoFiscal = 9`, `Codigo = LPAD(ROW_NUMBER() OVER (ORDER BY "CreatedAtUtc","Id")::text, 6, '0')`
   (sobre TODAS las filas, incluidas las borradas lógicamente); 3. endurecer a NOT NULL; 4. borrar `Nombre` y
   `Apellido`; 5. crear los índices. `Down()` simétrico (recompone `Nombre`/`Apellido` de forma aproximada y lo
   documenta).
6. **Sembrar la serie `SOCIOS` con `migrationBuilder.Sql` (NO con `HasData`, porque su valor depende de los
   datos)**: una `Serie` (`Codigo='SOCIOS'`, `PermiteHuecos=false`) y una `LineaSerie` (`NumeroInicial='000001'`,
   `NumeroFinal='999999'`, `UltimoNumeroUsado = LPAD(<total de filas migradas>::text, 6, '0')`, `FechaInicial='2000-01-01'`,
   `Incremento=1`), con Ids fijos, `CreatedAtUtc = now()`, `CreatedBy='system'`, `IsDeleted=false`. Así los
   códigos nuevos continúan tras los migrados sin colisión. Como no usa `HasData`, la migración de prueba
   posterior debe salir VACÍA.

**Creación del código (primer consumidor real de la numeración de la Task 2.2):** el handler de alta abre una
transacción con `IUnitOfWork.BeginTransactionAsync`, llama a `IGeneradorNumeroDocumento.SiguienteAsync("SOCIOS", fecha)`,
añade la entidad y confirma con `CommitAsync`; si algo falla, el rollback devuelve el número (sin huecos). El
generador exige transacción activa. Cubre con un test de integración: **10 altas concurrentes → 10 códigos
distintos y consecutivos**, y un alta fallida (dato inválido tras reservar número) NO consume número.

**Validación** (validador estático compartido, como `TerminosPago`): `NombreComercial` obligatorio (≤200);
si `TipoDocumentoFiscal` ≠ SinDocumento, `NumeroDocumentoFiscal` obligatorio (≤20; SIN validar formato RNC/NCF,
el proyecto no está localizado); `LimiteCredito` ≥ 0; `TerminoPagoId`, si viene, debe existir y no estar borrado;
`Email`, si viene, con formato válido. Errores con el nombre del campo del DTO.

**Api:** las 5 acciones con `Result<T>` y `ToActionResult()` (sustituyendo los `if`/`BadRequest()` manuales
heredados). **Blazor, adaptación MÍNIMA para que cada flujo existente siga funcionando** (la UI de los campos
nuevos es la 2.7): el formulario muestra un único campo "Nombre comercial" en lugar de Nombre+Apellido; los
listados, el detalle, el dashboard y las exportaciones Excel/PDF muestran "Nombre comercial" donde mostraban
Nombre y Apellido.

**Verificación:** build + suite (`ApplicationDbContextModelTests` verde); migración aplicada con 2–3 clientes
sembrados (sobreviven, `NombreComercial` correcto, `Codigo` sin duplicados, serie con el contador correcto);
prueba de migración vacía; y **EN EJECUCIÓN**, los mismos flujos de la 2.5 (listado, alta multipart, detalle,
edición, borrado, dashboard, Excel, PDF, Home) con estados HTTP reales.

## Task 2.7 — `SocioDeNegocio`: interfaz de los campos nuevos

**Reestructurada (Ruling K).** Sobre el backend de la 2.6.

- Formulario de alta/edición (`ClienteEditorForm`/`ClienteFields.razor`, que conservan su nombre de UI): añade
  `Tipo` (select), `RazonSocial`, `TipoDocumentoFiscal` (select), `NumeroDocumentoFiscal`, `Ciudad`,
  `TerminoPagoId` (select poblado desde la API de `TerminosPago`), `LimiteCredito`, `Bloqueado` (select).
- **Patrón obligatorio para los `<select>` poblados desde la API** (lección de `CategoriasProducto`): si la
  carga de opciones falla o viene vacía, NO permitas guardar a ciegas ni pierdas el valor actual: incluye
  siempre la opción del valor vigente y bloquea el guardado con un aviso visible cuando las opciones no
  cargaron. Verifícalo en ejecución con un proxy que devuelva 500 y vacío.
- Listado: filtros por `Codigo`, `Tipo` y `NumeroDocumentoFiscal`; columnas nuevas. Detalle: todos los campos.
  El `Codigo` es de solo lectura (lo asigna el sistema).
- Exportación Excel, reporte PDF y dashboards: incorporan `Codigo`, `Tipo` y documento fiscal donde tenga
  sentido, sin romper lo existente.
- Restricciones: Static SSR estricto (cero `@rendermode`/`@onclick`/`@bind` interactivo); el alta de cliente
  conserva su envío directo `multipart`.
- **Verificación EN EJECUCIÓN obligatoria**, con estados HTTP reales: alta con todos los campos y con los
  mínimos, edición, documento fiscal duplicado (mensaje de conflicto, no 500), término de pago inexistente,
  proxy 500/vacío en la carga de términos de pago, exportaciones y PDF.

## Task 2.8 — (absorbida por 2.5–2.7)

La antigua 2.8 (Blazor) se repartió: el renombre de tipos en Blazor va en la 2.5, la adaptación mínima en la
2.6 y los campos nuevos en la 2.7. No hay tarea 2.8.

## Task 2.9 — `Producto`: campos nuevos, `UnidadMedidaBaseId`, `CategoriaId`

Depende de las Tasks 2.3 y 2.4 (tablas destino de las nuevas FKs).

**Files:**
- Modify: `src/OpenSource1.Core/Entities/Producto.cs`
- Modify: `ApplicationDbContext.cs`
- Create: migración `ExtendProducto`
- Modify: toda la feature `Features/Productos/` (Application), el repositorio Dapper,
  el controller, y Blazor (`ProductoEditorForm`, `ProductoFields.razor`, páginas)

**Interfaces:**
- Produces: `Producto` con los campos de la sección 2.3 del spec.

- [ ] **Step 1: Entidad**

```csharp
public enum MetodoCosteo { Promedio = 1 }
public enum BloqueoProducto { Ninguno = 0, Venta = 1, Todo = 2 }

public sealed class Producto : BaseEntity
{
    public required string Codigo { get; set; }
    public required string Nombre { get; set; }
    public decimal PrecioVenta { get; set; }
    // Stock ELIMINADO — se deriva en la Fase 3.
    public Guid UnidadMedidaBaseId { get; set; }
    public MetodoCosteo MetodoCosteo { get; set; } = MetodoCosteo.Promedio;
    public decimal CostoUnitario { get; set; } // proyección, no autoritativa
    public decimal CostoEstandar { get; set; }
    public bool CostoAjustado { get; set; } = true; // sin movimientos todavía, no hay nada que ajustar
    public Guid CategoriaId { get; set; }
    public BloqueoProducto Bloqueado { get; set; } = BloqueoProducto.Ninguno;
    public string? ImagePath { get; set; }
}
```

Se eliminan también los VOs `Categoria`/`UnidadMedida` de `Producto` (ya reemplazados por
FKs a las Tasks 2.3/2.4). `CategoriaProducto`/`UnidadMedida` como VOs ya se eliminaron en
esas tareas.

- [ ] **Step 2: Mapeo EF**

`Codigo` único parcial (ya existe, mantener). `PrecioVenta` con `HasPrecision(18, 4)`
(cambia de `(18,2)`). `UnidadMedidaBaseId`/`CategoriaId` FK `Restrict`. `CostoUnitario`/
`CostoEstandar` con `HasPrecision(18, 4)`.

- [ ] **Step 3: Migración con transformación de datos**

Igual que la Task 2.5: `Stock` se pierde (documentar en el `Down()` que no es
recuperable — es una decisión de diseño irreversible, D1 del spec). `UnidadMedidaBaseId`
se rellena buscando la fila de `UnidadesMedida` cuyo `Codigo` coincida con el
`UnidadMedidaCodigo` que tenía el VO aplanado (columna `UnidadMedidaCodigo` de la tabla
`Productos` antes de esta migración). `CategoriaId` igual contra `CategoriasProducto`, o
la categoría `GENERAL` sembrada en la Task 2.4 si no hay coincidencia exacta.

```bash
dotnet ef migrations add ExtendProducto --project src/OpenSource1.Infrastructure --startup-project src/OpenSource1.Api --output-dir Data/Migrations/Application
```

Revisar y editar el `Up()` generado con el mismo cuidado que la Task 2.5: primero añadir
columnas nuevas nullable, poblarlas con `UPDATE ... FROM`, luego endurecer a NOT NULL y
borrar las columnas viejas (`Stock`, `Precio`, `CategoriaCodigo`, `CategoriaNombre`,
`UnidadMedidaCodigo`, `UnidadMedidaNombre`).

**Verificación no negociable:** sembrar productos de prueba antes de migrar, confirmar
que `UnidadMedidaBaseId`/`CategoriaId` resuelven correctamente tras la migración.
`ApplicationDbContextModelTests` en verde. Migración de prueba después, debe salir vacía.

- [ ] **Step 4: Application, Infrastructure, Api, Blazor**

Actualizar `ProductoSearchCriteria`, `ProductoResponse`, los 5 handlers,
`DapperProductoReadRepository`, `ProductosController`, `ProductoEditorForm.cs`,
`ProductoFields.razor`, las 4 páginas de Productos y `IProductoApiClient` — mismo alcance
mecánico que las Tasks 2.6-2.8 pero sobre `Producto`, sin necesidad de un plan tan
detallado porque el patrón ya quedó establecido en las tareas anteriores de esta misma
fase. Alta/edición de producto debe permitir asociar unidades de medida adicionales
(`UnidadMedidaProducto`) más allá de la base — como mínimo, un selector de la unidad base
en el formulario; la gestión completa de conversiones múltiples puede quedar como mejora
de una fase posterior si el alcance crece demasiado (usa criterio y documenta la decisión
en el informe si recortas aquí).

- [ ] **Step 5: Verificar y commit**

```bash
dotnet build test.slnx
dotnet test test.slnx
```

---

## Task 2.10 — Verificación de cierre de la Fase 2

- [ ] Migración completa aplicada de cero sobre una base nueva, sin errores, en orden
      (`dotnet ef database update` desde limpio).
- [ ] `ApplicationDbContextModelTests` en verde.
- [ ] Suite completa en verde con Docker real, sin regresiones respecto a la Fase 0-1.
- [ ] `grep -rn "Cliente\b" src/ tests/` no debe encontrar la entidad vieja (sí puede
      encontrar `ClienteId`-style naming en comentarios históricos o en el propio texto
      del spec/plan, que no cuenta).
- [ ] Ningún `TODO`/placeholder dejado en el código de esta fase.
- [ ] Commit final con el resumen de la fase si hace falta uno de cierre.
