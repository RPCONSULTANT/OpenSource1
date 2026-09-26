# Fase 5 — Contabilidad simplificada · Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** plan de cuentas, grupos contables, tablas de setup (intersecciones) con derivación de cuentas por comodín, libro
contable append-only con registros que cuadran a 0, y el batch idempotente que contabiliza el costo del inventario; con
datos semilla suficientes para que la Fase 6 pueda facturar recién instalada.

**Architecture:** maestros (cuentas, grupos, setups) con el patrón CQRS-lite del proyecto y sus páginas Blazor Static SSR.
Un servicio `IRegistroContable` es el único escritor del libro contable (mismo papel que `IRegistroMovimientosInventario`
en inventario): recibe un asiento balanceado, numera con la serie `CONTAB`, inserta movimientos y registro, y verifica el
cuadre. `IDerivadorCuentas` resuelve cuentas con la regla "fila exacta → fila con comodín NULL → error". El batch de costo
lee `MovimientosValor` pendientes, deriva cuentas y postea deltas.

**Tech Stack:** .NET 10, EF Core 10 + Npgsql, Dapper, MediatR 13, xUnit, Docker/Postgres real.

**Spec:** [`docs/superpowers/specs/2026-09-12-axionerp-erp-modules-design.md`](../specs/2026-09-12-axionerp-erp-modules-design.md), sección "Fase 5 — Contabilidad simplificada", más el bloque "Desviaciones acordadas durante la ejecución de la Fase 5" que añade la Task 5.1. Pendientes heredados: sección "Resultado y pendientes que hereda la Fase 5" de [`2026-09-26-fase-4-diarios-inventario.md`](2026-09-26-fase-4-diarios-inventario.md).

## Global Constraints

- TFM `net10.0`; nullable e implicit usings. Solución `test.slnx`. Sin paquetes nuevos, sin CPM.
- Blazor Static SSR: sin `@rendermode`, `@onclick`, `@bind` interactivo ni JS nuevo; `<EditForm>` de un paso con `FormName`
  estático; `ConfirmDialog` + `?deleteId=` para borrar; selects cargados de la API con el valor vigente y bloqueo de Guardar si
  no cargaron; mensajes reales de la API; cada módulo de mantenimiento con agregar, modificar, eliminar, consultar y limpiar
  campos; `EntradaDecimal` para importes y porcentajes.
- Nombres en español. Importes `numeric(18,4)`; porcentajes `numeric(9,5)`.
- Errores: `Result`/`Error(Codigo, Mensaje, Campo)`; `.no_encontrado`→404, `.conflicto`→409, resto→400; una referencia
  inválida en el cuerpo NO usa un código `.no_encontrado`.
- Maestros: `BaseEntity` (auditoría, soft delete, `xmin`), índices únicos parciales `"IsDeleted" = false`, PUT con `Xmin`
  (concurrencia optimista vía `IGenericRepository.EstablecerVersionOriginal`, como lotes/líneas de diario) y "null = conservar"
  en campos opcionales. Borrar un maestro en uso → 409 `<entidad>.conflicto`.
- Libros: PK `bigint` identidad, append-only con el trigger `libro_inventario_append_only()` (o uno propio igual), sin soft
  delete ni `xmin`, sin `IAggregateRoot`, protegidos por `LibroInventarioSinUpdateNiDeleteTests`.
- Migraciones con `dotnet ef migrations add`; sin operaciones `xmin` espurias; `ApplicationDbContextModelTests` en verde y
  sonda con `Up()` vacío tras cada migración.
- Tests contra Postgres real con `DOCKER_CONTEXT=default` como variable de entorno. Suite completa al final de cada task
  (hoy 829). La build tiene 6 avisos CS0618 preexistentes; no añadir avisos.
- Commits LOCALES autorizados en `feat/erp-fase-5-contabilidad` (desde `feat/erp-fase-4-diarios`). Nunca merge ni push. No
  tocar `appsettings*.json`.
- Permisos: consultar = CanConsult; alta = CanAdd; modificar = CanModify; borrar = CanDelete; **ejecutar el batch de costo =
  CanModify**.
- Runtime obligatorio (API + Blazor + Postgres reales) para cada página nueva, con evidencia en el informe.

## Review Focus

1. **Asiento descuadrado** (bug de cálculo, redondeo): `IRegistroContable` rechaza con `Result` fallido y no escribe nada;
   nunca queda un registro con `SUM(Importe) <> 0`. Test en Task 5.5.
2. **Combinación sin setup** en el batch de costo (producto con un grupo de inventario sin fila en `SetupsInventario` ni
   comodín): ese movimiento se informa como pendiente con `setup_contable.inexistente` y el detalle de la combinación; los
   demás se contabilizan; nada a medias para el que falla. Test en Task 5.6.
3. **Batch dos veces seguidas / en paralelo:** la segunda no inserta nada; en paralelo nunca se contabiliza dos veces el
   mismo delta. Test en Task 5.6.
