# Fase 4 — Diarios de inventario · Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** permitir cargar existencias iniciales y hacer ajustes positivos, negativos y reclasificaciones entre almacenes
mediante lotes de diario borrador que se registran de forma atómica en el libro de inventario de la Fase 3, con número de
registro sin huecos.

**Architecture:** plantillas de diario (sembradas, solo lectura), lotes y líneas borrador (maestros con `xmin` y soft delete,
CRUD como el resto de módulos), y un comando `PostearLoteDiarioCommand` que en UNA transacción bloquea los productos del lote,
reserva el número de la serie, llama a `IRegistroMovimientosInventario.RegistrarAsync` por cada línea, crea el
`RegistroDiario` (append-only) y borra las líneas. Blazor Static SSR con dos páginas: lotes y líneas de un lote.

**Tech Stack:** .NET 10, EF Core 10 + Npgsql, Dapper, MediatR 13, xUnit, Docker/Postgres real.

**Spec:** [`docs/superpowers/specs/2026-09-12-axionerp-erp-modules-design.md`](../specs/2026-09-12-axionerp-erp-modules-design.md), sección "Fase 4 — Diarios de inventario", más el bloque "Desviaciones acordadas durante la ejecución de la Fase 4" que añade la Task 4.1. Pendientes heredados: sección "Resultado y pendientes que hereda la Fase 4" de [`2026-09-25-fase-3-inventario-libro.md`](2026-09-25-fase-3-inventario-libro.md).

## Global Constraints

- TFM `net10.0`; nullable e implicit usings habilitados. Solución `test.slnx` (`dotnet build test.slnx`, `dotnet test test.slnx`).
- `PackageReference` directo por proyecto, sin CPM. No se añaden paquetes.
- Blazor Static SSR: sin `@rendermode`, `@onclick`, `@bind` interactivo ni JS nuevo. Formularios GET/POST con
  `[SupplyParameterFromForm]`/`[SupplyParameterFromQuery]`, antiforgery, `<EditForm>` de un solo paso con `FormName`
  estático, `ConfirmDialog` + `?deleteId=` solo para borrar (y, en esta fase, para confirmar el registro de un lote). Un
  `<select>` cargado de la API incluye siempre el valor vigente y bloquea Guardar si las opciones no cargaron. Mensajes
  reales de la API en 400/409.
- Nombres en español. Cantidades y factores `numeric(18,6)`; importes y costos `numeric(18,4)`.
- Errores: `Result`/`Error(Codigo, Mensaje, Campo)`; `.no_encontrado`→404, `.conflicto`→409, resto→400. Una referencia
  inválida en el cuerpo NO usa un código `.no_encontrado`.
- Migraciones con `dotnet ef migrations add`; quitar a mano las operaciones de columna `xmin` espurias; SQL a mano dentro
  de la migración generada. Tras cada migración: `ApplicationDbContextModelTests` en verde y sonda con `Up()` vacío.
- Tests contra Postgres real: `DOCKER_CONTEXT=default dotnet test ...` (solo variable de entorno). Suite completa al final de
  cada task (hoy 712 verdes).
- Commits LOCALES en `feat/erp-fase-4-diarios` (ramificada de `feat/erp-fase-3-inventario`); el usuario los autorizó. Nunca
  merge ni push. No tocar `appsettings*.json`.
- Runtime obligatorio para páginas Blazor (API + Blazor + Postgres reales, curl con cookie y antiforgery), con la evidencia en
  el informe.
- Toda escritura en el libro pasa por `IRegistroMovimientosInventario` (nunca SQL directo al libro desde esta fase).

## Review Focus

1. **Lote con una línea inválida en medio** (p. ej. la 3.ª de 5 sale más de lo que hay): el registro falla con el número de
   línea en el mensaje y **ninguna** fila nueva en el libro, en `RegistrosDiario` ni en la serie (el número no se consume);
   las líneas siguen en el lote. Test en Task 4.3.
