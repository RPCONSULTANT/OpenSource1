# Fase 3 — Inventario: almacenes y libro de movimientos · Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** crear el catálogo de almacenes y el libro de inventario append-only (cantidad y valor separados,
aplicaciones entrada→salida), el servicio que registra entradas/salidas con costo promedio ponderado, la rutina
idempotente `AjustarCostoMovimientos`, y sustituir la columna legada `Producto.Stock` por la existencia derivada
del libro sin perder el stock actual.

**Architecture:** `Almacen` es un maestro más (mismo patrón CQRS-lite que `UnidadMedida`: EF para escritura vía
`IUnitOfWork`, Dapper vía `IDbSession` para lectura paginada, MediatR, `Result`, página Blazor Static SSR). El libro
son tres tablas con PK `bigint` identidad, sin soft delete, sin `xmin`, protegidas por triggers de PostgreSQL que
impiden `UPDATE`/`DELETE` (salvo `CantidadRestante`). Toda escritura en el libro pasa por un único servicio de
Infrastructure (`RegistroMovimientosInventario`) que corre dentro de la transacción del llamador y serializa por
producto con un advisory lock. La Fase 4 (diarios) y la 6 (facturas) son sus consumidores.

**Tech Stack:** .NET 10, EF Core 10 + Npgsql, Dapper, MediatR 13, xUnit, Docker/Postgres real.

**Spec:** [`docs/superpowers/specs/2026-09-12-axionerp-erp-modules-design.md`](../specs/2026-09-12-axionerp-erp-modules-design.md), sección "Fase 3", más la sección "Desviaciones acordadas durante la ejecución de la Fase 3" que añade la Task 3.1.

## Global Constraints

- TFM `net10.0`; nullable e implicit usings habilitados. Solución `test.slnx` (`dotnet build test.slnx`, `dotnet test test.slnx`).
- `PackageReference` directo por proyecto, sin CPM. No se añaden paquetes.
- Blazor Static SSR: sin `@rendermode`, `@onclick`, `@bind` interactivo ni JS nuevo. Formularios GET/POST con
  `[SupplyParameterFromForm]`/`[SupplyParameterFromQuery]`, antiforgery, `<EditForm>` de un solo paso con
  `FormName` estático, `ConfirmDialog` + `?deleteId=` solo para borrar. Un `<select>` cargado de la API incluye
  siempre el valor vigente y bloquea Guardar si las opciones no cargaron.
- Nombres en español. Precisión: cantidades y factores `numeric(18,6)`; importes y costos `numeric(18,4)`.
- PK de libros `bigint` identidad; maestros `uuid`. Soft delete y `xmin` solo en maestros.
- Errores: `Result`/`Error(Codigo, Mensaje, Campo)`. `ToActionResult()`: código terminado en `.no_encontrado`→404,
  `.conflicto`→409, resto→400. Una referencia inválida en el cuerpo NO usa un código `.no_encontrado`.
- Patrón VO: constructor privado + `Of(...)` (no hay VOs nuevos previstos en esta fase).
- Migraciones con `dotnet ef migrations add`; quitar a mano las operaciones de columna `xmin` que el scaffold de
  Npgsql mete por error; SQL a mano (triggers, transformación de datos) dentro de la migración generada. Tras cada
  migración: `ApplicationDbContextModelTests` en verde y una migración sonda
  (`dotnet ef migrations add ProbeX -p src/OpenSource1.Infrastructure -s src/OpenSource1.Api --context ApplicationDbContext -o <scratchpad>/probe --no-build`) con `Up()` vacío.
- Tests contra Postgres real: `DOCKER_CONTEXT=default dotnet test test.slnx --filter <...>` (variable de entorno, no
  cambiar el contexto global). Suite completa al final de cada task.
- Commits locales en `feat/erp-fase-3-inventario` (ramificada de `feat/erp-fase-2-dominio-maestro`). Prohibido
  merge/push. Mensajes en español terminando con las líneas de atribución que indique el controlador.
- No tocar `appsettings*.json`; secretos solo por variables de entorno.
- Cada módulo de mantenimiento: agregar, modificar, eliminar, consultar, limpiar campos.
- Runtime obligatorio para páginas Blazor: levantar API + Blazor, recorrer los flujos con curl (cookie + antiforgery)
  y dejar la evidencia en el informe.

## Review Focus

1. **Stock legado al migrar:** un producto con `Stock` 120, otro con 0, otro con −5 y uno borrado lógicamente →
   tras migrar la existencia derivada en el almacén `PRINCIPAL` es 120/0/−5 (el borrado conserva su movimiento) y el
   valor es `Stock × CostoUnitario`. Test en Task 3.6.
2. **Salida sin existencia suficiente o con almacén/producto bloqueado o borrado:** `Result` fallido y **ninguna**
   fila escrita (rollback verificado contando filas). Test en Task 3.4.
3. **Posteo con fecha retroactiva** (una entrada anterior a salidas ya registradas): la salida conserva su costo
   hasta que corre el ajuste; el ajuste inserta el delta y una segunda ejecución no inserta nada. Test en Task 3.5.
4. **Dos salidas concurrentes del mismo producto** que juntas superan la existencia: una gana, la otra falla; la
   existencia nunca queda negativa por la carrera. Test en Task 3.4.
5. **Borrar un almacén con movimientos / quitar el predeterminado:** 409; el único predeterminado se reasigna de
   forma atómica y nunca hay dos. Tests en Task 3.2.

---

## Task 3.1 — Correcciones de diseño al spec (solo docs)

El controlador ya adjudicó estas desviaciones; esta task solo las escribe en el spec para que las siguientes
tasks argumenten desde un único texto.

**Files:**
- Modify: `docs/superpowers/specs/2026-09-12-axionerp-erp-modules-design.md` (añadir tras la verificación de la Fase 3 un bloque "Desviaciones acordadas durante la ejecución de la Fase 3")

- [ ] **Step 1: Escribir el bloque** con exactamente estas viñetas:

```markdown
**Desviaciones acordadas durante la ejecución de la Fase 3:**

- **Fórmula del promedio.** La fórmula de 3.5 (solo filas con `CantidadValorada > 0`) sobrevalora el inventario
  en cuanto hay salidas (compra 10 a 10, venta 10, compra 10 a 20 → promedio 15 y valor residual 50 con existencia
  0) y cuenta dos veces las transferencias. Se usa el promedio móvil por día: para una salida con fecha `d`,
  `Costo = V / Q` con `V = SUM(ImporteCosto)` y `Q = SUM(CantidadValorada)` sobre **todos** los movimientos de valor
  del producto con `FechaRegistro < d`, más los de **entradas no transferencia** con `FechaRegistro = d`. Las
  salidas del mismo día comparten el costo. Si `Q <= 0` se usa `Producto.CostoUnitario` y el producto queda con
  `CostoAjustado = false`.
- **Movimientos de valor de ajuste y redondeo** llevan `CantidadValorada = 0` (no alteran la cantidad valorada).
- **Append-only real.** Triggers de PostgreSQL rechazan `DELETE` en las tres tablas del libro y `UPDATE` en
  `MovimientosValor` y `AplicacionesMovimientoProducto`; en `MovimientosProducto` solo se permite cambiar
  `CantidadRestante` (lo consumen las aplicaciones, como `Remaining Quantity` en BC).
- **Stock legado.** `Producto.Stock` se elimina en esta fase (Ruling L de la Fase 2). La migración crea el almacén
  `PRINCIPAL` (predeterminado) y convierte cada `Stock <> 0` en un movimiento de apertura (`TipoOrigen = Migracion`)
  con su movimiento de valor `Stock × CostoUnitario`. Un stock negativo se migra como salida sin aplicaciones.
- **Serialización por producto** con `pg_advisory_xact_lock` en lugar de `SELECT ... FOR UPDATE` sobre `Productos`
  (no bloquea la edición del maestro).
- **Existencia por almacén** de un producto: `GET api/productos/{id}/existencias`. Las vistas completas de
  movimientos quedan para la Fase 7.
- Las columnas de grupos contables (`GrupoInventarioId`, `GrupoNegocioId`, `GrupoProductoId`) se crean `uuid`
  nulas SIN FK; la FK llega con sus tablas en la Fase 5.
```

- [ ] **Step 2: Commit** `docs: desviaciones de diseño de la fase 3 (promedio movil diario, append-only con triggers, migracion del stock legado)`.

---

## Task 3.2 — Maestro `Almacenes` (dominio, API, tests)

**Files:**
- Create: `src/OpenSource1.Core/Entities/Almacen.cs`
- Modify: `src/OpenSource1.Infrastructure/Data/ApplicationDbContext.cs` (DbSet + mapeo + `HasData` de `PRINCIPAL`)
- Create: migración `AddAlmacenes` (generada)
- Create: `src/OpenSource1.Application/Features/Almacenes/**` (Commands, Queries, Handlers, Dtos, `AlmacenValidator`, `AlmacenSearchCriteria`, `IAlmacenReadRepository`) — copiar la forma exacta de `Features/UnidadesMedida`
- Create: `src/OpenSource1.Infrastructure/Data/Queries/DapperAlmacenReadRepository.cs` (+ registro DI donde se registran los otros)
- Create: `src/OpenSource1.Api/Controllers/AlmacenesController.cs` (`api/almacenes`, mismas políticas que `UnidadesMedidaController`)
- Test: `tests/OpenSource1.SmokeTests/Features/Almacenes/Handlers/*`, `tests/OpenSource1.SmokeTests/Api/AlmacenesApiTests.cs`

**Interfaces:**
- Produces: `Almacen` (`Guid Id`), `AlmacenIds.Principal = Guid.Parse("b1000000-0000-0000-0000-000000000001")` (constante pública en Core, la usa la migración de la Task 3.6), `DbSet<Almacen> Almacenes`.

```csharp
namespace OpenSource1.Core.Entities;

public sealed class Almacen : BaseEntity
{
    public required string Codigo { get; set; }          // varchar(10), MAYÚSCULAS, único parcial
    public required string Nombre { get; set; }          // varchar(100)
    public string? DireccionLinea1 { get; set; }         // varchar(300)
    public string? DireccionLinea2 { get; set; }         // varchar(300)
    public string? Ciudad { get; set; }                  // varchar(100)
    public string? PaisCodigo { get; set; }              // varchar(2), validado con el VO Pais existente
    public bool Bloqueado { get; set; }
    public bool EsPredeterminado { get; set; }
}

public static class AlmacenIds
{
    public static readonly Guid Principal = Guid.Parse("b1000000-0000-0000-0000-000000000001");
}
```

Reglas:
- `Codigo` se normaliza `Trim().ToUpperInvariant()`; 1-10 caracteres `[A-Z0-9_-]`; duplicado → 409 (índice único
  parcial `"IsDeleted" = false`, 23505 ya se traduce).
- Índice único parcial `IX_Almacenes_EsPredeterminado` sobre `EsPredeterminado` con filtro
  `"EsPredeterminado" = true AND "IsDeleted" = false` (a lo sumo uno).
- Marcar uno como predeterminado (alta o PUT con `EsPredeterminado = true`) desmarca el anterior **en la misma
  transacción** (`BeginTransactionAsync` → quitar la marca del actual → `SaveChangesAsync` → marcar el nuevo →
  `CommitAsync`; dos `SaveChanges` porque el índice no es diferible).
- PUT con `EsPredeterminado = false` sobre el predeterminado actual → 400 `almacen.predeterminado_requerido`
  (siempre hay uno; se cambia marcando otro).
- DELETE del predeterminado → 409 `almacen.conflicto` ("marque otro como predeterminado"). DELETE de un almacén con
  movimientos → 409; la guarda de movimientos se añade en la Task 3.3 cuando exista la tabla (dejar el handler con
  un método privado `TieneMovimientosAsync` que hoy devuelve `false` NO está permitido: la guarda y su test llegan
  juntos en 3.3).
- PUT: campos nuevos anulables con semántica "null = conservar" (igual que SocioNegocio tras el Ruling V).
- Seed `HasData`: `PRINCIPAL` / "Almacén principal" / `EsPredeterminado = true`, `CreatedAtUtc` fijo
  `2026-09-25T00:00:00Z`, `CreatedBy = "system"`.