4. **Borrar una cuenta usada** por un setup, un grupo de cliente o un movimiento → 409; cuenta `Encabezado`/`Total` o
   bloqueada en un setup → 400. Test en Tasks 5.2/5.4.
5. **Comodín:** fila exacta gana a la de comodín; comodín en un eje no se confunde con el otro eje. Test en Task 5.4.

---

## Task 5.1 — Desviaciones de diseño (solo docs; la hace el controlador)

Añadir al spec, tras la verificación de la Fase 5:

```markdown
**Desviaciones acordadas durante la ejecución de la Fase 5:**

- **`ImporteCostoPosteadoContabilidad` es la segunda columna actualizable del libro de valor.** El trigger append-only de
  `MovimientosValor` pasa a permitir un `UPDATE` que cambie ÚNICAMENTE esa columna (como `CantidadRestante` en
  `MovimientosProducto` y "Cost Posted to G/L" en BC); todo lo demás sigue prohibido.
- **El batch de costo contabiliza todos los tipos de movimiento, no solo ventas.** Para cada delta: débito/crédito
  `CuentaInventario` (de `SetupsInventario[Almacén × GrupoInventario]`) por `+delta` y la contrapartida por `−delta` en
  `CuentaCostoVentas` si el movimiento es Venta, o en `CuentaAjusteInventario` (de `SetupsInventario`) en ajustes, apertura
  migrada y compras; las transferencias contabilizan inventario contra inventario entre almacenes (si la cuenta es la misma,
  se netea y no se escribe). Un asiento por ejecución y grupo de movimientos; cada movimiento de valor se contabiliza entero
  o no se contabiliza.
- **Grupos en maestros:** `Producto` gana `GrupoProductoId`, `GrupoIvaProductoId`, `GrupoInventarioId`; `SocioNegocio`
  gana `GrupoNegocioId`, `GrupoIvaNegocioId`, `GrupoClienteContableId`. Todas FK nulables; la migración asigna los grupos
  semilla por defecto (`BIENES`, `ITBIS18`, `GENERAL`; `NACIONAL`, `ITBIS18`, `GENERAL`) a los registros existentes. Un
  grupo nulo en el momento de derivar → `setup_contable.grupo_faltante`.
- **Congelación de grupos en el libro de valor (D8):** `RegistrarAsync` copia `GrupoInventarioId`/`GrupoProductoId` del
  producto y `GrupoNegocioId` del socio (si lo hay) al `MovimientoValor`. La migración rellena los movimientos ya existentes
  con los grupos por defecto, desactivando el trigger SOLO dentro de esa migración.
- **Un solo mantenimiento para los cinco grupos simples** (`GruposNegocio`, `GruposProducto`, `GruposIvaNegocio`,
  `GruposIvaProducto`, `GruposInventario`): API `api/grupos-contables/{tipo}` y una página con selector de tipo.
  `GruposClienteContable` tiene su propio mantenimiento (lleva cuentas).
- **Numeración:** los registros contables usan la serie `CONTAB` (sin huecos, sembrada).
- **Sin posteo contable al facturar** en esta fase: `ConfiguracionInventario.PosteoAutomaticoCosto` no se implementa (el
  batch es el único camino, como el valor por defecto del spec).
```

Commit junto con este plan.

---

## Task 5.2 — Plan de cuentas (dominio, API, página)

**Files:**
- Create: `src/OpenSource1.Core/Entities/Contabilidad/CuentaContable.cs`; `src/OpenSource1.Core/Enums/TipoCuentaContable.cs`, `TipoResultadoCuenta.cs`
- Modify: `ApplicationDbContext.cs`; migración `AddPlanCuentas` (con las cuentas semilla por `HasData`, Ids fijos `f1000000-0000-0000-0000-0000000000NN`)
- Create: `src/OpenSource1.Application/Features/CuentasContables/**`, `DapperCuentaContableReadRepository`, `src/OpenSource1.Api/Controllers/CuentasContablesController.cs` (`api/cuentas-contables`)
- Create: `src/OpenSource1.Blazor/Components/Pages/CuentasContables.razor` + campos + cliente HTTP; menú "Contabilidad"
- Test: `tests/OpenSource1.SmokeTests/Features/CuentasContables/**`, `Api/CuentasContablesApiTests.cs`

```csharp
namespace OpenSource1.Core.Enums;
public enum TipoCuentaContable : short { Posteo = 1, Encabezado = 2, Total = 3, InicioTotal = 4, FinTotal = 5 }
public enum TipoResultadoCuenta : short { Resultado = 1, Balance = 2 }

namespace OpenSource1.Core.Entities.Contabilidad;
public sealed class CuentaContable : BaseEntity
{
    public required string Numero { get; set; }        // varchar(20), [0-9.-], único parcial
    public required string Nombre { get; set; }        // varchar(100)
    public TipoCuentaContable TipoCuenta { get; set; }
    public TipoResultadoCuenta TipoResultado { get; set; }
    public bool PosteoDirecto { get; set; }
    public bool Bloqueada { get; set; }
    public int Sangria { get; set; }                   // 0-10
}
public static class CuentaContableIds { /* Caja, CxC, Inventario, IvaPorPagar, Ventas, CostoVentas, AjusteInventario, DescuentoVentas */ }
```