2. **Dos usuarios registran a la vez dos lotes que tocan los mismos productos en orden distinto**: ambos terminan (o uno da
   409 reintentable), nunca 500 ni interbloqueo, y la existencia final es la suma correcta. Test en Task 4.3.
3. **Registrar el mismo lote dos veces** (doble clic, dos pestañas): la segunda vez no crea movimientos (lote vacío → 400
   `diario.lote_vacio`), nunca duplica. Test en Task 4.3.
4. **Editar una línea mientras otro la registra**: la edición recibe 409 por `xmin`/línea inexistente, no reaparece una
   línea ya registrada. Test en Task 4.2/4.3.
5. **Reclasificación**: la suma de `ImporteCosto` de sus movimientos de valor es exactamente 0 y la existencia total del
   producto no cambia; origen = destino → 400. Test en Task 4.3.

---

## Task 4.1 — Desviaciones de diseño (solo docs; la hace el controlador)

Añadir al spec, tras la verificación de la Fase 4, el bloque:

```markdown
**Desviaciones acordadas durante la ejecución de la Fase 4:**

- **Plantillas sembradas y de solo lectura.** `ARTICULO` (Tipo=1) y `RECLASIF` (Tipo=2), ambas con la serie `DIARIO-INV`
  (sin huecos). No hay CRUD de plantillas: dos tipos fijos no justifican un mantenimiento.
- **`LineasDiario.CostoUnitario` está en la unidad BASE del producto** (evita redondeos al convertir un costo por caja y
  respeta los 4 decimales del libro). La UI lo rotula "Costo unitario (unidad base)".
- **`CantidadPorUnidadMedida` se congela al guardar la línea** y el registro vuelve a obtener el factor: si cambió, el
  registro falla con `diario.factor_cambiado` y el número de línea (hay que volver a guardar la línea).
- **`NumeroDocumento` de la línea es opcional**; si viene vacío, los movimientos llevan el `NumeroRegistro`.
- **Orden de registro:** por `FechaRegistro`, luego entradas antes que salidas, luego `NumeroLinea`, para que una salida pueda
  consumir una entrada del mismo día del mismo lote.
- **Las líneas registradas se borran lógicamente** (las líneas son maestros con soft delete como el resto).
- **`RegistrosDiario` es append-only** (mismo trigger que el libro).
- **Borrar un almacén** toma un advisory lock exclusivo del almacén y `RegistrarAsync` toma el mismo lock en modo compartido,
  para que no pueda registrarse un movimiento en un almacén que se está borrando. Cambiar la unidad base y borrar un producto
  toman el lock del producto.
- **Permisos:** consultar = CanConsult; crear/editar lotes y líneas = CanAdd/CanModify; borrar = CanDelete; **registrar un
  lote = CanModify** (afecta al inventario; Administrador y Supervisor).
```

Commit junto con este plan.

---

## Task 4.2 — Plantillas, lotes y líneas de diario (dominio, API, tests)

**Files:**
- Create: `src/OpenSource1.Core/Entities/Inventario/PlantillaDiario.cs`, `LoteDiario.cs`, `LineaDiario.cs`; `src/OpenSource1.Core/Enums/TipoPlantillaDiario.cs`
- Modify: `ApplicationDbContext.cs` (DbSets, mapeo, `HasData` de las dos plantillas); migración `AddDiariosInventario` (generada; la serie `DIARIO-INV` y su línea se siembran con SQL como `SOCIOS` en `ExtendSocioNegocio`)
- Create: `src/OpenSource1.Application/Features/DiariosInventario/**` (Plantillas: listar/obtener; Lotes: CRUD; Líneas: CRUD por lote) + repositorios Dapper de lectura
- Create: `src/OpenSource1.Api/Controllers/DiariosInventarioController.cs` (`api/diarios-inventario`)
- Test: `tests/OpenSource1.SmokeTests/Features/DiariosInventario/**`, `tests/OpenSource1.SmokeTests/Api/DiariosInventarioApiTests.cs`