- Listado: filtros `codigo`, `nombre`, `bloqueado`; orden permitido `Codigo`, `Nombre`, `CreatedAtUtc`; orden
  estable con `, "Id" ASC`.

- [ ] **Step 1: Tests que fallan** (handlers con fakes como en `Features/UnidadesMedida`, API como en `UnidadesMedidaApiTests`):
  - alta normaliza `" pri2 "` → `PRI2`; código inválido (`"A B"`, 11 chars, vacío) → 400 con `Campo = "Codigo"`;
  - alta de otro con `EsPredeterminado = true` → el anterior queda `false` (verificar con GET de ambos);
  - PUT `EsPredeterminado = false` sobre el predeterminado → 400 `almacen.predeterminado_requerido`;
  - DELETE del predeterminado → 409; DELETE normal → 204 y luego GET 404 y el listado no lo devuelve;
  - código duplicado → 409; recrear tras borrar → 201;
  - la semilla `PRINCIPAL` existe tras migrar y es la única predeterminada;
  - roles: Supervisor no puede POST (403), consulta sí (200) — seguir `AuthPermissionsApiTests`;
  - concurrencia del predeterminado: dos POST paralelos con `EsPredeterminado = true` → al final exactamente un
    predeterminado (`SELECT COUNT(*) ... WHERE "EsPredeterminado" AND NOT "IsDeleted"` = 1); uno de los dos puede
    responder 409, nunca 500.
- [ ] **Step 2:** correr los tests nuevos y ver que fallan por no existir el código.
- [ ] **Step 3:** implementar entidad, mapeo, migración (`dotnet ef migrations add AddAlmacenes ...`, revisar y quitar operaciones `xmin`), features, repositorio Dapper, controller.
- [ ] **Step 4:** tests nuevos + `ApplicationDbContextModelTests` en verde; sonda de migración vacía.
- [ ] **Step 5:** suite completa; commit `feat: maestro de almacenes con predeterminado unico y API`.

---

## Task 3.3 — Esquema del libro de inventario (append-only)

**Files:**
- Create: `src/OpenSource1.Core/Entities/Inventario/MovimientoProducto.cs`, `MovimientoValor.cs`, `AplicacionMovimientoProducto.cs`
- Create: `src/OpenSource1.Core/Enums/TipoMovimientoInventario.cs`, `TipoDocumentoInventario.cs`, `TipoOrigenMovimiento.cs`, `TipoValor.cs`
- Modify: `ApplicationDbContext.cs` (3 DbSet + mapeo)
- Create: migración `AddLibroInventario` (generada + SQL de triggers)
- Modify: `src/OpenSource1.Application/Features/Almacenes/Handlers/DeleteAlmacenCommandHandler.cs` (guarda de movimientos)
- Test: `tests/OpenSource1.SmokeTests/Infrastructure/LibroInventarioAppendOnlyTests.cs`, ampliar `AlmacenesApiTests`

**Interfaces:**
- Produces (las usan 3.4-3.6 y las Fases 4/6):

```csharp
namespace OpenSource1.Core.Enums;
public enum TipoMovimientoInventario : short { Compra = 1, Venta = 2, AjustePositivo = 3, AjusteNegativo = 4, Transferencia = 5 }
public enum TipoDocumentoInventario : short { Ninguno = 0, RegistroDiario = 1, FacturaVenta = 2 }
public enum TipoOrigenMovimiento : short { Diario = 1, FacturaVenta = 2, AjusteCosto = 3, Migracion = 99 }
public enum TipoValor : short { CostoDirecto = 1, Redondeo = 3 }
```

```csharp
namespace OpenSource1.Core.Entities.Inventario;

// No heredan de BaseEntity: sin soft delete, sin UpdatedAt, sin xmin. Implementan IAggregateRoot para poder
// usarse con IUnitOfWork.Repository<T>() si hiciera falta (la escritura normal es por el servicio de la Task 3.4).
public sealed class MovimientoProducto
{
    public long Id { get; set; }
    public Guid ProductoId { get; set; }
    public Guid AlmacenId { get; set; }
    public TipoMovimientoInventario TipoMovimiento { get; set; }
    public TipoDocumentoInventario TipoDocumento { get; set; }
    public string? NumeroDocumento { get; set; }            // varchar(20)
    public int NumeroLineaDocumento { get; set; }
    public DateOnly FechaRegistro { get; set; }
    public DateOnly FechaDocumento { get; set; }
    public decimal Cantidad { get; set; }                   // (18,6) con signo, en unidad BASE
    public decimal? CantidadRestante { get; set; }          // (18,6) solo entradas (> 0 al crearse)
    public decimal CantidadFacturada { get; set; }          // (18,6)
    public Guid UnidadMedidaId { get; set; }                // unidad del documento
    public decimal CantidadPorUnidadMedida { get; set; }    // (18,6) congelado (D8); 1 para la base
    public Guid? SocioNegocioId { get; set; }
    public TipoOrigenMovimiento TipoOrigen { get; set; }
    public required string ClaveOrigen { get; set; }        // varchar(50)
    public DateTimeOffset CreatedAtUtc { get; set; }
    public required string CreatedBy { get; set; }          // varchar(100)
    public Guid? UsuarioId { get; set; }                    // referencia lógica sin FK (otra BD)
}

public sealed class MovimientoValor
{
    public long Id { get; set; }
    public long? MovimientoProductoId { get; set; }
    public Guid ProductoId { get; set; }
    public Guid AlmacenId { get; set; }
    public TipoValor TipoValor { get; set; }
    public TipoMovimientoInventario TipoMovimiento { get; set; }
    public DateOnly FechaRegistro { get; set; }
    public decimal CantidadValorada { get; set; }           // (18,6); 0 en ajustes/redondeo
    public decimal CantidadFacturada { get; set; }
    public decimal ImporteCosto { get; set; }               // (18,4) con signo
    public decimal CostoPorUnidad { get; set; }             // (18,4)
    public decimal ImporteVenta { get; set; }               // (18,4)
    public decimal ImporteCostoPosteadoContabilidad { get; set; } // (18,4) default 0
    public bool Ajuste { get; set; }
    public TipoDocumentoInventario TipoDocumento { get; set; }
    public string? NumeroDocumento { get; set; }
    public int NumeroLineaDocumento { get; set; }
    public Guid? GrupoInventarioId { get; set; }            // sin FK hasta la Fase 5
    public Guid? GrupoNegocioId { get; set; }
    public Guid? GrupoProductoId { get; set; }
    public TipoOrigenMovimiento TipoOrigen { get; set; }
    public required string ClaveOrigen { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public required string CreatedBy { get; set; }
    public Guid? UsuarioId { get; set; }
}

public sealed class AplicacionMovimientoProducto
{
    public long Id { get; set; }
    public long MovimientoEntradaId { get; set; }
    public long MovimientoSalidaId { get; set; }
    public decimal Cantidad { get; set; }                   // (18,6) > 0
    public DateOnly FechaRegistro { get; set; }
}
```