Semilla (Numero / Nombre / Tipo / Resultado): `1` Activos (Encabezado, Balance), `1101` Caja, `1201` Cuentas por cobrar
clientes, `1301` Inventario de mercancías (Posteo, Balance); `2` Pasivos (Encabezado), `2101` ITBIS por pagar (Posteo,
Balance); `4` Ingresos (Encabezado, Resultado), `4101` Ventas, `4102` Descuentos sobre ventas (Posteo, Resultado); `5` Costos
(Encabezado, Resultado), `5101` Costo de ventas, `5201` Ajustes de inventario (Posteo, Resultado). `PosteoDirecto = false` en
las que solo toca el sistema (CxC, Inventario, ITBIS por pagar, Costo de ventas); `true` en el resto de Posteo.

Reglas: cambiar `TipoCuenta` de Posteo a otro tipo, o borrar, una cuenta con movimientos contables o referenciada por
cualquier setup/grupo de cliente → 409 `cuenta_contable.conflicto` (la guarda de movimientos se añade en la Task 5.5 cuando
exista el libro; aquí solo referencias de setups/grupos, que llegan en 5.3/5.4 — dejar el método de guarda preparado en el
repositorio y ampliarlo en cada task, con su test). Listado ordenado por `Numero` por defecto, filtros `numero`, `nombre`,
`tipoCuenta`, `tipoResultado`, `bloqueada`. Página: listado con sangría visual por `Sangria`, alta/edición/borrado/limpiar.

- [ ] Tests (validaciones, duplicado 409, semilla presente, roles, xmin 409) → implementar → runtime de la página → suite → commit `feat: plan de cuentas con semilla, API y pagina`.

---

## Task 5.3 — Grupos contables y grupos en productos y socios

**Files:**
- Create: `src/OpenSource1.Core/Entities/Contabilidad/GrupoContable.cs` (base abstracta con `Codigo varchar(20)`, `Descripcion varchar(100)`), `GrupoNegocio.cs`, `GrupoProducto.cs`, `GrupoIvaNegocio.cs`, `GrupoIvaProducto.cs`, `GrupoInventario.cs`, `GrupoClienteContable.cs` (+ `CuentaCxCId` NOT NULL, `CuentaDescuentoId?`, `CuentaInteresId?`); enum `TipoGrupoContable` (Negocio=1, Producto=2, IvaNegocio=3, IvaProducto=4, Inventario=5)
- Modify: `Producto.cs`, `SocioNegocio.cs` (grupos, ver desviaciones), `ApplicationDbContext.cs`; migración `AddGruposContables` (tablas, semillas `HasData`, columnas en Productos/SociosNegocio con backfill por SQL a los grupos por defecto)
- Create: Application `Features/GruposContables/**` (un conjunto genérico parametrizado por `TipoGrupoContable`) y `Features/GruposClienteContable/**`; controladores `api/grupos-contables/{tipo}` y `api/grupos-cliente-contable`
- Modify: comandos/DTOs/validadores de Productos y SociosNegocio (grupos opcionales en POST/PUT con "null = conservar"; validan que el grupo exista → `<entidad>.grupo_invalido`), repositorios Dapper de lectura (Id y Codigo de cada grupo en el DTO)
- Create/Modify Blazor: página `GruposContables.razor` (selector de tipo por query), página `GruposClienteContable.razor` (con selects de cuentas de Posteo no bloqueadas), y selects de grupos en `ProductoFields`/`ClienteFields` y en las fichas
- Test: handlers, API, migración con datos (productos y socios existentes quedan con los grupos por defecto)

Semilla (Ids fijos `f2000000-...`): Negocio `NACIONAL`, `EXTERIOR`; Producto `BIENES`, `SERVICIOS`; IvaNegocio `ITBIS18`,
`EXENTO`; IvaProducto `ITBIS18`, `EXENTO`; Inventario `GENERAL`; ClienteContable `GENERAL` (CxC = `1201`, Descuento =
`4102`). Borrar un grupo usado por un producto, socio o setup → 409 `grupo_contable.conflicto` / `grupo_cliente_contable.conflicto`.
`CuentaCxCId` debe ser una cuenta Posteo no bloqueada → si no, 400 `grupo_cliente_contable.cuenta_invalida`.

- [ ] Tests → implementar → runtime (grupos, grupo de cliente, selects de grupos en producto y cliente incl. API caída) → suite → commit `feat: grupos contables, grupos de cliente y clasificacion contable de productos y socios`.

---

## Task 5.4 — Setups (intersecciones) y derivador de cuentas