**Interfaces (Produces):**

```csharp
namespace OpenSource1.Core.Enums;
public enum TipoPlantillaDiario : short { Articulo = 1, Reclasificacion = 2 }

namespace OpenSource1.Core.Entities.Inventario;

public sealed class PlantillaDiario : BaseEntity
{
    public required string Codigo { get; set; }       // varchar(20), único parcial
    public required string Nombre { get; set; }       // varchar(100)
    public TipoPlantillaDiario Tipo { get; set; }
    public Guid SerieId { get; set; }                 // FK Series, Restrict
}

public static class PlantillaDiarioIds
{
    public static readonly Guid Articulo = Guid.Parse("d1000000-0000-0000-0000-000000000001");
    public static readonly Guid Reclasificacion = Guid.Parse("d1000000-0000-0000-0000-000000000002");
}

public sealed class LoteDiario : BaseEntity
{
    public Guid PlantillaDiarioId { get; set; }       // FK Restrict
    public required string Codigo { get; set; }       // varchar(20), MAYÚSCULAS; único parcial (PlantillaDiarioId, Codigo)
    public required string Nombre { get; set; }       // varchar(100)
    public Guid? SerieId { get; set; }                // FK nullable; null = la de la plantilla
    public bool Bloqueado { get; set; }
}

public sealed class LineaDiario : BaseEntity
{
    public Guid LoteDiarioId { get; set; }            // FK Restrict
    public int NumeroLinea { get; set; }              // único parcial (LoteDiarioId, NumeroLinea); lo asigna el sistema: max+10000
    public DateOnly FechaRegistro { get; set; }
    public DateOnly FechaDocumento { get; set; }
    public string? NumeroDocumento { get; set; }      // varchar(20)
    public TipoMovimientoInventario TipoMovimiento { get; set; } // AjustePositivo=3, AjusteNegativo=4, Transferencia=5
    public Guid ProductoId { get; set; }
    public Guid AlmacenId { get; set; }
    public Guid? AlmacenDestinoId { get; set; }       // solo Transferencia
    public Guid UnidadMedidaId { get; set; }
    public decimal CantidadPorUnidadMedida { get; set; } // (18,6) congelado al guardar
    public decimal Cantidad { get; set; }             // (18,6) > 0, en UnidadMedidaId
    public decimal? CostoUnitario { get; set; }       // (18,4) en unidad BASE; obligatorio en AjustePositivo
    public decimal ImporteCosto { get; set; }         // (18,4) calculado: Round(Cantidad × factor × CostoUnitario, 4) o 0
    public string? Descripcion { get; set; }          // varchar(200)
}
```

Reglas:
- Plantillas: `HasData` con `ARTICULO`/"Diario de artículos"/Articulo y `RECLASIF`/"Diario de reclasificación"/Reclasificacion,
  ambas con `SerieId` de `DIARIO-INV`; por eso la serie usa un Id fijo `e1000000-0000-0000-0000-000000000001` sembrado
  también por `HasData` (a diferencia de SOCIOS, no depende de datos) y su `LineaSerie` (`NumeroInicial` "000001",
  `NumeroFinal` "999999", `UltimoNumeroUsado` "000000", `FechaInicial` 2020-01-01) por `HasData` con Id fijo
  `e1000000-0000-0000-0000-000000000002`. Comprueba que `GeneradorNumeroDocumento` acepta "000000" como último usado
  (siguiente "000001"); si no, ajusta la semilla, no el generador.