Mapeo: tablas `MovimientosProducto`, `MovimientosValor`, `AplicacionesMovimientoProducto`; `Id` con
`UseIdentityAlwaysColumn()`; FKs `Restrict` a `Productos`, `Almacenes`, `UnidadesMedida`, `SociosNegocio`,
`MovimientosProducto` (valor y aplicaciones). Sin `HasQueryFilter` propio, pero las FK a maestros con filtro global
generan el aviso de EF de "required end with query filter": configurar la navegación como no requerida o sin
navegación (solo FK) para que no se oculten movimientos de productos borrados. CHECKs:
`CK_MovimientosProducto_Cantidad_NoCero` (`"Cantidad" <> 0`), `CK_MovimientosProducto_Restante`
(`"CantidadRestante" IS NULL OR ("CantidadRestante" >= 0 AND "CantidadRestante" <= "Cantidad")`),
`CK_Aplicaciones_Cantidad_Positiva`, `CK_MovimientosProducto_Factor_Positivo`.
Índices exactamente los de spec 3.2/3.3 (el parcial `WHERE "CantidadRestante" > 0` sobre
`(ProductoId, AlmacenId, FechaRegistro, Id)` — se añade `AlmacenId` e `Id` porque la aplicación FIFO es por
almacén y en orden; el parcial de valor `WHERE "ImporteCosto" <> "ImporteCostoPosteadoContabilidad"`), más
`(MovimientoEntradaId)` y `(MovimientoSalidaId)` en aplicaciones y `(AlmacenId)` en movimientos de producto (guarda
de borrado).

SQL de la migración (dentro de `Up`, tras crear las tablas; `Down` elimina triggers y función antes de las tablas):

```sql
CREATE OR REPLACE FUNCTION libro_inventario_append_only() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'El libro % es de solo inserción: DELETE no permitido', TG_TABLE_NAME USING ERRCODE = 'P0001';
    END IF;
    IF TG_TABLE_NAME = 'MovimientosProducto'
       AND (to_jsonb(NEW) - 'CantidadRestante') = (to_jsonb(OLD) - 'CantidadRestante') THEN
        RETURN NEW;
    END IF;
    RAISE EXCEPTION 'El libro % es de solo inserción: UPDATE no permitido', TG_TABLE_NAME USING ERRCODE = 'P0001';
END $$;

CREATE TRIGGER "TR_MovimientosProducto_AppendOnly" BEFORE UPDATE OR DELETE ON "MovimientosProducto"
    FOR EACH ROW EXECUTE FUNCTION libro_inventario_append_only();
CREATE TRIGGER "TR_MovimientosValor_AppendOnly" BEFORE UPDATE OR DELETE ON "MovimientosValor"
    FOR EACH ROW EXECUTE FUNCTION libro_inventario_append_only();
CREATE TRIGGER "TR_AplicacionesMovimientoProducto_AppendOnly" BEFORE UPDATE OR DELETE ON "AplicacionesMovimientoProducto"
    FOR EACH ROW EXECUTE FUNCTION libro_inventario_append_only();
-- TRUNCATE no dispara triggers de fila: se bloquea aparte.
CREATE TRIGGER "TR_MovimientosProducto_NoTruncate" BEFORE TRUNCATE ON "MovimientosProducto"
    FOR EACH STATEMENT EXECUTE FUNCTION libro_inventario_append_only();
CREATE TRIGGER "TR_MovimientosValor_NoTruncate" BEFORE TRUNCATE ON "MovimientosValor"
    FOR EACH STATEMENT EXECUTE FUNCTION libro_inventario_append_only();
CREATE TRIGGER "TR_AplicacionesMovimientoProducto_NoTruncate" BEFORE TRUNCATE ON "AplicacionesMovimientoProducto"
    FOR EACH STATEMENT EXECUTE FUNCTION libro_inventario_append_only();
```

(Para el TRUNCATE `TG_OP = 'TRUNCATE'` cae en el último `RAISE`; ajustar el mensaje si se quiere, sin cambiar el
comportamiento.) Si el fixture de tests limpia tablas con `TRUNCATE`/`DELETE` entre tests, comprobarlo ANTES: el
fixture debe dejar de limpiar esas tablas o recrear la BD; no se desactivan los triggers en tests.

Guarda de borrado de almacén: `DeleteAlmacenCommandHandler` devuelve 409 `almacen.conflicto` si existe algún
`MovimientoProducto` con ese `AlmacenId` (consulta EF `AnyAsync`, sin filtro de borrado porque el libro no lo tiene).

- [ ] **Step 1: Tests que fallan** en `LibroInventarioAppendOnlyTests` (Postgres real, insertando con SQL directo o EF):
  - insertar un movimiento de producto + valor + aplicación funciona;
  - `UPDATE "MovimientosProducto" SET "CantidadRestante" = 0` funciona;
  - `UPDATE "MovimientosProducto" SET "Cantidad" = 1` → `PostgresException` con `SqlState == "P0001"`;
  - `UPDATE "MovimientosValor" SET "ImporteCosto" = 0` → P0001; `DELETE` en cada tabla → P0001; `TRUNCATE` → P0001;
  - `Cantidad = 0` → violación de CHECK (23514); `CantidadRestante > Cantidad` → 23514;
  - `AlmacenesApiTests`: DELETE de un almacén con un movimiento insertado → 409; sin movimientos → 204.