**Files:**
- Create: `src/OpenSource1.Core/Entities/Contabilidad/SetupContableGeneral.cs`, `SetupIva.cs`, `SetupInventario.cs`; enum `TipoCalculoIva` (Normal=1, Exento=2)
- Modify: `ApplicationDbContext.cs`; migración `AddSetupsContables` con semillas
- Create: Application `Features/SetupsContables/**` (tres CRUD), `Services/Contabilidad/IDerivadorCuentas.cs` + `SetupIvaResuelto`; Infrastructure `Services/Contabilidad/DerivadorCuentas.cs` (Dapper)
- Create: controlador `api/setups-contables/{general|iva|inventario}`; página `SetupsContables.razor` con selector de tipo
- Test: derivador (exacta, comodín, sin setup, grupo nulo), handlers, API, página en runtime

```csharp
public sealed record SetupIvaResuelto(decimal PorcentajeIva, Guid CuentaIvaVentasId, Guid? CuentaIvaComprasId,
    string IdentificadorIva, TipoCalculoIva TipoCalculo);
public interface IDerivadorCuentas
{
    Task<Result<Guid>> CuentaCxCAsync(Guid? grupoClienteContableId, CancellationToken ct = default);
    Task<Result<Guid>> CuentaVentasAsync(Guid? grupoNegocioId, Guid? grupoProductoId, CancellationToken ct = default);
    Task<Result<Guid>> CuentaCostoVentasAsync(Guid? grupoNegocioId, Guid? grupoProductoId, CancellationToken ct = default);
    Task<Result<Guid>> CuentaDescuentoVentasAsync(Guid? grupoNegocioId, Guid? grupoProductoId, CancellationToken ct = default);
    Task<Result<SetupIvaResuelto>> IvaAsync(Guid? grupoIvaNegocioId, Guid? grupoIvaProductoId, CancellationToken ct = default);
    Task<Result<Guid>> CuentaInventarioAsync(Guid? almacenId, Guid? grupoInventarioId, CancellationToken ct = default);
    Task<Result<Guid>> CuentaAjusteInventarioAsync(Guid? almacenId, Guid? grupoInventarioId, CancellationToken ct = default);
}
```

Reglas del derivador: el grupo "principal" de cada lookup (grupoProducto en general, grupoIvaProducto en IVA,
grupoInventario en inventario, grupoClienteContable en CxC) es obligatorio: nulo → `setup_contable.grupo_faltante` con el
nombre del grupo. Resolución: fila exacta (ambas claves) → fila con el eje secundario `NULL` (comodín) → error
`setup_contable.inexistente` con mensaje "No existe setup <tabla> para <GrupoA=código> × <GrupoB=código|cualquiera>". Solo
las filas no borradas. La cuenta resuelta debe ser Posteo y no bloqueada → si no, `setup_contable.cuenta_invalida`. Con
`PostgreSQL`, un índice único con columnas nulables no impide dos filas con `NULL`: usar índices únicos con
`NULLS NOT DISTINCT` (PostgreSQL 15+; comprobar la versión del contenedor de tests y documentarlo) o un índice único
parcial por cada combinación de nulos.

Semillas: General `NACIONAL×BIENES`, `NACIONAL×SERVICIOS`, `NULL×BIENES`, `NULL×SERVICIOS` → Ventas 4101, Costo 5101,
Descuento 4102, Ajuste 5201; IVA `ITBIS18×ITBIS18` (18, ITBIS por pagar 2101, identificador `ITBIS18`, Normal),
`ITBIS18×EXENTO` y `EXENTO×ITBIS18` y `EXENTO×EXENTO` (0, 2101, `EXENTO`, Exento); Inventario `NULL×GENERAL` → Inventario
1301, Ajuste 5201, Variación 5201.

Validaciones de los CRUD: cuentas referenciadas deben ser Posteo no bloqueadas; `PorcentajeIva` 0–100 con ≤5 decimales;
Exento exige porcentaje 0; combinación duplicada → 409. La guarda de borrado de cuentas (Task 5.2) se amplía a setups.

- [ ] Tests (Review Focus 4 y 5 incluidos) → implementar → runtime de la página → suite → commit `feat: setups contables con comodin y derivador de cuentas`.

---

## Task 5.5 — Libro contable, registro de asientos y grupos congelados en el libro de valor

**Files:**
- Create: `src/OpenSource1.Core/Entities/Contabilidad/MovimientoContable.cs`, `RegistroContable.cs`; migración `AddLibroContable` (tablas, índices del spec 5.5, trigger append-only, serie `CONTAB` por `HasData` como `DIARIO-INV`, FKs de las columnas de grupos de `MovimientosValor` y backfill de grupos por defecto en los movimientos de valor existentes con el trigger desactivado SOLO dentro de la migración)
- Create: `src/OpenSource1.Application/Services/Contabilidad/IRegistroContable.cs`; Infrastructure `Services/Contabilidad/RegistroContable.cs`
- Modify: `RegistroMovimientosInventario.cs` (congelar grupos en `MovimientoValor`), guarda de borrado de cuentas (movimientos contables), `LibroInventarioSinUpdateNiDeleteTests` (proteger `MovimientosContables`, `RegistrosContables`)
- Create: consultas `GET api/contabilidad/movimientos?cuentaId=&desde=&hasta=&pagina=` y `GET api/contabilidad/registros?pagina=` (CanConsult) — vistas completas en la Fase 7
- Test: `RegistroContableTests` (Postgres), congelación de grupos, migración con datos