- Endpoints (todas las rutas bajo `api/diarios-inventario`):
  - `GET plantillas` (CanConsult) → lista de las dos.
  - `GET lotes?plantillaId=&codigo=&nombre=&pagina=...` (CanConsult), `GET lotes/{id}`, `POST lotes` (CanAdd), `PUT lotes/{id}` (CanModify), `DELETE lotes/{id}` (CanDelete: 409 `diario.conflicto` si tiene líneas).
  - `GET lotes/{loteId}/lineas` (CanConsult, todas, orden `NumeroLinea`; sin paginar, un lote es pequeño; tope 1 000 líneas por lote validado al crear), `GET lineas/{id}`, `POST lotes/{loteId}/lineas` (CanAdd), `PUT lineas/{id}` (CanModify), `DELETE lineas/{id}` (CanDelete).
- Validación al guardar una línea (400 con `Campo`): lote existe y no bloqueado (`diario.lote_bloqueado`); `TipoMovimiento`
  permitido por la plantilla (Articulo: 3/4; Reclasificacion: solo 5) → `diario.tipo_movimiento_invalido`; producto existe
  y no bloqueado Todo, almacén existe y no bloqueado (`diario.producto_invalido`, `diario.almacen_invalido`); Transferencia
  exige `AlmacenDestinoId` distinto del origen (`diario.almacen_destino_invalido`) y en 3/4 debe ser null; cantidad > 0 y ≤
  6 decimales (`diario.cantidad_invalida`); AjustePositivo exige `CostoUnitario` ≥ 0, ≤ 4 decimales, < 1e14
  (`diario.costo_requerido` / `diario.costo_invalido`) y en 4/5 debe ser null; unidad: factor vía
  `IConversionUnidadMedidaService.ObtenerFactorAsync` (propaga su error con `Campo = UnidadMedidaId`); `NumeroDocumento` ≤ 20;
  `Descripcion` ≤ 200. **No** se valida existencia suficiente al guardar (se valida al registrar).
- Listado de lotes con `NumeroLineas` (conteo) para la UI.

- [ ] **Step 1: Tests que fallan** (handlers con fakes y API contra Postgres): cada regla de validación con su código y campo;
  la serie y las plantillas existen tras migrar y `SiguienteAsync("DIARIO-INV", hoy)` dentro de una transacción da "000001";
  `NumeroLinea` 10000, 20000, …; PUT de línea con `xmin` desactualizado → 409; borrar lote con líneas → 409, sin líneas → 204;
  código de lote duplicado en la misma plantilla → 409, en otra plantilla → 201; roles (Supervisor no crea; Ejecutor sí).
- [ ] **Step 2:** verlos fallar. **Step 3:** implementar. **Step 4:** tests + `ApplicationDbContextModelTests` + sonda vacía.
- [ ] **Step 5:** suite completa; commit `feat: plantillas, lotes y lineas de diario de inventario con validacion al capturar`.

---

## Task 4.3 — Registro de un lote (posteo) y cierre de los pendientes de la Fase 3

**Files:**
- Create: `src/OpenSource1.Core/Entities/Inventario/RegistroDiario.cs`; migración `AddRegistrosDiario` (+ trigger append-only reutilizando `libro_inventario_append_only()`)
- Create: `src/OpenSource1.Application/Features/DiariosInventario/Commands/PostearLoteDiarioCommand.cs` + handler; endpoint `POST api/diarios-inventario/lotes/{id}/registrar` (CanModify) → 200 `{ numeroRegistro, movimientos, desdeMovimientoProducto, hastaMovimientoProducto }`; `GET api/diarios-inventario/registros?loteId=&pagina=...` (CanConsult)
- Modify: `RegistroMovimientosInventario.cs` (lock compartido del almacén), `BloqueoInventarioProducto.cs` (o una clase hermana `BloqueoInventarioAlmacen`), `DeleteAlmacenCommandHandler.cs`, `DeleteProductoCommandHandler.cs`, `UpdateProductoCommandHandler.cs` (locks de los pendientes de la Fase 3)
- Test: `tests/OpenSource1.SmokeTests/Features/DiariosInventario/PostearLoteDiarioTests.cs` (Postgres real), ampliar `DiariosInventarioApiTests`