- [ ] **Step 2:** verlos fallar.
- [ ] **Step 3:** entidades, enums, mapeo, migración con los triggers.
- [ ] **Step 4:** tests + `ApplicationDbContextModelTests` + sonda vacía.
- [ ] **Step 5:** suite completa; commit `feat: libro de inventario append-only (movimientos de producto, valor y aplicaciones) protegido por triggers`.

---

## Task 3.4 — Servicio de registro y consultas de existencia/costo

**Files:**
- Create: `src/OpenSource1.Application/Services/Inventario/IRegistroMovimientosInventario.cs`, `MovimientoInventarioSolicitud.cs`, `IConsultaInventario.cs`, `IUsuarioActual.cs`
- Create: `src/OpenSource1.Infrastructure/Services/Inventario/RegistroMovimientosInventario.cs`, `ConsultaInventario.cs`, `CostoPromedioCalculadora.cs`
- Create: `src/OpenSource1.Api/Infrastructure/UsuarioActualHttp.cs` (implementa `IUsuarioActual` con los claims) y un `UsuarioActualSistema` por defecto en Infrastructure (`"system"`, `null`) registrado con `TryAdd` para tests/batch
- Modify: los `DependencyInjection` correspondientes
- Test: `tests/OpenSource1.SmokeTests/Services/RegistroMovimientosInventarioTests.cs`, `ConsultaInventarioTests.cs`

**Interfaces:**
- Consumes: `IConversionUnidadMedidaService.ConvertirABaseAsync`, `IUnitOfWork.HayTransaccionActiva`, `IDbSession`.
- Produces:

```csharp
namespace OpenSource1.Application.Services.Inventario;

public sealed record MovimientoInventarioSolicitud(
    Guid ProductoId,
    Guid AlmacenId,
    TipoMovimientoInventario TipoMovimiento,
    decimal Cantidad,                 // SIEMPRE > 0, en la unidad indicada; el signo lo da EsEntrada
    bool EsEntrada,
    Guid UnidadMedidaId,
    decimal? CostoUnitario,           // obligatorio (>= 0) en entradas no transferencia, en unidad BASE; ignorado en salidas
    DateOnly FechaRegistro,
    DateOnly FechaDocumento,
    TipoDocumentoInventario TipoDocumento,
    string? NumeroDocumento,
    int NumeroLineaDocumento,
    TipoOrigenMovimiento TipoOrigen,
    string ClaveOrigen,
    Guid? SocioNegocioId = null,
    decimal ImporteVenta = 0m);

public sealed record MovimientoRegistrado(long MovimientoProductoId, long MovimientoValorId, decimal CantidadBase, decimal ImporteCosto);

public interface IRegistroMovimientosInventario
{
    /// Requiere transacción activa (error "inventario.sin_transaccion" si no la hay, como GeneradorNumeroDocumento).
    /// No hace commit: el llamador confirma o deshace todo el documento.
    Task<Result<MovimientoRegistrado>> RegistrarAsync(MovimientoInventarioSolicitud solicitud, CancellationToken ct = default);
}

public sealed record ExistenciaAlmacen(Guid AlmacenId, string AlmacenCodigo, string AlmacenNombre, decimal Existencia);

public interface IConsultaInventario
{
    Task<decimal> ExistenciaAsync(Guid productoId, Guid? almacenId, DateOnly? fecha, CancellationToken ct = default);
    Task<IReadOnlyList<ExistenciaAlmacen>> ExistenciasPorAlmacenAsync(Guid productoId, CancellationToken ct = default);
    /// Costo promedio vigente para una SALIDA con fecha 'fecha' (fórmula de las desviaciones de la Fase 3);
    /// null si Q <= 0.
    Task<decimal?> CostoPromedioAsync(Guid productoId, DateOnly fecha, CancellationToken ct = default);
}

public interface IUsuarioActual { string Nombre { get; } Guid? Id { get; } }
```

Algoritmo de `RegistrarAsync` (todo con la conexión/transacción compartida de `IDbSession`):

1. Sin transacción activa → `inventario.sin_transaccion`.
2. `SELECT pg_advisory_xact_lock(hashtextextended(@productoId::text, 0))` — serializa por producto hasta el commit.
3. Validar: cantidad > 0 (`inventario.cantidad_invalida`, campo `Cantidad`); producto existe (filtro global) y no
   `Bloqueado == Todo` (`inventario.producto_bloqueado`); almacén existe y no bloqueado (`inventario.almacen_bloqueado`);
   entrada no transferencia con `CostoUnitario` null o < 0 → `inventario.costo_requerido`. Ningún código termina en
   `.no_encontrado` (son referencias del cuerpo): `inventario.producto_invalido`, `inventario.almacen_invalido`.
4. `cantidadBase = ConvertirABaseAsync(...)` (propaga su error); factor congelado =
   `cantidadBase / cantidad` redondeado a 6, o 1 si la unidad es la base (usar el factor devuelto por la consulta
   si se expone; no recalcular desde el resultado redondeado si la API del servicio de conversión permite obtenerlo
   — si no lo permite, añadir `ObtenerFactorAsync` al servicio de conversión con el mismo criterio de identidad).
5. **Entrada:** `MovimientoProducto` con `Cantidad = +cantidadBase`, `CantidadRestante = +cantidadBase`;
   `MovimientoValor` con `CantidadValorada = +cantidadBase`, `ImporteCosto = Round(cantidadBase × CostoUnitario, 4)`,
   `CostoPorUnidad = CostoUnitario`, `TipoValor = CostoDirecto`.
6. **Salida:** existencia en el almacén a `FechaRegistro` (`SUM(Cantidad)` con `FechaRegistro <= fecha`) y
   restante abierto en el almacén (`SUM(CantidadRestante)` de entradas con `FechaRegistro <= fecha`) — si
   `min(ambos) < cantidadBase` → `inventario.existencia_insuficiente` (campo `Cantidad`, mensaje con la existencia
   disponible). Costo = `CostoPromedioAsync(producto, fecha)`; si null → `Producto.CostoUnitario` y marcar ajuste.
   `MovimientoProducto` con `Cantidad = -cantidadBase`, `CantidadRestante = null`; aplicaciones FIFO contra las
   entradas abiertas del almacén con `FechaRegistro <= fecha` ordenadas por `(FechaRegistro, Id)`, bloqueándolas
   `FOR UPDATE`, decrementando `CantidadRestante` (única columna actualizable) e insertando
   `AplicacionMovimientoProducto`; `MovimientoValor` con `CantidadValorada = -cantidadBase`,
   `ImporteCosto = -Round(cantidadBase × costo, 4)`, `CostoPorUnidad = Round(costo, 4)`, `ImporteVenta` de la solicitud.