```csharp
public sealed record LineaAsiento(Guid CuentaContableId, decimal Importe, string? Descripcion,
    Guid? SocioNegocioId = null, Guid? ProductoId = null, Guid? GrupoNegocioId = null, Guid? GrupoProductoId = null,
    Guid? GrupoIvaNegocioId = null, Guid? GrupoIvaProductoId = null);
public sealed record AsientoContable(DateOnly FechaRegistro, DateOnly FechaDocumento, TipoDocumentoContable TipoDocumento,
    string? NumeroDocumento, string Descripcion, TipoOrigenMovimiento TipoOrigen, string ClaveOrigen,
    IReadOnlyList<LineaAsiento> Lineas);
public sealed record AsientoRegistrado(long RegistroContableId, string NumeroRegistro, long DesdeMovimiento, long HastaMovimiento);
public interface IRegistroContable
{
    /// Requiere transacción activa. No hace commit. Rechaza sin escribir: sin líneas, importe 0 en una línea, >4 decimales,
    /// cuenta inexistente/no Posteo/bloqueada, SUM(Importe) <> 0 (contabilidad.asiento_descuadrado con el descuadre).
    Task<Result<AsientoRegistrado>> RegistrarAsync(AsientoContable asiento, CancellationToken ct = default);
}
public enum TipoDocumentoContable : short { Ninguno = 0, CostoInventario = 1, FacturaVenta = 2, Cobro = 3 }
```

`Debito = max(Importe, 0)`, `Credito = max(-Importe, 0)`; `NumeroCuenta` congelado de la cuenta; la verificación del cuadre
se hace en memoria antes de escribir y además con `SELECT SUM` tras insertar (si no es 0 → excepción → rollback). La
`TipoOrigenMovimiento` existente se amplía si hace falta (`CostoInventario` = 4) — valores nuevos, nunca renumerar.

- [ ] Tests (Review Focus 1; cuentas inválidas; numeración CONTAB sin huecos y no consumida en fallo; grupos congelados en
  nuevos movimientos de valor; backfill de la migración) → implementar → suite → commit `feat: libro contable append-only con registro de asientos balanceados y grupos congelados en el libro de valor`.

---

## Task 5.6 — Batch `PostearCostoInventarioContabilidad`

**Files:**
- Create: `Features/Contabilidad/Commands/PostearCostoInventarioCommand.cs` + handler; Infrastructure `Services/Contabilidad/PosteoCostoInventario.cs`; endpoint `POST api/contabilidad/postear-costo-inventario` (CanModify) → 200 `{ asientos, movimientosValorContabilizados, pendientes: [{ movimientoValorId, codigo, mensaje }] }`
- Modify: migración `PermitirContabilizacionCosto` (función del trigger: permitir en `MovimientosValor` un UPDATE que cambie SOLO `ImporteCostoPosteadoContabilidad`); `LibroInventarioSinUpdateNiDeleteTests` (permitir exactamente `UPDATE "MovimientosValor" SET "ImporteCostoPosteadoContabilidad"` y seguir prohibiendo cualquier otro)
- Modify: `RegistroMovimientosInventario`/`MovimientoValor` si hace falta exponer el tipo de movimiento al batch (ya está en la fila)
- Create: página o botón en la página de Contabilidad para ejecutar el batch (ConfirmDialog, CanModify) y ver el resultado
- Test: `PosteoCostoInventarioTests` (Postgres)

Algoritmo: una transacción por "lote de movimientos" (p. ej. por producto, para que un fallo de setup de un producto no
bloquee a los demás): lock `pg_advisory_xact_lock(hashtextextended('contab-costo', 0))` global del batch (dos ejecuciones
en paralelo se serializan), `SELECT ... FOR UPDATE` de los `MovimientosValor` pendientes del grupo (índice parcial
existente), derivar cuentas por movimiento (desviaciones de la Task 5.1), construir UN asiento por producto con una línea
por movimiento y cuenta (agrupar por cuenta), `IRegistroContable.RegistrarAsync`, y `UPDATE ... SET
"ImporteCostoPosteadoContabilidad" = "ImporteCosto"` de esos movimientos. Si un movimiento no tiene setup: se excluye de ese
asiento y se informa en `pendientes`; el resto del producto se contabiliza. `TipoOrigen = CostoInventario`, `ClaveOrigen` =
número de registro. Fecha del asiento = `FechaRegistro` del movimiento (agrupar también por fecha: un asiento por producto y
fecha).