**Interfaces:**

```csharp
public sealed class RegistroDiario       // sin BaseEntity: append-only, sin soft delete ni xmin
{
    public long Id { get; set; }                          // identidad
    public required string NumeroRegistro { get; set; }   // varchar(20), único
    public Guid LoteDiarioId { get; set; }                // FK Restrict
    public long DesdeMovimientoProducto { get; set; }
    public long HastaMovimientoProducto { get; set; }
    public int Lineas { get; set; }
    public DateTimeOffset FechaCreacion { get; set; }
    public required string CreadoPor { get; set; }        // varchar(100)
    public Guid? UsuarioId { get; set; }
}

public sealed record ResultadoRegistroLote(string NumeroRegistro, int Movimientos, long DesdeMovimientoProducto, long HastaMovimientoProducto);
public sealed record PostearLoteDiarioCommand(Guid LoteDiarioId) : IRequest<Result<ResultadoRegistroLote>>;
```

Algoritmo del handler (una transacción de `IUnitOfWork`, que enrola EF y Dapper; si el comando es `ICommand` y el
`TransactionBehavior` ya abre la transacción, reutilízala; en ningún caso dos transacciones):

1. Cargar el lote (404 `diario.no_encontrado` si no existe; 400 `diario.lote_bloqueado`) y sus líneas **con `FOR UPDATE`**
   sobre las filas de `LineasDiario` (serializa dos registros del mismo lote); sin líneas → 400 `diario.lote_vacio`.
2. Prevalidar cada línea (mismas reglas estáticas que al guardar, contra el estado ACTUAL de producto/almacén/unidad; el
   factor actual debe ser igual al congelado → si no, `diario.factor_cambiado`). Si alguna falla: devolver TODOS los
   errores, cada uno con `Campo = "Lineas[<NumeroLinea>].<Campo>"`, sin escribir nada.
3. `BloquearProductosAsync` con todos los productos del lote.
4. `IGeneradorNumeroDocumento.SiguienteAsync(serie del lote ?? serie de la plantilla, hoy)` → `NumeroRegistro`.
5. Para cada línea en el orden de las desviaciones: `RegistrarAsync` con `TipoDocumento = RegistroDiario`,
   `NumeroDocumento = línea.NumeroDocumento ?? NumeroRegistro`, `NumeroLineaDocumento = NumeroLinea`,
   `TipoOrigen = Diario`, `ClaveOrigen = NumeroRegistro`. AjustePositivo: entrada con el costo de la línea. AjusteNegativo:
   salida. Transferencia: salida en origen y, con el costo `-ImporteCosto / CantidadBase` de su `MovimientoRegistrado`,
   entrada `Transferencia` en destino (mismo `NumeroLineaDocumento`). Si cualquier `RegistrarAsync` falla → rollback
   completo y devolver su error con `Campo = "Lineas[<NumeroLinea>].<Campo original>"`.
6. Invariante: por cada reclasificación, `ImporteCosto` de salida + entrada = 0 (comprobar en memoria; si no, excepción).
7. Insertar `RegistroDiario` (desde/hasta = min/max `MovimientoProductoId` generados) y borrar lógicamente las líneas.
8. Commit.

Pendientes de la Fase 3:
- `RegistrarAsync` toma, después del lock del producto, `pg_advisory_xact_lock_shared(<clave del almacén>)` del almacén (y
  del destino cuando lo registra el handler, porque es otra llamada). `DeleteAlmacenCommandHandler` abre transacción, toma el
  mismo lock en exclusivo, comprueba movimientos y borra. Clave: `hashtextextended('almacen:' || id::text, 0)` para no
  chocar con la del producto; documentarla junto a la del producto.