7. `Producto.CostoAjustado = false` siempre que se registre una salida, una entrada con fecha anterior a la última
   salida del producto, o se haya usado el costo de reserva. (Actualización por SQL de una sola columna para no
   pisar el `xmin` de una edición concurrente del maestro más de lo inevitable.)
8. `CreatedBy`/`UsuarioId` de `IUsuarioActual`; `CreatedAtUtc = UtcNow`.
9. Devolver `MovimientoRegistrado`.

La escritura puede hacerse con EF (`context.MovimientosProducto.Add` + `SaveChangesAsync` dentro de la transacción)
o con Dapper `INSERT ... RETURNING "Id"`; elegir uno y usarlo en todo el servicio. Nunca `Update`/`Remove` de EF
sobre entidades del libro (el trigger lo rechazaría).

`ConsultaInventario` con Dapper; `CostoPromedioAsync`:

```sql
SELECT COALESCE(SUM(v."ImporteCosto"), 0) AS "V", COALESCE(SUM(v."CantidadValorada"), 0) AS "Q"
FROM "MovimientosValor" v
WHERE v."ProductoId" = @productoId
  AND (v."FechaRegistro" < @fecha
       OR (v."FechaRegistro" = @fecha AND v."CantidadValorada" > 0 AND v."TipoMovimiento" <> 5));
```

`costo = Q > 0 ? V / Q : null` (decimal sin redondear; se redondea al construir el importe).

- [ ] **Step 1: Tests que fallan** (Postgres real, cada test en su propia transacción/datos):
  - entrada 10 a 10 → existencia 10, valor 100, `CantidadRestante` 10;
  - entrada 10 a 10 + entrada 10 a 20 → salida 5 con costo 15, importe −75; aplicaciones FIFO: 5 contra la primera;
  - el ejemplo del spec corregido: entrada 10 a 10, salida 10 (costo 10), entrada 10 a 20, salida 10 → costo 20 y
    valor final 0;
  - salida en unidad alternativa (caja = 12 base) → cantidad −12, factor congelado 12; entrada en la base → factor 1;
  - salida mayor que la existencia → `inventario.existencia_insuficiente` y **cero filas nuevas** en las tres tablas
    (contar antes/después dentro de la transacción deshecha);
  - existencia por almacén: entradas en dos almacenes, salida en uno → `ExistenciasPorAlmacenAsync` correcto;
    salida en el almacén sin stock falla aunque el total alcance;
  - fecha: existencia a una fecha anterior a la entrada es 0; salida fechada antes de la entrada → insuficiente;
  - producto con `Bloqueado = Todo`, almacén bloqueado, producto borrado → errores con los códigos indicados;
  - sin transacción activa → `inventario.sin_transaccion`;
  - costo de reserva: salida sin entradas valoradas (stock creado solo por migración con costo 0 no aplica; usar un
    caso con `Q <= 0`) → usa `Producto.CostoUnitario` y deja `CostoAjustado = false`;
  - **concurrencia:** existencia 10; dos tareas en paralelo con conexiones y transacciones propias, cada una sale 7
    → exactamente una `Exito`, la otra `inventario.existencia_insuficiente`; existencia final 3.
- [ ] **Step 2:** verlos fallar.
- [ ] **Step 3:** implementar.
- [ ] **Step 4:** tests en verde; mutaciones de control: quitar el advisory lock (el test de concurrencia debe
  fallar al menos de forma reproducible en 20 repeticiones; si no, reforzar el test con una barrera), invertir el
  orden FIFO (falla el test de aplicaciones).
- [ ] **Step 5:** suite completa; commit `feat: servicio de registro de movimientos de inventario con costo promedio, aplicaciones FIFO y existencia derivada`.

---

## Task 3.5 — Rutina `AjustarCostoMovimientos`

**Files:**
- Create: `src/OpenSource1.Application/Features/Inventario/Commands/AjustarCostoMovimientosCommand.cs` (+ handler en `Features/Inventario/Handlers`)
- Create: `src/OpenSource1.Application/Services/Inventario/IAjusteCostoInventario.cs`
- Create: `src/OpenSource1.Infrastructure/Services/Inventario/AjusteCostoInventario.cs`
- Create: `src/OpenSource1.Api/Controllers/InventarioController.cs` con `POST api/inventario/ajustar-costo` (`CanModify`) → 200 `{ productosAjustados, movimientosValorCreados }`
- Test: `tests/OpenSource1.SmokeTests/Services/AjusteCostoInventarioTests.cs`, `tests/OpenSource1.SmokeTests/Api/InventarioApiTests.cs`

**Interfaces:**

```csharp
public sealed record ResultadoAjusteCosto(int ProductosAjustados, int MovimientosValorCreados);
public interface IAjusteCostoInventario
{
    /// Productos con CostoAjustado = false (o solo 'productoId' si se indica). Una transacción por producto.
    Task<Result<ResultadoAjusteCosto>> AjustarAsync(Guid? productoId, CancellationToken ct = default);
}
```

Algoritmo por producto (transacción propia + el mismo advisory lock que 3.4):