- [ ] Tests: apertura migrada + ajuste positivo + ajuste negativo + transferencia entre almacenes con misma cuenta (neteo) →
  asientos cuadrados, saldo de Inventario 1301 = valor del inventario; Review Focus 2 (producto con grupo sin setup: pendiente,
  el otro producto sí); Review Focus 3 (dos veces seguidas: segunda 0; en paralelo con barrera: nunca doble); tras
  `AjustarCostoMovimientos` (delta nuevo) la siguiente ejecución contabiliza solo el delta; el trigger sigue rechazando
  cualquier otro UPDATE en `MovimientosValor`. Mutaciones: quitar el lock global (paralelo falla), contabilizar
  `ImporteCosto` en vez del delta (idempotencia falla). → implementar → runtime del botón → suite → commit `feat: batch idempotente de contabilizacion del costo de inventario`.

---

## Task 5.7 — Cierre de la Fase 5

- [x] Cadena de migraciones desde vacía y desde el estado final de la Fase 4 con datos (productos, socios, movimientos);
  Down al final de la Fase 4 y vuelta a HEAD.
- [x] `ApplicationDbContextModelTests`, sonda vacía, `has-pending-model-changes`; `grep` TODO; suite completa.
- [x] Sección "Resultado y pendientes que hereda la Fase 6" al final de este plan.

---

## Resultado y pendientes que hereda la Fase 6

**Resultado.** La Fase 5 queda cerrada: plan de cuentas con semilla (Task 5.2), cinco grupos contables simples más el grupo
de cliente contable con clasificación en productos/socios y backfill a las semillas por defecto (Task 5.3), setups con
comodín y `IDerivadorCuentas` (regla fila exacta → comodín → error, Task 5.4), libro contable append-only con
`IRegistroContable` como único escritor y verificación de cuadre por trigger constraint diferido (Task 5.5), y el batch
idempotente `PostearCostoInventarioContabilidad` que contabiliza el delta de costo de todos los movimientos de valor
pendientes agrupando por producto/fecha (Task 5.6). Las cinco Review Focus de la fase tienen test dedicado y pasan.

La cadena de migraciones se verificó completa en ambos sentidos, con un contenedor Postgres 17 temporal
(`DOCKER_CONTEXT=default`, borrado al terminar):

1. **BD vacía → HEAD** (30 migraciones, `InitialApplicationDb` … `PermitirContabilizacionCosto`): aplica limpio.
2. **BD en el estado final de la Fase 4 con datos → HEAD**: migrado hasta `AddRegistrosDiario` (24 migraciones), sembrado
   por SQL un almacén, un producto, un socio y dos movimientos de valor (apertura `TipoOrigen = 99` de 100 unidades a
   costo 8.00 y un ajuste `TipoOrigen = 99` de −10, aplicado por FIFO contra la apertura), y luego `dotnet ef database
   update` a HEAD (6 migraciones de la Fase 5). Verificado por SQL tras la migración: 12 cuentas del plan y 10 filas de
   grupos semilla; el producto y el socio sembrados quedaron con `BIENES/ITBIS18/GENERAL` y `NACIONAL/ITBIS18/GENERAL`
   (backfill); los dos `MovimientosValor` sembrados quedaron con `GrupoInventarioId`/`GrupoProductoId` rellenados
   (`GrupoNegocioId` en null, correcto: no tienen socio) y el trigger `TR_MovimientosValor_AppendOnly` con
   `tgenabled = 'O'`; 9 filas de setups semilla (4 general + 4 IVA + 1 inventario); serie `CONTAB` sin huecos; los cinco
   triggers de los libros contables (`TR_RegistrosContables_*`, `TR_MovimientosContables_*`) activos, incluida
   `TR_MovimientosContables_Cuadre` como constraint trigger `DEFERRABLE INITIALLY DEFERRED`.
3. **Down a `AddRegistrosDiario`** (Down completo de la Fase 5: revierte sus 6 migraciones y quedan 24 aplicadas): las
   12 tablas nuevas de la Fase 5 (`CuentasContables`, los seis `Grupos*`, los tres `Setups*`, `RegistrosContables` y
   `MovimientosContables`) y las 6 columnas de grupo añadidas a `Productos` (3) y `SociosNegocio` (3) desaparecen, y los
   datos de la Fase 4 (almacén, producto, socio, los dos movimientos de valor) se conservan intactos.
4. **Up de nuevo a HEAD**: mismo resultado exacto que el paso 2 — 30 migraciones, mismos conteos de cuentas/grupos/setups,
   mismo backfill del producto y del socio sembrados, los mismos `MovimientosValor` con sus grupos rellenados otra vez, y
   los mismos triggers activos. Sin duplicados: los `HasData` y el backfill por SQL son idempotentes frente a un
   Down+Up completo.

Comandos usados (contenedor `postgres:17-alpine` propio, puerto 65442, borrado al final):

```
dotnet ef database update -p src/OpenSource1.Infrastructure -s src/OpenSource1.Api --context ApplicationDbContext \
  --connection "Host=localhost;Port=65442;Database=AxionERP_App;Username=postgres;Password=..."
dotnet ef database update 20260925220000_AddRegistrosDiario -p src/OpenSource1.Infrastructure -s src/OpenSource1.Api \
  --context ApplicationDbContext --connection "..."
```