- `DeleteProductoCommandHandler` y el cambio de unidad base en `UpdateProductoCommandHandler`: dentro de una transacción,
  `BloqueoInventarioProducto` antes de comprobar movimientos (necesitan acceso al lock desde Application: expónlo con
  `IRegistroMovimientosInventario.BloquearProductosAsync` ya existente).

- [ ] **Step 1: Tests que fallan:**
  - lote Articulo: +10 a 5 (P1, ALM1), −3 (P1, ALM1), +2 a 7 (P2, ALM2) → existencias 7 y 2, un `RegistroDiario`
    "000001" con `Lineas = 3` y desde/hasta correctos, líneas borradas, `CostoAjustado = false` en P1;
  - Review Focus 1: 5 líneas, la 3.ª sale de más → error con `Campo` "Lineas[30000].Cantidad", cero filas nuevas en libro y
    registros, las 5 líneas siguen, y el siguiente registro correcto recibe "000001" (número no consumido);
  - prevalidación: dos líneas inválidas → dos errores en la misma respuesta;
  - Review Focus 5: reclasificación de 4 de ALM1 a ALM2 → dos movimientos `Transferencia`, `SUM(ImporteCosto) = 0`, existencia
    total igual; origen = destino rechazado al guardar;
  - salida y entrada del mismo día en el mismo lote con la salida en una línea anterior → se registra (orden de las desviaciones);
  - factor cambiado entre guardar y registrar (actualiza a mano la fila de `UnidadesMedidaProducto`) → `diario.factor_cambiado`;
  - Review Focus 3: registrar dos veces seguidas → la segunda 400 `diario.lote_vacio`; en paralelo (dos conexiones, barrera)
    → exactamente un éxito y un `lote_vacio` (o 409), sin movimientos duplicados;
  - Review Focus 2: dos lotes concurrentes con líneas P1,P2 y P2,P1 → ambos terminan, existencias correctas;
  - Review Focus 4: PUT de una línea tras registrar el lote → 404/409, la línea no reaparece;
  - borrar un almacén en paralelo con un registro en ese almacén → o el registro falla (almacén inválido) o el borrado da 409;
    nunca un movimiento en un almacén borrado (test con barrera);
  - API: registrar como Supervisor → 200, como Ejecutor → 403; `GET registros` lista el registro.
- [ ] **Step 2:** verlos fallar. **Step 3:** implementar. **Step 4:** verdes; mutaciones: quitar el `FOR UPDATE` de las líneas
  (el test de doble registro en paralelo debe fallar), quitar el lock compartido del almacén (el test de borrado concurrente
  debe fallar), invertir el orden entradas/salidas del mismo día (su test debe fallar).
- [ ] **Step 5:** suite completa; commit `feat: registro atomico de lotes de diario de inventario con numero sin huecos y cierre de los bloqueos pendientes`.

---

## Task 4.4 — Páginas Blazor de diarios de inventario

**Files:**
- Create: `src/OpenSource1.Blazor/Components/Pages/DiariosInventario.razor` (lotes), `DiarioInventarioLote.razor` (líneas y registro, ruta `/diarios-inventario/{loteId:guid}`), componentes de campos, `IDiarioInventarioApiClient`/`DiarioInventarioApiClient`
- Modify: `Program.cs` (cliente HTTP), `MainLayout.razor` (menú)

Página de lotes: selector de plantilla (GET), listado de lotes con filtros y número de líneas, alta/edición/borrado de lote,
limpiar campos (patrón de `Almacenes.razor`). Página de un lote: cabecera del lote, tabla de líneas, formulario de alta/edición
de línea (selects de producto, almacén, almacén destino solo en reclasificación, unidad de medida — la base del producto y, si
existieran, sus equivalencias; fechas; cantidad y costo con `EntradaDecimal`), borrado de línea con `ConfirmDialog`, y botón
"Registrar lote" que abre un `ConfirmDialog` (`?registrar=true`) y hace POST al endpoint; tras éxito muestra el número de
registro y un enlace a la ficha de cada producto; tras error muestra todos los mensajes (con su número de línea). Selects de
producto y almacén: si hay muchos, usar el filtro GET existente del listado de productos (búsqueda por texto) en vez de cargar
todos; los selects incluyen siempre el valor vigente.