```text
V = 0; Q = 0; ultimoCosto = null
para cada día d en orden (fechas distintas de los movimientos de producto del producto):
    entradas_d  = movimientos con Cantidad > 0 y TipoMovimiento <> Transferencia, fecha d
    V += SUM(ImporteCosto de TODOS sus movimientos de valor); Q += SUM(Cantidad)
    costo = Q > 0 ? V / Q : (ultimoCosto ?? Producto.CostoUnitario)
    si Q > 0: ultimoCosto = costo
    para cada salida o transferencia de fecha d (orden Id):
        esperado = Round(Cantidad × costo, 4)            // Cantidad con signo
        actual   = SUM(ImporteCosto de sus movimientos de valor, incluidos ajustes previos)
        si esperado <> actual: insertar MovimientoValor(Ajuste = true, TipoValor = CostoDirecto,
                                 CantidadValorada = 0, ImporteCosto = esperado − actual, TipoOrigen = AjusteCosto,
                                 ClaveOrigen = "AJUSTE-" + Id del movimiento, resto copiado del original)
        V += esperado; Q += Cantidad
    si Q == 0 y V <> 0 y |V| < 0.01: insertar MovimientoValor(TipoValor = Redondeo, CantidadValorada = 0,
                                 ImporteCosto = −V) sobre la última salida del día; V = 0
al final: CostoAjustado = true; CostoUnitario = Round(ultimoCosto ?? CostoUnitario, 4)
```

Las entradas de transferencia (Fase 4) quedan valoradas igual que su salida pareada: como ambas usan el mismo
`costo` del día y signo opuesto, el promedio no cambia. Documentarlo en el XML doc.

- [ ] **Step 1: Tests que fallan:**
  - salida registrada, después una entrada **retroactiva** a menor fecha y distinto costo → el ajuste inserta
    exactamente un movimiento de valor con el delta calculado a mano; la segunda ejecución devuelve
    `MovimientosValorCreados = 0` y no cambia el recuento de filas;
  - tras ajustar, `CostoAjustado = true` y `CostoUnitario` = promedio final redondeado a 4;
  - valor total del inventario tras ajuste = `SUM(ImporteCosto)` = existencia × costo final (± 0.0001);
  - redondeo: entradas 3 a 10/3 (importe 10.0000 con costo 3.3333…) y salida total → valor final exactamente 0 con
    una fila `TipoValor = Redondeo`;
  - ningún `UPDATE` sobre `MovimientosValor` (el trigger lo haría fallar; el test pasa en verde con triggers activos);
  - API: `POST api/inventario/ajustar-costo` como Ejecutor → 200; Supervisor (sin CanModify) → 403.
- [ ] **Step 2:** verlos fallar. **Step 3:** implementar. **Step 4:** verdes; mutación: usar `<=` en lugar de
  `<` al acumular (o no sumar ajustes previos en `actual`) → la idempotencia debe fallar.
- [ ] **Step 5:** suite completa; commit `feat: rutina idempotente AjustarCostoMovimientos (promedio movil diario, ajustes y redondeo append-only)`.

---

## Task 3.6 — Eliminar `Producto.Stock`: migración al libro y existencia derivada en API, UI y reportes

**Files:**
- Modify: `src/OpenSource1.Core/Entities/Producto.cs` (quitar `Stock`)
- Create: migración `ReemplazarStockPorLibro` (generada; `Up`: insertar aperturas ANTES de `DropColumn`; `Down`: `AddColumn` y reconstruir `Stock` desde el libro solo para los movimientos `TipoOrigen = 99` agregando por producto, redondeado a entero; documentar que `Down` pierde movimientos posteriores)
- Modify: `Features/Productos/**` (comandos, `IDatosProducto`, validator, handlers, `ProductoResponse`, `ProductoSearchCriteria`), `DapperProductoReadRepository.cs`, `ProductosController.cs` (+ `GET api/productos/{id}/existencias`)
- Modify (Blazor): `IProductoApiClient.cs`, `ProductoApiClient.cs`, `ProductoEditorForm.cs`, `ProductoFields.razor`, `ProductoNew.razor`, `ProductoDetail.razor`, `Productos.razor`, `ProductosDashboard.razor`, `Home.razor`, `Reporteria.razor`, `ClienteReportForms.cs`, `QuestPdfReportDocumentService.cs`, `Program.cs` (exportaciones)
- Test: `tests/OpenSource1.SmokeTests/Infrastructure/ReemplazarStockPorLibroMigrationTests.cs` (seguir el patrón de `ExtendProductoMigrationTests`), ajustar tests existentes de productos/exportaciones

**Interfaces:**
- Consumes: `IConsultaInventario.ExistenciasPorAlmacenAsync`, `AlmacenIds.Principal`, enums de 3.3.
- Produces: `ProductoResponse.Existencia` (decimal, todos los almacenes, hoy) sustituye a `Stock`; filtro de
  listado `existencia` (igual semántica que el antiguo `stock`: `>=`) y `StockState` (`all`/`with`/`without`)
  calculado sobre la existencia; columna de orden `Existencia`.

SQL de apertura en `Up` (antes del `DropColumn`; el almacén `PRINCIPAL` existe por la semilla de 3.2):

```sql
INSERT INTO "MovimientosProducto" ("ProductoId","AlmacenId","TipoMovimiento","TipoDocumento","NumeroDocumento",
    "NumeroLineaDocumento","FechaRegistro","FechaDocumento","Cantidad","CantidadRestante","CantidadFacturada",
    "UnidadMedidaId","CantidadPorUnidadMedida","TipoOrigen","ClaveOrigen","CreatedAtUtc","CreatedBy")
SELECT p."Id", 'b1000000-0000-0000-0000-000000000001',
       CASE WHEN p."Stock" > 0 THEN 3 ELSE 4 END, 0, NULL, 0,
       CURRENT_DATE, CURRENT_DATE, p."Stock",
       CASE WHEN p."Stock" > 0 THEN p."Stock" END, 0,
       p."UnidadMedidaBaseId", 1, 99, 'MIGRACION-STOCK', now(), 'migracion'
FROM "Productos" p
WHERE p."Stock" <> 0;              -- incluye borrados lógicamente: su historia se conserva

INSERT INTO "MovimientosValor" ("MovimientoProductoId","ProductoId","AlmacenId","TipoValor","TipoMovimiento",
    "FechaRegistro","CantidadValorada","CantidadFacturada","ImporteCosto","CostoPorUnidad","ImporteVenta",
    "ImporteCostoPosteadoContabilidad","Ajuste","TipoDocumento","NumeroDocumento","NumeroLineaDocumento",
    "TipoOrigen","ClaveOrigen","CreatedAtUtc","CreatedBy")
SELECT m."Id", m."ProductoId", m."AlmacenId", 1, m."TipoMovimiento", m."FechaRegistro", m."Cantidad", 0,
       ROUND(m."Cantidad" * p."CostoUnitario", 4), p."CostoUnitario", 0, 0, false, 0, NULL, 0,
       99, 'MIGRACION-STOCK', now(), 'migracion'
FROM "MovimientosProducto" m JOIN "Productos" p ON p."Id" = m."ProductoId"
WHERE m."ClaveOrigen" = 'MIGRACION-STOCK';
```