Resto de la verificación: `dotnet build test.slnx --no-incremental` (0 errores, 6 avisos CS0618 preexistentes, ninguno
nuevo); `ApplicationDbContextModelTests` en verde; sonda `ProbeFase57` con `Up()`/`Down()` vacíos y el repositorio sin
cambios (borrada); `dotnet ef migrations has-pending-model-changes` → "No changes have been made to the model since the
last migration."; `grep -rn "\bTODO\b"` sobre `src/`/`tests/` sin marcadores reales (solo apariciones de la palabra
española "todo/todos"). Suite completa (`DOCKER_CONTEXT=default dotnet test test.slnx`): **1052/1052** (sin cambios desde
el cierre de la Task 5.6, porque esta task es de verificación y documentación, no añade tests), ~13 min.

**Requisito de infraestructura: PostgreSQL 15+.** El derivador de cuentas (Task 5.4) depende de índices únicos con
`NULLS NOT DISTINCT` (p. ej. `IX_SetupsInventario_AlmacenId_GrupoInventarioId`) para que dos filas de comodín (`NULL`) en
el mismo eje no puedan duplicarse; esa sintaxis solo existe desde PostgreSQL 15. El contenedor de todas las pruebas de la
fase (incluida esta verificación) usó `postgres:17-alpine`. Cualquier entorno de despliegue de la Fase 6 en adelante debe
correr PostgreSQL 15 o superior.

**Limitaciones conocidas que hereda la Fase 6:**

- **El Down de `AddLibroContable` es destructivo y no rearma el batch.** Borra el libro contable (`RegistrosContables`,
  `MovimientosContables`), pero `MovimientosValor.ImporteCostoPosteadoContabilidad` conserva lo ya contabilizado, así que
  tras un Down+Up el batch de costo NO recontabiliza nada de lo anterior (solo ve `ImporteCosto <>
  ImporteCostoPosteadoContabilidad`). Para reconstruir el libro hay que poner esa columna a 0 a mano, con el trigger
  append-only de `MovimientosValor` desactivado durante el cambio (mientras `PermitirContabilizacionCosto` no esté aplicada,
  `libro_inventario_append_only()` rechaza ese UPDATE; en HEAD lo admite), y volver a ejecutar el batch. Está comentado en
  el propio `Down` de la migración.
- **El borrado de socios de negocio no tiene guarda de uso.** `DeleteSocioNegocioCommandHandler` hace el borrado lógico
  sin consultar referencias. Hoy no importa (el batch de costo no rellena `MovimientosContables.SocioNegocioId`), pero la
  Fase 6 empezará a usar esa columna al facturar: debe añadir la guarda (409 `socio_negocio.conflicto` si hay movimientos
  contables, documentos o borradores que lo referencian).
- **`SetupsContableGeneral.CuentaAjusteInventarioId` no la usa nadie todavía.** El batch de costo toma la contrapartida de
  compras/ajustes/apertura de `SetupsInventario.CuentaAjusteInventarioId` (vía `CuentaAjusteInventarioAsync(almacén,
  grupo de inventario)`); la columna homónima del setup general solo existe y está protegida por la guarda de uso de
  cuentas. Si la Fase 6 la necesita (p. ej. un ajuste por grupo de negocio × producto), debe añadir su método al
  derivador; si no, puede quedarse como está.

- **Sin posteo contable al facturar en esta fase.** `ConfiguracionInventario.PosteoAutomaticoCosto` no existe todavía; el
  único camino para contabilizar el costo de inventario es el batch manual `POST
  api/contabilidad/postear-costo-inventario` (Task 5.6). La Fase 6 factura sin generar ningún asiento de costo por sí
  misma — el spec (sección 6.5, paso 7) ya condiciona las patas de costo/inventario del asiento de venta a
  `PosteoAutomaticoCosto`, que sigue sin implementarse.
- **`ClaveOrigen` del batch de costo no es el número de registro contable, es la clave del grupo.** `IRegistroContable.
  RegistrarAsync` asigna el número de la serie `CONTAB` DENTRO del registro y exige `ClaveOrigen` como parámetro de
  entrada, así que el batch no puede conocer ese número de antemano. Usa `ClaveOrigen = COSTO-aaaammdd-<ProductoId:N>`
  (producto × fecha), determinista y suficiente para trazabilidad; el número real vive en
  `RegistrosContables.NumeroRegistro`. Si la Fase 6 necesita que `ClaveOrigen` sea literalmente el número del documento
  que originó el asiento (p. ej. el número de la factura posteada), puede seguir ese mismo patrón sin tocar el contrato
  de `IRegistroContable` — el motor de posteo de facturas SÍ conoce su número antes de registrar el asiento (lo reserva
  en el paso 2 del spec, antes del paso 7).