- [ ] **Step 1:** implementar. **Step 2: Runtime** con Postgres/API/Blazor: crear lote en ARTICULO, agregar líneas (positiva,
  negativa), editar, borrar, registrar con éxito (número 000001, existencias en la ficha del producto), registrar con una línea
  que sale de más (mensaje con número de línea, líneas intactas), lote de reclasificación, Supervisor registra, Ejecutor no ve
  el botón y un POST forzado da 403 (mensaje), select con API caída (proxy) bloquea Guardar. `grep` sin
  `@rendermode`/`@onclick`. **Step 3:** suite completa; commit `feat: paginas Blazor Static SSR de diarios de inventario`.

---

## Task 4.5 — Cierre de la Fase 4

- [x] Cadena de migraciones desde BD vacía y desde una BD en el estado final de la Fase 3 con datos; Down a
  `AddIndicesLibroInventario` y vuelta a HEAD.
- [x] `ApplicationDbContextModelTests`, sonda vacía, `has-pending-model-changes`.
- [x] Añadir `RegistrosDiario` al test de código fuente de la Fase 3 (`LibroInventarioSinUpdateNiDeleteTests`).
- [x] `grep` TODO/NotImplemented; suite completa.
- [x] Añadir al final de este plan "Resultado y pendientes que hereda la Fase 5".

---

## Resultado y pendientes que hereda la Fase 5

**Resultado.** La Fase 4 queda cerrada: plantillas (`ARTICULO`/`RECLASIF`, sembradas de solo lectura sobre la serie
`DIARIO-INV`), lotes y líneas de diario con validación al capturar (Task 4.1/4.2), registro atómico de un lote completo
en una sola transacción con numeración sin huecos y cierre de los bloqueos pendientes (`RegistroDiario`, Task 4.3), y las
páginas Blazor Static SSR correspondientes (Task 4.4). La cadena de migraciones se verificó completa en ambos sentidos
(BD vacía → HEAD; BD en el estado final de la Fase 3 con productos y movimientos de apertura `TipoOrigen = 99` → HEAD →
Down a `AddIndicesLibroInventario` → HEAD de nuevo), sin pérdida de los movimientos previos y con la serie/plantillas
recreándose igual en cada pasada. `RegistrosDiario` quedó protegida por el mismo mecanismo de solo-código-fuente que
`MovimientosValor`/`AplicacionesMovimientoProducto` (Task 4.5).

**Limitaciones conocidas que hereda la Fase 5:**
- **Unidad de la línea siempre la base del producto.** La API y la UI de Blazor no ofrecen un selector de unidad de
  medida distinta de `UnidadMedidaBaseId` al capturar una línea de diario: no existe todavía un endpoint de
  equivalencias de unidad expuesto para este flujo (`IConversionUnidadMedidaService` sí soporta factores distintos de 1
  puertas adentro, pero nadie en diarios se lo pide). Si la Fase 5 necesita capturar en una unidad de compra/venta
  distinta de la base, hace falta ese endpoint y su selector.
- **Select de almacenes sin paginar (hasta 200).** `DiarioInventarioLote.razor` carga el catálogo completo de almacenes
  con `PageRequest.TamanoMaximo = 200` en vez de un buscador por texto como el de productos. Razonable mientras el
  catálogo de almacenes sea pequeño; si creciera, necesita el mismo patrón de búsqueda GET que productos.
