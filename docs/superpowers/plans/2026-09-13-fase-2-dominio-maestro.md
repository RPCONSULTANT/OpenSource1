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

## Task 2.5 — `SocioDeNegocio`: entidad, mapeo, migración de datos

La tarea de mayor riesgo de la fase: renombra la tabla que sostiene todo el CRUD de
Clientes ya en producción académica (Entregable 2), preservando los datos.

**Files:**
- Create: `src/OpenSource1.Core/Entities/SocioDeNegocio.cs`
- Delete: `src/OpenSource1.Core/Entities/Cliente.cs` (tras confirmar que las Tasks 2.6-2.8
  ya no lo referencian — hacer esta tarea de último dentro del grupo Socio, no primero)
- Modify: `ApplicationDbContext.cs`
- Create: migración `RenameClienteToSocioNegocio` (con transformación de datos, no solo DDL)

**Interfaces:**
- Produces: `SocioDeNegocio` entity con todos los campos de la sección 2.1 del spec
  salvo los de grupos contables (`GrupoNegocioId`, `GrupoIvaNegocioId`,
  `GrupoClienteContableId` — se añaden en la Fase 5, dejar los `uuid?` como columnas
  presentes pero sin FK todavía, o mejor: **no añadirlas en esta tarea**, se agregan
  cuando la Fase 5 tenga la tabla destino; documentarlo así en el código).

- [ ] **Step 1: Entidad**

```csharp
namespace OpenSource1.Core.Entities;

public enum TipoSocioNegocio { Cliente = 1, Proveedor = 2, Ambos = 3 }
public enum TipoDocumentoFiscal { Rnc = 1, Cedula = 2, Pasaporte = 3, SinDocumento = 9 }
public enum BloqueoSocioNegocio { Ninguno = 0, Facturacion = 1, Todo = 2 }

namespace OpenSource1.Core.Entities;

public sealed class SocioDeNegocio : BaseEntity
{
    public required string Codigo { get; set; }
    public TipoSocioNegocio Tipo { get; set; }
    public required string NombreComercial { get; set; }
    public string? RazonSocial { get; set; }
    public TipoDocumentoFiscal TipoDocumentoFiscal { get; set; } = TipoDocumentoFiscal.SinDocumento;
    public string? NumeroDocumentoFiscal { get; set; }
    public string? Email { get; set; }
    public string? Telefono { get; set; }
    public DireccionCliente? Direccion { get; set; }
    public string? Ciudad { get; set; }
    public Pais? Pais { get; set; }
    public Sector? Sector { get; set; }
    public Guid? TerminoPagoId { get; set; }
    public decimal LimiteCredito { get; set; }
    public BloqueoSocioNegocio Bloqueado { get; set; } = BloqueoSocioNegocio.Ninguno;
    public string? ImagePath { get; set; }
}
```

Nota: `DireccionCliente`/`Pais`/`Sector` son los VOs YA migrados al patrón factory
seguro (Lote D2/fix-wave final de la Fase 0-1) — reutilízalos tal cual, no los reescribas.
Considera si el nombre `DireccionCliente` debería renombrarse a `DireccionFiscal` (el
spec lo llama así) — si lo haces, es un rename de tipo que toca los 2 call sites
existentes (`CreateClienteCommandHandler`/`UpdateClienteCommandHandler`, que se
reemplazan de todas formas en la Task 2.6) más el propio archivo del VO. Decisión: **sí,
renómbralo** — es el momento correcto, antes de que se generalice el nombre viejo.

- [ ] **Step 2: Mapeo EF**

`Codigo` único parcial. `NumeroDocumentoFiscal` único parcial **doble**: por `IsDeleted`
Y por `IS NOT NULL` a la vez —
`HasFilter("\"IsDeleted\" = false AND \"NumeroDocumentoFiscal\" IS NOT NULL")` (el spec
pide "único parcial WHERE IS NOT NULL"; combinarlo con el filtro de soft delete es la
lección de la Fase 0-1, no un añadido opcional). Mismo tratamiento para `Email`.
`TerminoPagoId` FK opcional a `TerminoPago`, `Restrict`. Los enums se guardan como
`smallint` (`HasConversion<short>()` o el mapeo por defecto de EF para enums, que ya es
`int` — usar `.HasConversion<short>()` explícito para que ocupe 2 bytes como pide el spec,
aunque esto es un detalle menor, prioriza correctitud sobre tamaño de columna si hay
conflicto).