- **El batch aborta la ejecución completa (500) ante una excepción inesperada dentro de un grupo.**
  `PosteoCostoInventario.PostearAsync` no envuelve el `foreach` de grupos en un `try/catch`: si un grupo lanza algo que
  no es un `Result` fallido controlado (p. ej. un error de conexión a mitad de un `SELECT ... FOR UPDATE`), la excepción
  sube sin capturar, el `ContabilidadController` no la traduce y el endpoint responde 500. Los grupos ya procesados y
  comprometidos (`COMMIT`) antes del que falló quedan contabilizados correctamente porque cada grupo es su propia
  transacción; los que faltan (incluido el que lanzó la excepción) simplemente no se procesaron. Es seguro volver a
  ejecutar el batch completo: la idempotencia por `ImporteCosto <> ImporteCostoPosteadoContabilidad` hace que solo se
  reintenten los grupos pendientes. La Fase 6 debería decidir si esto es aceptable para un batch que se dispare
  automáticamente (p. ej. un job programado) o si conviene capturar por grupo y devolver el fallo como otro
  `pendiente` — hoy solo los errores de derivación (`Result` fallido) se reportan así; cualquier otra excepción no.
- **N+1 del derivador de cuentas dentro del batch.** `PosteoCostoInventario` llama a `IDerivadorCuentas` (dos consultas:
  `CuentaInventarioAsync` + `CuentaCostoVentasAsync`/`CuentaAjusteInventarioAsync`) una vez POR MOVIMIENTO de valor
  pendiente, dentro de un `foreach`, sin cachear por combinación de ejes ya resuelta dentro del mismo grupo/ejecución.
  Aceptado mientras el volumen de movimientos pendientes por ejecución sea moderado (como en Task 3.6/4.3 con patrones
  similares); si la Fase 6 factura a volumen y el batch empieza a tardar, conviene memoizar el resultado del derivador
  por `(eje1, eje2)` dentro de la misma ejecución antes de escalar el batch.
- **Guardas de uso "check-then-act" en grupos contables y almacenes (patrón aceptado, no nuevo de esta fase).**
  `IGrupoContableUsoService.EstaEnUsoAsync` (Task 5.3) y la guarda de `DeleteAlmacenCommandHandler` contra
  `SetupsInventario` (Task 5.4) son una consulta SIN bloqueo antes de borrar: un alta o un setup concurrente que asigne
  ese grupo/almacén justo después de la consulta y antes del borrado lógico puede dejar una referencia "viva" a un
  grupo/almacén ya borrado. La FK es `Restrict` pero el borrado es LÓGICO, así que no lo impide. Es el mismo patrón ya
  aceptado en categorías/unidades de medida de productos (Fase 3) y en el borrado de lote/línea de diario (Fase 4,
  corregido allí con un advisory lock porque afectaba directamente al libro; aquí no se corrigió porque el peor caso es
  un `setup_contable.inexistente`/`grupo_faltante` explícito al derivar, nunca un asiento a medias). La Fase 6 hereda el
  mismo patrón para sus propios maestros (términos de pago, grupos de cliente ya cubiertos) y debe decidir caso a caso si
  alguno de sus flujos (p. ej. bloquear un socio para facturación) necesita el lock en vez de la consulta simple.

**Lo que la Fase 6 debe reutilizar, no reinventar:**

- **Registrar asientos con `IRegistroContable.RegistrarAsync`** (único escritor del libro contable), con
  `TipoDocumentoContable.FacturaVenta` para el asiento de la factura y `TipoDocumentoContable.Cobro` para el del pago
  (spec 6.5 paso 7 y 6.6), y `TipoOrigenMovimiento.FacturaVenta` como origen de esos movimientos (el enum ya reserva ese
  valor = 2; `CostoInventario` = 4 es del batch de esta fase, no se reutiliza para facturación).
- **Derivar cuentas con `IDerivadorCuentas`**: `CuentaCxCAsync` (grupo de cliente contable, obligatorio), `CuentaVentasAsync`
  y `CuentaDescuentoVentasAsync` (grupo de negocio × grupo de producto; el comodín solo se admite en el eje de NEGOCIO:
  un grupo de producto nulo da `setup_contable.grupo_faltante`, nunca casa un comodín) e `IvaAsync` (grupo de IVA de
  negocio × grupo de IVA de producto, con el comodín igualmente solo en el eje de negocio; además del identificador y
  porcentaje de IVA congelados). La regla de resolución (fila exacta → comodín → `setup_contable.inexistente`) y los códigos de error
  (`setup_contable.grupo_faltante`, `.inexistente`, `.cuenta_invalida`) son los mismos que ya usa el batch de costo; no
  hace falta un derivador nuevo.
- Los grupos que la Fase 6 necesita congelar en el borrador y en el documento posteado
  (`GrupoNegocioId`/`GrupoIvaNegocioId`/`GrupoClienteContableId` del socio; `GrupoProductoId`/`GrupoIvaProductoId`/
  `GrupoInventarioId` de cada línea de producto) ya existen en `Producto`/`SocioNegocio` desde esta fase (Task 5.3), con
  backfill a las semillas por defecto para los maestros existentes.