- **`NumeroRegistro` único global entre series.** El índice único de `RegistrosDiario.NumeroRegistro` no está compuesto
  con la serie: hoy solo existe `DIARIO-INV`, así que no hay colisión posible, pero si la Fase 5 (u otra) agrega una
  segunda serie `DIARIO-*` que produzca el mismo número, el segundo registro recibiría 409 en vez de coexistir. Aceptado
  en su momento (Ruling BE de la Task 4.3), documentado para no sorprender más adelante.
- **~7 consultas por línea en la prevalidación del registro.** `PostearLoteDiarioCommandHandler` prevalida cada línea
  del lote con varias consultas independientes (producto, almacén(es), factor de unidad, existencia, bloqueo...) en vez
  de una consulta batch por lote. Aceptado con el lote acotado a 1000 líneas (Ruling BE); si la Fase 5 sube ese tope o
  necesita lotes más grandes en volumen, conviene revisar el patrón de acceso a datos de esta prevalidación.
- **Fecha de la serie en UTC.** `LineaSerie.FechaInicial`/el corte diario de la numeración usan la fecha en UTC, no la
  fecha local del usuario; en husos horarios alejados de UTC, un registro cerca de medianoche podría numerarse con la
  fecha "equivocada" desde la perspectiva del usuario. Documentado en la Task 4.3, sin corregir en esta fase.
- **La relajación de 4 decimales del costo aplica a CUALQUIER entrada de tipo Transferencia**, no solo a la que genera
  el diario: `RegistroMovimientosInventario.RegistrarAsync` la decide por `TipoMovimiento`, no por `TipoOrigen`. Hoy
  solo el diario de inventario produce entradas Transferencia, así que el efecto observable es el mismo, pero un
  futuro llamador directo de `RegistrarAsync` con una entrada Transferencia heredaría la misma relajación aunque no
  sea una reclasificación.
- **Una cadena de transferencias del mismo día (A→B y luego B→C) solo se registra si sus líneas están en el orden de
  la dependencia** (A→B antes que B→C): el registro procesa las líneas por `FechaRegistro`, entradas antes que
  salidas, y `NumeroLinea`, y `NumeroLinea` es inmutable una vez capturada la línea (no hay "mover línea" en la UI ni
  en la API). Si el usuario captura B→C antes que A→B en el mismo lote y misma fecha, la segunda transferencia falla
  por existencia insuficiente aunque la primera la habría cubierto.
- **Cambiar la unidad base de un producto que ya tiene líneas de diario capturadas (sin registrar) no las actualiza.**
  `CantidadPorUnidadMedida` quedó congelada con el factor de la unidad elegida al guardar la línea; si esa unidad deja
  de ser la base, el registro fallará con `diario.factor_cambiado` (Task 4.2). La única salida desde la UI es "Cambiar
  producto" en esas líneas (borrarlas y volver a capturarlas), no hay una forma de "releer" el factor sin recapturar.
- **Serialización global de un mismo libro por la serie de numeración.** El `FOR UPDATE` de la línea de la serie en
  `IGeneradorNumeroDocumento.SiguienteAsync` serializa TODOS los registros que usan esa serie (hoy, `DIARIO-INV`),
  independientemente del producto: dos posteos que no comparten ningún producto igual se esperan entre sí durante ese
  tramo. Es la razón por la que un test que quitara el prebloqueo de productos (`BloquearProductosAsync`) seguiría sin
  fallar entre dos lotes de diario distintos: el prebloqueo de productos evita el interbloqueo A/B-B/A dentro de un
  mismo posteo, pero la protección contra OTROS escritores del libro (otro registro, otro borrado de producto/almacén)
  ya la da el orden global de locks (lote → líneas → productos → línea de serie → almacenes).
- Borrar una línea mientras otro registra el lote devuelve 409 (conflicto de `xmin`) en lugar de un 404 limpio: el borrado lee
  la línea antes de tomar el lock del lote y no la relee. Sin corrupción (rollback).
- Borrar un lote con `Bloqueado = true` no se rechaza (sí se rechazan altas, ediciones y borrados de sus líneas).