- [ ] **Step 3: Migración con transformación de datos**

Generar la migración de esquema:

```bash
dotnet ef migrations add RenameClienteToSocioNegocio --project src/OpenSource1.Infrastructure --startup-project src/OpenSource1.Api --output-dir Data/Migrations/Application
```

**Revisar el `Up()` generado.** Si EF genera un `DropTable("Clientes")` +
`CreateTable("SociosNegocio")` (porque son tipos C# distintos, no un rename detectado),
hay que **editarlo a mano** para preservar los datos — igual que se hizo con `xmin` en la
Fase 0-1, documentando el motivo:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.RenameTable(name: "Clientes", newName: "SociosNegocio");

    // Columnas nuevas con NOT NULL requieren un valor por defecto para las filas
    // existentes; se rellenan en el mismo Up() con un UPDATE, luego se puede quitar el
    // default si el modelo no lo pide.
    migrationBuilder.AddColumn<string>(name: "Codigo", table: "SociosNegocio", type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "");
    migrationBuilder.AddColumn<short>(name: "Tipo", table: "SociosNegocio", type: "smallint", nullable: false, defaultValue: (short)1);
    migrationBuilder.AddColumn<string>(name: "NombreComercial", table: "SociosNegocio", type: "character varying(200)", maxLength: 200, nullable: false, defaultValue: "");
    // ... resto de columnas nuevas, todas nullable o con default ...

    migrationBuilder.Sql("""
        UPDATE "SociosNegocio"
        SET "NombreComercial" = TRIM(CONCAT("Nombre", ' ', "Apellido")),
            "Tipo" = 1,
            "TipoDocumentoFiscal" = 9,
            "Codigo" = 'CLI-' || LPAD(ROW_NUMBER() OVER (ORDER BY "CreatedAtUtc")::text, 6, '0')
        """);

    migrationBuilder.DropColumn(name: "Nombre", table: "SociosNegocio");
    migrationBuilder.DropColumn(name: "Apellido", table: "SociosNegocio");

    migrationBuilder.CreateIndex(name: "IX_SociosNegocio_Codigo", table: "SociosNegocio", column: "Codigo", unique: true, filter: "\"IsDeleted\" = false");
    // ... resto de índices ...
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    // Simétrico: recompone Nombre/Apellido desde NombreComercial (best-effort, con aviso
    // de pérdida de la separación original si el Down() se ejecuta alguna vez) y
    // renombra la tabla de vuelta.
}
```

El `ROW_NUMBER()` para `Codigo` es solo para no dejar duplicados en el `UNIQUE`; no tiene
que ser el formato final de numeración de socios (eso podría usar `IGeneradorNumeroDocumento`
de la Task 2.2 en el futuro, pero es una migración de datos histórica, no un flujo nuevo).

**Verificación no negociable:** aplicar la migración contra una copia de la base con
datos de prueba (sembrar 2-3 `Clientes` antes de migrar), confirmar que los datos
sobreviven con `NombreComercial` poblado correctamente y `Codigo` sin duplicados.
Confirmar `ApplicationDbContextModelTests` en verde. Generar una migración de prueba
después (`dotnet ef migrations add ProbeSocio -o /tmp/probe`) y confirmar que sale vacía.

- [ ] **Step 4: Verificar y commit**

**No borrar `Cliente.cs` todavía** — se hace al final de la Task 2.6, cuando ya no haya
ningún `using OpenSource1.Core.Entities.Cliente` en el árbol.

---

## Task 2.6 — `SocioDeNegocio`: capa Application

Reemplaza toda la feature `Features/Clientes/` por `Features/SociosNegocio/`, con los
campos nuevos.

**Files:**
- Create: `src/OpenSource1.Application/Features/SociosNegocio/` completo (mismo layout
  que `Features/Clientes/`: `Commands/`, `Queries/`, `Handlers/`, `Dtos/`,
  `SocioNegocioSearchCriteria.cs`, `ISocioNegocioReadRepository.cs`)
- Delete: `src/OpenSource1.Application/Features/Clientes/` completo

**Interfaces:**
- Produces: `CreateSocioNegocioCommand`, `UpdateSocioNegocioCommand`,
  `DeleteSocioNegocioCommand`, `GetSocioNegocioByIdQuery`, `ListSociosNegocioQuery`,
  `SocioNegocioResponse`, `ISocioNegocioReadRepository`. Consumidos por las Tasks 2.7 y
  2.8, y por las Fases 4-6 (facturación referencia `SocioDeNegocio`).

- [ ] **Step 1: Comandos y DTO**

Replica `CreateClienteCommand`/`ClienteResponse` campo por campo, añadiendo los nuevos:
`Codigo` (asignado por el sistema en `Create`, no recibido del cliente HTTP — se genera
con un contador simple `SELECT COUNT(*) + 1` formateado, NO uses `IGeneradorNumeroDocumento`
todavía salvo que quieras crear una `Serie` "SOCIOS" en la Task 2.2 para ello — decisión:
**créala**, es consistente y evita un segundo mecanismo de numeración), `Tipo`,
`RazonSocial`, `TipoDocumentoFiscal`, `NumeroDocumentoFiscal`, `Ciudad`, `TerminoPagoId`,
`LimiteCredito`, `Bloqueado`.

Los handlers de escritura usan `Result<SocioNegocioResponse>` en vez del tipo directo sin
envolver que usa `Cliente` hoy — es código que se reescribe entero, así que adopta la
convención correcta desde ahora (no repliques el patrón viejo de `ClienteResponse`
directo). Los errores de validación de VOs siguen naciendo como
`ErroresDeDominioException` desde los propios VOs (`Pais.Of`, etc.) — eso no cambia.

- [ ] **Step 2: `ISocioNegocioReadRepository` y `SocioNegocioSearchCriteria`**

Replica `IClienteReadRepository`, añadiendo filtros por `Codigo`, `Tipo`,
`NumeroDocumentoFiscal`.

- [ ] **Step 3: Handlers**

Replica los 5 handlers de Clientes (`Create`/`Update`/`Delete`/`GetById`/`List`),
adaptados a los nuevos campos y a `Result`.

- [ ] **Step 4: Verificar y commit**

---

## Task 2.7 — `SocioDeNegocio`: Infrastructure + Api

**Files:**
- Create: `src/OpenSource1.Infrastructure/Data/Queries/DapperSocioNegocioReadRepository.cs`
- Delete: `src/OpenSource1.Infrastructure/Data/Queries/DapperClienteReadRepository.cs`
- Modify: `src/OpenSource1.Infrastructure/Data/DependencyInjection.cs`
- Create: `src/OpenSource1.Api/Controllers/SociosNegocioController.cs`
- Delete: `src/OpenSource1.Api/Controllers/ClientesController.cs`
- Delete: `tests/OpenSource1.SmokeTests/Api/ClientesApiTests.cs` → recrear como
  `SociosNegocioApiTests.cs`
- Delete: `tests/OpenSource1.SmokeTests/Features/Clientes/` → recrear en
  `tests/OpenSource1.SmokeTests/Features/SociosNegocio/`

**Interfaces:**
- Consumes: todo lo de la Task 2.6.

- [ ] **Step 1: Repositorio Dapper**

Replica `DapperClienteReadRepository` sobre `"SociosNegocio"`, con `ColumnasPermitidas`
ampliada (`Codigo`, `NombreComercial`, `NumeroDocumentoFiscal`, etc.).

- [ ] **Step 2: Controller**

Replica `ClientesController`, usando `ResultExtensions.ToActionResult()` para todo (no
las validaciones manuales con `if`/`BadRequest()` sin cuerpo que tiene hoy — reemplázalas
por la validación que ya hacen los VOs vía `ErroresDeDominioException`, que el
`GlobalExceptionHandler` ya traduce a 400 con el campo).

- [ ] **Step 3: Borrar `Cliente.cs` y confirmar que compila**

```bash
grep -rn "OpenSource1.Core.Entities.Cliente\b" src/ tests/ | grep -v obj/
```
Debe salir vacío antes de borrar el archivo. Si algo queda (Blazor, típicamente), la
Task 2.8 lo resuelve — puede que este Step 3 se mueva al final de la Task 2.8 si el orden
real de dependencias lo exige; usa criterio.

- [ ] **Step 4: Tests de integración**

Recrear `ClientesApiTests.cs` como `SociosNegocioApiTests.cs`, mismo alcance (CRUD +
401/403/404), con Postgres real.

- [ ] **Step 5: Verificar y commit**

```bash
dotnet build test.slnx
dotnet test test.slnx
```

---

## Task 2.8 — `SocioDeNegocio`: Blazor

**Files:**
- Create: `src/OpenSource1.Blazor/Services/ISocioNegocioApiClient.cs` + implementación
  (reemplaza `IClienteApiClient`/`ClienteApiClient`)
- Delete: `src/OpenSource1.Blazor/Services/IClienteApiClient.cs`, `ClienteApiClient.cs`
- Create: `src/OpenSource1.Blazor/Components/SocioNegocioEditorForm.cs`,
  `SocioNegocioFields.razor` (reemplazan `ClienteEditorForm.cs`/`ClienteFields.razor`)
- Create: páginas `SociosNegocio.razor`, `SocioNegocioDetail.razor`, `SocioNegocioNew.razor`,
  `SociosNegocioDashboard.razor` (reemplazan las 4 de Clientes)
- Delete: las 4 páginas de Clientes + `ClienteEditorForm.cs` + `ClienteFields.razor`
- Modify: cualquier referencia a `ListAllAsync`/reportería/dashboards que usaba
  `IClienteApiClient` (recordar el hallazgo de la Fase 0-1: exports/reportería/dashboards
  dependen de `ListAllAsync` — no dejar ninguno apuntando al tipo borrado)
- Modify: `src/OpenSource1.Blazor/Components/Routes.razor` / menú de navegación si
  referencia "Clientes" por nombre de ruta

**Restricción dura:** Static SSR, sin `@onclick`/`@rendermode` nuevos — mismo patrón que
ya usan las páginas de Clientes/Productos.

- [ ] **Step 1: `ISocioNegocioApiClient`**

Replica `IClienteApiClient` (incluido `ListAllAsync` con el bucle de paginación completo
— **no** simplificarlo a una sola página, es la corrección de la Fase 0-1).

- [ ] **Step 2: Formulario y campos**

Replica `ClienteEditorForm`/`ClienteFields.razor`, añadiendo los campos nuevos con las
mismas anotaciones de validación (`[Required]`, `[MaxLength]`) que ya usa el patrón.

- [ ] **Step 3: Páginas**

Replica las 4 páginas de Clientes, renombradas, con las columnas/filtros nuevos donde
aplique.

- [ ] **Step 4: Barrido de referencias**

```bash
grep -rln "IClienteApiClient\|ClienteApiClient\|ClienteEditorForm\|ClienteFields\|/clientes\b" src/OpenSource1.Blazor/
```
Actualizar cada resultado. Prestar atención especial a `Program.cs` (exports),
`Home.razor`, `Bitacora.razor`, cualquier dashboard — son los 12 call sites que la Fase
0-1 ya enumeró exhaustivamente para `ListAllAsync`.

- [ ] **Step 5: Verificar y commit**

```bash
dotnet build test.slnx
dotnet test test.slnx
```

Verificación manual adicional (si el entorno lo permite): levantar la API y Blazor,
confirmar que la navegación a la nueva sección de socios de negocio funciona sin errores
de enrutamiento.

---

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