(Usar `FechaRegistro = CURRENT_DATE` de la migración es aceptable y queda documentado; si el almacén PRINCIPAL se
hubiera borrado lógicamente antes de migrar, la migración lo reactiva primero con un `UPDATE` explícito.)

Lectura: el SELECT base de `DapperProductoReadRepository` (subconsulta aplanada existente) añade
`COALESCE(e."Existencia", 0) AS "Existencia"` con
`LEFT JOIN (SELECT "ProductoId", SUM("Cantidad") AS "Existencia" FROM "MovimientosProducto" GROUP BY "ProductoId") e`
— dentro de la subconsulta aplanada para que `ColumnasPermitidas` siga funcionando sin alias de tabla. Comprobar
con `EXPLAIN ANALYZE` en 30 000 productos / 100 000 movimientos que el listado paginado sigue por debajo de
~100 ms en local; si no, pasar a `LEFT JOIN LATERAL` por página y anotarlo.

API: `POST`/`PUT` ya no aceptan `stock` (propiedad desconocida ignorada por System.Text.Json; documentar en el XML
doc del request que el stock se mueve con diarios de inventario — Fase 4). `GET api/productos/{id}/existencias`
(`CanConsult`) → `ExistenciaAlmacen[]`; 404 si el producto no existe.

UI: el campo Stock desaparece de alta/edición; detalle muestra "Existencia" total y una tabla por almacén (de
`/existencias`); listado, dashboard, Home, reportería, PDF y Excel muestran "Existencia" (formato con los decimales
de la unidad base del producto, como máximo 6; sin notación científica). El Excel crudo cambia la cabecera `Stock`
por `Existencia`.

- [ ] **Step 1: Test de migración que falla** (antes de migrar sembrar: Stock 120 con costo 2.5, Stock 0, Stock −5,
  un producto borrado con Stock 7, un producto con unidad base `KG`): tras `Up` → existencia derivada 120/0/−5/7,
  `SUM(ImporteCosto)` = 300 / — / −5×costo / 7×costo, `CantidadRestante` 120 en la entrada y NULL en la salida,
  sin movimientos para Stock 0, columna `Stock` inexistente; `Down` recrea `Stock` con 120/0/−5/7; `Up` otra vez
  idempotente en resultado.
- [ ] **Step 2: Tests de API que fallan:** `existencia` en `ProductoResponse`; filtro `existencia=10`, `stockState=with/without`, orden por `Existencia`; `GET /existencias` por almacén tras registrar movimientos con el servicio; POST con `stock: 50` → existencia 0 (ignorado).
- [ ] **Step 3:** implementar migración, backend y UI.
- [ ] **Step 4:** tests + `ApplicationDbContextModelTests` + sonda vacía. `grep -rn "\bStock\b" src --include='*.cs' --include='*.razor'` fuera de `Migrations/` solo puede devolver `StockState`/textos de UI justificados.
- [ ] **Step 5: Runtime Blazor:** listado, alta, edición, detalle con tabla por almacén, dashboard, Home, exportaciones
  `.xlsx`/PDF con valores decimales y negativos; proxy 500/vacío en `/existencias` → el detalle sigue mostrándose con
  un aviso y sin romper la página.
- [ ] **Step 6:** suite completa; commit `feat: la existencia de producto se deriva del libro de inventario y Producto.Stock se migra a movimientos de apertura`.

---

## Task 3.7 — Página Blazor de Almacenes

**Files:**
- Create: `src/OpenSource1.Blazor/Components/Pages/Almacenes.razor`, `src/OpenSource1.Blazor/Components/AlmacenFields.razor`, `src/OpenSource1.Blazor/Services/IAlmacenApiClient.cs`, `AlmacenApiClient.cs`
- Modify: registro del cliente HTTP en `Program.cs`, menú de navegación (donde están Unidades de medida)

Copiar la estructura exacta de `UnidadesMedida.razor` (listado con filtros GET, formulario de alta/edición de un
solo paso, `ConfirmDialog` con `?deleteId=`, mensajes reales de la API, botón Limpiar que vuelve a la URL sin
parámetros). Campos: Código, Nombre, Dirección 1/2, Ciudad, País (select del VO `Pais` existente, como en Clientes),
Bloqueado, Predeterminado. El predeterminado se marca con una insignia en el listado y su botón Eliminar no se
muestra (la API igual lo rechazaría con 409 y el diálogo muestra el conflicto).

- [ ] **Step 1:** implementar.
- [ ] **Step 2: Runtime:** agregar, modificar (incluido cambiar el predeterminado), eliminar (normal, predeterminado
  forzado por POST → mensaje 409, con movimientos → 409), consultar con filtros, limpiar campos; Supervisor ve pero
  no puede agregar; sin `@rendermode`/`@onclick` (`grep`).
- [ ] **Step 3:** suite completa; commit `feat: pagina Blazor Static SSR de almacenes`.

---

## Task 3.8 — Cierre de la Fase 3

- [ ] Cadena completa de migraciones desde BD vacía y desde una BD con datos de la Fase 2 (productos con stock).
- [ ] `ApplicationDbContextModelTests` y sonda vacía.
- [ ] `grep` de `TODO`/`NotImplemented` y de `Stock` residual.
- [ ] Suite completa.
- [ ] Test de spec "ningún camino de código hace UPDATE ni DELETE sobre los libros": ya lo garantizan los triggers
  de 3.3; añadir además un test que recorra el código fuente (`src/**/*.cs`) y falle si encuentra
  `MovimientosValor`/`AplicacionesMovimientoProducto` junto a `UPDATE `/`DELETE `/`.Remove(`/`.Update(`.
