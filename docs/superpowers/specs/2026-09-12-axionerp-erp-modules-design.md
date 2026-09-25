# Diseño: módulos ERP de AxionERP (inventario, facturación, contabilidad)

**Fecha:** 2026-09-12
**Estado:** aprobado para implementación
**Investigación de referencia:** [`docs/research/erp-reference-data-model.md`](../../research/erp-reference-data-model.md)

## Contexto

AxionERP (`OpenSource1`) es una solución .NET 10 con arquitectura Onion, PostgreSQL,
EF Core para escritura y Dapper para lectura, Blazor Static SSR y API JWT.

Hoy tiene 4 entidades de negocio anémicas (`Cliente`, `Producto`, `Entrada`, `AppSetting`),
sin relaciones ni foreign keys, sin transacciones multi-agregado, sin concurrencia
optimista, sin paginación y con el stock como columna `integer` mutable.

Este documento diseña la evolución hacia módulos ERP de inventario, facturación y
contabilidad, tomando como referencia la estructura de datos de Dynamics 365 Business
Central y, en puntos concretos, de SAP.

## Decisiones de arquitectura

Estas nueve decisiones gobiernan todas las fases. Cada una tiene su justificación en
el informe de investigación.

| # | Decisión | Fundamento |
|---|---|---|
| D1 | El stock y todo saldo son **derivados**, nunca columnas almacenadas | En BC `Item.Inventory`, `Customer.Balance`, `G/L Account.Balance` y `Remaining Amount` son FlowFields (`SUM`). Un saldo almacenado puede desincronizarse de sus movimientos; uno derivado no |
| D2 | **Cantidad y valor en tablas separadas** (1 movimiento de producto : N movimientos de valor) | La cantidad es definitiva al postear; el costo no lo es. BC separa `Item Ledger Entry` de `Value Entry` por esta razón |
| D3 | Los libros son **append-only**: sin `UPDATE`, sin `DELETE` | `Value Entry` declara permisos `ri` (lectura + inserción). El costo se corrige agregando filas. Una anulación es un movimiento contrario |
| D4 | Las cuentas contables **se derivan al postear**, no se capturan en el documento | El documento guarda clasificadores (posting groups); las tablas de intersección contienen las cuentas |
| D5 | Toda fila de libro lleva `(TipoOrigen, ClaveOrigen)` | El par AWTYP/AWKEY de SAP. Dos columnas, gratis ahora, doloroso de retrofitear |
| D6 | **Borrador y posteado son tablas distintas**; lo posteado es inmutable y **sin discriminador de tipo** | BC copia a `Sales Invoice Header` cuya PK es `No.` a secas, sin `Document Type` |
| D7 | "Abierta/cerrada" **se deriva**, no se persiste en tablas separadas | SAP usó BSID/BSAD/BSIK/BSAK y en S/4HANA los eliminó: ahora son vistas |
| D8 | Las líneas **congelan** factor de UdM, `% IVA`, grupos contables y costo unitario | Un `JOIN` al maestro actual reescribiría la historia |
| D9 | Series de numeración **separadas** para borrador (con huecos) y posteado (sin huecos) | Gapless implica bloqueo de fila hasta el commit. No hay truco; solo se limita el alcance del bloqueo |

### Convenciones transversales

- **Precisión decimal:** cantidades y factores de conversión `numeric(18,6)`; importes,
  costos y precios `numeric(18,4)`; porcentajes `numeric(9,5)`. Los importes que llegan
  a un documento legal se redondean a 2 decimales al congelarse.
- **PK de libros:** `bigint` con identidad secuencial, no `uuid`. Los libros crecen sin
  límite y un UUIDv4 aleatorio degrada la localidad de inserción en índices B-tree.
  Los maestros mantienen `uuid` por compatibilidad con lo existente.
- **Idioma:** entidades, tablas y columnas en español, siguiendo la convención actual
  del proyecto (`Clientes`, `Productos`, `CreatedAtUtc`).
- **Soft delete** aplica solo a maestros. Los libros nunca se borran (D3).
- **Concurrencia optimista** aplica solo a maestros y documentos borrador. Los libros son
  append-only, no hay nada que colisione.

### Limitaciones aceptadas explícitamente

Quedan fuera de alcance, y son todas aditivas (no obligan a reescribir lo construido):

- Método de costeo: **solo promedio ponderado**. Sin FIFO, LIFO, estándar ni específico.
- Periodo de cálculo del promedio: **día**. Sin `Average Cost Period` configurable.
- Sin ubicaciones/bins dentro del almacén.
- Sin multimoneda: moneda única (`DOP`), campo presente pero sin tasas de cambio.
- Sin revaluación de inventario.
- Sin libros generales de ventas ni de compras (solo emisión de facturación).
- Sin reglas de determinación de cuentas con comodines ni validez por fechas (se usa el
  comodín `NULL` en las tablas de setup, sin prioridad derivada).
- Sin `DocumentLink` estilo VBFA.
- Sin localización fiscal dominicana (NCF, formatos 606/607, etc.). El campo de documento
  fiscal existe, pero no hay validación de NCF ni reportes fiscales.
- Foreign key real a la tabla de usuarios: **imposible** sin consolidar las dos bases de
  datos (`AxionERP_App` / `AxionERP_Identity`). Se añade `UsuarioId uuid` como referencia
  lógica sin FK, documentada como tal.

---

## Fase 0 — Saneamiento urgente

No es diseño; es sangrado activo que hay que detener antes de tocar el modelo.

| Tarea | Evidencia |
|---|---|
| Rotar la contraseña de PostgreSQL y la clave de firma JWT; mover a User Secrets | `src/OpenSource1.Api/appsettings.json:3-4,9` trackeado en git; `UserSecretsId` existe y no se usa |
| Añadir `appsettings.json` al `.gitignore` y dejar `appsettings.Example.json` versionado | `.gitignore:17-19` cubre `.env` y `secrets.json`, no `appsettings.json` |
| Subir `Microsoft.AspNetCore.OpenApi` para resolver GHSA-v5pm-xwqc-g5wc | Warning NU1903 (severidad alta) en el build |
| Poner `RecreateOnPendingModelChangesInDevelopment: false` | `appsettings.json:22` + `DatabaseMigrationExecutor.cs:25-32` ejecutan `EnsureDeletedAsync()`: borra la base entera |
| Retirar `AppSettings` y `Entradas` | Marcados `[Obsolete]`, 6 warnings CS0618, con controllers, tablas y tests vivos |
| Corregir el `Location` de creación de usuario | `UsersController.cs:46` pasa el email donde `GetById` espera un Id |
| Cerrar el fallback permisivo de CORS | `Program.cs:57-62` cae en `AllowAnyOrigin()` si la sección está vacía |

**Verificación:** `dotnet build test.slnx` sin el warning NU1903 y sin CS0618.

---

## Fase 1 — Kernel transversal

Los seis bloqueantes del diagnóstico. Nada de ERP se escribe hasta que esto esté en pie.

### 1.1 Conexión compartida EF + Dapper

**El problema.** `NpgsqlConnectionFactory.cs:15` abre una conexión independiente de la del
`DbContext`. Una lectura Dapper dentro de un flujo de escritura no ve la transacción
abierta por EF. Eso hace imposible "leer existencia → validar → descontar" de forma
consistente, que es la operación central de todo el módulo de inventario.

**La solución.** No basta con añadir `BeginTransaction` al `UnitOfWork`: ambos deben
compartir la misma conexión física.

```csharp
// OpenSource1.Application/Data/IDbSession.cs
public interface IDbSession
{
    DbConnection Connection { get; }              // misma instancia para EF y Dapper
    DbTransaction? CurrentTransaction { get; }
    Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken ct);
    Task CommitAsync(CancellationToken ct);
    Task RollbackAsync(CancellationToken ct);
}
```

`DbSession` se registra **scoped** y crea una `NpgsqlConnection` sin abrir. El
`ApplicationDbContext` se construye con `UseNpgsql(session.Connection)`, de modo que EF
abre y usa esa misma instancia. Los repositorios Dapper piden `session.Connection` y
pasan `transaction: session.CurrentTransaction` en cada llamada.

Cuando hay transacción activa, `dbContext.Database.UseTransaction(session.CurrentTransaction)`
la enrola. Resultado: EF y Dapper comparten conexión y transacción.

**Cambios adicionales:** las 8 llamadas Dapper pasan de `using` a `await using`
(`NpgsqlConnection` implementa `IAsyncDisposable`), y la disposición de la conexión pasa a
ser responsabilidad del scope, no de cada método.

### 1.2 Pipeline behaviors de MediatR

Hoy `Services/DependencyInjection.cs:9-10` solo registra handlers. Cero behaviors.

```
ILoggingBehavior      → traza comando + duración
IValidationBehavior   → FluentValidation, devuelve Result fallido (no excepción)
ITransactionBehavior  → envuelve solo ICommand (no IQuery) en transacción
```

Se introducen los marcadores `ICommand<T>` e `IQuery<T>` (ambos heredan de `IRequest<T>`)
para que el behavior transaccional sepa a qué aplicarse.

### 1.3 Convención de error única: `Result` / `Result<T>`

Hoy coexisten cuatro convenciones incompatibles: `null` sentinel
(`UpdateClienteCommandHandler.cs:16`), `bool` sentinel
(`DeleteClienteCommandHandler.cs:14`), tuplas `(bool, IReadOnlyList<string>)`
(`UserAdminService.cs:27`) y excepciones sin capturar desde los factories de VO.

```csharp
public readonly record struct Error(string Codigo, string Mensaje, string? Campo = null);

public class Result
{
    public bool EsExito { get; }
    public bool EsFallo => !EsExito;
    public IReadOnlyList<Error> Errores { get; }
    public static Result Exito();
    public static Result Fallo(params Error[] errores);   // lanza si no hay errores
}

public sealed class Result<T> : Result
{
    /// Lanza InvalidOperationException si el resultado es fallido.
    public T Valor { get; }
    public bool TryObtenerValor([MaybeNullWhen(false)] out T valor);

    public static Result<T> Exito(T valor);
    public static new Result<T> Fallo(params Error[] errores);
    public static Result<T> Fallo(Result origen);          // propaga errores
}
```

**Por qué `Valor` lanza en vez de devolver `T?`.** `T?` sobre un `T` sin restricción **no**
se traduce a `Nullable<T>` cuando `T` es un tipo valor. Con la firma ingenua,
`Result<int>.Fallo(...).Valor` devuelve `0`, exactamente igual que
`Result<int>.Exito(0).Valor`, y `Valor is null` es `false` en ambos. Un handler que lea
`.Valor` sin comprobar `.EsExito` obtendría un cero plausible y silencioso.

Con `Result<decimal>` de costos, `Result<int>` de cantidades y `Result<Guid>` de
identificadores atravesando inventario, facturación y contabilidad, ese cero silencioso es
un descuadre contable. Fallar ruidosamente es la opción correcta; `TryObtenerValor` cubre
el camino en que el fallo es esperado.

Los errores llevan código estable (`producto.no_encontrado`, `setup_iva.inexistente`)
para que la API los traduzca a HTTP y la UI a mensajes.

Un `ResultToActionResult` en la API mapea: sin errores → 200/201/204; `*.no_encontrado`
→ 404; `*.conflicto` → 409; `*.bloqueado` → 422; resto → 400 con `ProblemDetails` y
`errors` por campo.

### 1.4 Manejo global de errores

`Program.cs:68` registra `AddProblemDetails()` pero el pipeline (`:102-128`) **nunca llama
a `UseExceptionHandler()`**, así que las excepciones no manejadas salen como 500 sin
cuerpo. Se añade `app.UseExceptionHandler()` y un `IExceptionHandler` que registra y
devuelve `ProblemDetails`.

### 1.5 Concurrencia optimista

PostgreSQL expone `xmin` como token de versión de fila sin columna adicional:

```csharp
modelBuilder.Entity<T>().Property<uint>("xmin").IsRowVersion().HasColumnName("xmin");
```

Se aplica a todos los maestros y documentos borrador. Un `DbUpdateConcurrencyException`
se traduce a `Error("entidad.modificada_por_otro", ...)` → HTTP 409.

### 1.6 Soft delete

`IsDeleted bool NOT NULL DEFAULT false`, `DeletedAtUtc`, `DeletedBy` en maestros, con
filtro global de consulta en EF y cláusula explícita en los repositorios Dapper. Los
libros quedan excluidos (D3).

### 1.7 Contrato de paginación

Hoy no hay ni un `LIMIT` en el repositorio, y los contratos
`IClienteReadRepository`/`IProductoReadRepository` **no pueden expresar paginación**.

```csharp
public sealed record PageRequest(int Pagina = 1, int TamanoPagina = 50,
                                 string? OrdenarPor = null, bool Descendente = true)
{
    public const int TamanoMaximo = 200;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Pagina,
                                    int TamanoPagina, long Total);
```

Todos los listados existentes y nuevos pasan a devolver `PagedResult<T>`. Esto atraviesa
Application, API y Blazor: es un cambio de contrato, y por eso se hace ahora con 4 tablas
en lugar de después con líneas de factura.

### 1.8 Allow-list de columnas en SQL

`FilterExpressionBuilder.cs:28,62` interpola el **nombre de columna** en el SQL sin
validar. Los valores sí van parametrizados, así que hoy no es explotable (todos los
llamantes pasan literales), pero es el sink exacto que se activa en cuanto un grid ERP
acepte `?sortBy=`.

```csharp
public sealed class ColumnasPermitidas
{
    private readonly FrozenSet<string> _columnas;
    public bool EsValida(string columna);
    public string Citar(string columna);   // lanza si no está en la lista
}
```

Cada repositorio Dapper declara su allow-list. El `OrdenarPor` del `PageRequest` se
valida contra ella; si no coincide, se usa el orden por defecto.

### 1.9 Bug de filtros exactos

`FilterExpressionBuilder.cs:55-75`: si el término no parsea se descarta en silencio, y si
todos fallan no se añade cláusula alguna. Resultado: `?precio=abc` **devuelve la tabla
completa**. Pasa a devolver `Result` fallido con `filtro.valor_invalido` → HTTP 400.

### 1.10 `ValueObject.Equals` no compara tipos

`Abstractions/ValueObject.cs:7-8` compara solo los componentes de igualdad, así que dos
VOs distintos de un único string se consideran iguales. Irrelevante hoy; trampa segura
con `NumeroCuenta`, `CodigoGrupo` y `CodigoAlmacen`.

```csharp
public override bool Equals(object? obj) =>
    obj is ValueObject other
    && other.GetType() == GetType()
    && GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
```

Se añade también `operator ==` / `operator !=`.

### 1.11 Estrategia de índices

Solo hay 2 índices en toda la base (`AppSettings.Key`, `Productos.Codigo`), y **todos**
los listados ordenan por `CreatedAtUtc` sin índice.

- Índice en `CreatedAtUtc DESC` en cada tabla listada
- Único en el documento fiscal del socio de negocio (parcial, `WHERE ... IS NOT NULL`)
- Extensión `pg_trgm` + índices GIN en las columnas de texto buscadas con `ILIKE '%...%'`
  (`FilterExpressionBuilder.cs:27` usa wildcard inicial: ningún B-tree sirve)
- Los índices de los libros se definen en sus fases respectivas

### 1.12 Permisos por recurso

Las 4 políticas actuales (`Identity/DependencyInjection.cs:70-80`) son globales:
`CanDelete` es una sola política para todo el sistema, así que no se puede expresar
"anula facturas pero no borra almacenes".

Se introduce un catálogo de permisos `<modulo>.<recurso>.<accion>`
(`inventario.diario.postear`, `facturacion.factura.emitir`,
`contabilidad.cuenta.modificar`), emitidos como claims en el JWT y evaluados por una
`PermissionPolicyProvider`. Los roles existentes se mapean a conjuntos de permisos, y las
4 políticas actuales se conservan como alias para no romper los controllers vigentes.

**Verificación de la Fase 1:** build verde; tests unitarios nuevos de `Result`,
`ColumnasPermitidas`, `ValueObject.Equals` y del comportamiento transaccional
(escritura + lectura Dapper dentro de la misma transacción, con rollback verificable).

---

## Fase 2 — Dominio maestro

### 2.1 `Cliente` → `SocioDeNegocio`

`Cliente` tiene hoy `Nombre`, `Apellido`, `Email`: una persona, no una entidad
facturable. Se convierte en socio de negocio único (cliente y proveedor en una tabla,
como `OCRD.CardType` de SAP Business One). Es la decisión que **cambia todas las FKs**, y
por eso se toma ahora.

Tabla `SociosNegocio`:

| Columna | Tipo | Notas |
|---|---|---|
| `Id` | `uuid` PK | |
| `Codigo` | `varchar(20)` | UNIQUE, de serie numerada |
| `Tipo` | `smallint` | Cliente=1, Proveedor=2, Ambos=3 |
| `NombreComercial` | `varchar(200)` | NOT NULL |
| `RazonSocial` | `varchar(200)` | |
| `TipoDocumentoFiscal` | `smallint` | RNC=1, Cedula=2, Pasaporte=3, SinDocumento=9 |
| `NumeroDocumentoFiscal` | `varchar(20)` | UNIQUE parcial `WHERE IS NOT NULL` |
| `Email` | `varchar(256)` | UNIQUE parcial |
| `Telefono` | `varchar(50)` | |
| `DireccionLinea1/2` | `varchar(300)` | VO `DireccionFiscal` aplanado |
| `Ciudad` | `varchar(100)` | |
| `PaisCodigo` | `varchar(2)` | |
| `Sector` | `varchar(100)` | |
| `TerminoPagoId` | `uuid` | FK → `TerminosPago` |
| `LimiteCredito` | `numeric(18,4)` | 0 = sin límite |
| `GrupoNegocioId` | `uuid` | FK, Fase 5 |
| `GrupoIvaNegocioId` | `uuid` | FK, Fase 5 |
| `GrupoClienteContableId` | `uuid` | FK, Fase 5 |
| `Bloqueado` | `smallint` | No=0, Facturacion=1, Todo=2 |
| `ImagePath` | `varchar(500)` | |
| auditoría + `xmin` + soft delete | | |

El saldo del socio **no se almacena** (D1): se deriva de `MovimientosCliente`.

**Migración de datos:** los `Clientes` existentes se migran con
`NombreComercial = Nombre + ' ' + Apellido`, `Tipo = Cliente`,
`TipoDocumentoFiscal = SinDocumento`, y `Codigo` generado secuencialmente.

Tabla `TerminosPago`: `Id`, `Codigo` UNIQUE, `Descripcion`, `DiasVencimiento int`,
`DiasDescuento int`, `PorcentajeDescuento numeric(9,5)`.

### 2.2 Unidades de medida con conversión

Hoy `UnidadMedida` es un `IReadOnlyDictionary` estático hardcodeado en Core
(`ValueObjects/UnidadMedida.cs:8-19`), sin tabla y sin conversión. Es incompatible con lo
requerido.

Tabla `UnidadesMedida` (catálogo global): `Id uuid`, `Codigo varchar(10)` UNIQUE,
`Nombre varchar(50)`, `Decimales smallint` (redondeo de cantidades).

Tabla `UnidadesMedidaProducto` (equivalente a `Item Unit of Measure` 5404):

| Columna | Tipo | Notas |
|---|---|---|
| `Id` | `uuid` PK | |
| `ProductoId` | `uuid` | FK |
| `UnidadMedidaId` | `uuid` | FK |
| `CantidadPorUnidadMedida` | `numeric(18,6)` | NOT NULL, > 0 |

UNIQUE(`ProductoId`, `UnidadMedidaId`). La unidad base del producto tiene factor 1 y su
fila es obligatoria.

**Conversión:** `cantidadBase = cantidad * CantidadPorUnidadMedida`. El factor se
**congela** en cada línea de documento y de libro (D8).

El VO `UnidadMedida` estático se elimina; las validaciones pasan a resolverse contra la
tabla. El VO `Pais` se conserva tal cual (los países no necesitan administración).

### 2.3 `Producto`

| Cambio | Detalle |
|---|---|
| `Stock integer` | **Se elimina.** Pasa a derivarse en Fase 3 (D1) |
| `Precio numeric(18,2)` | → `PrecioVenta numeric(18,4)` |
| `UnidadMedidaBaseId uuid` | FK → `UnidadesMedida`, NOT NULL |
| `MetodoCosteo smallint` | Promedio=1 (único implementado) |
| `CostoUnitario numeric(18,4)` | **Proyección mantenida**, no autoritativa: la recalcula la rutina de ajuste de costo. El valor autoritativo siempre se deriva de `MovimientosValor` |
| `CostoEstandar numeric(18,4)` | Informativo |
| `CostoAjustado bool` | La doc confirma que `Cost is Adjusted` vive en `Item`, no en el movimiento |
| `CategoriaId uuid` | FK → `CategoriasProducto` (hoy es texto libre sin catálogo) |
| `GrupoProductoId`, `GrupoIvaProductoId`, `GrupoInventarioId` | `uuid` FK, se pueblan en Fase 5 |
| `Bloqueado smallint` | No=0, Venta=1, Todo=2 |
| auditoría + `xmin` + soft delete | |

Tabla `CategoriasProducto`: `Id`, `Codigo varchar(30)` UNIQUE, `Nombre varchar(100)`,
`CategoriaPadreId uuid` nullable (jerarquía).

### 2.4 Numeración de documentos

Lo necesitan las Fases 4 y 6. Gapless implica bloqueo de fila: no hay alternativa (D9).

Tabla `Series`: `Id`, `Codigo varchar(20)` UNIQUE, `Descripcion`,
`PermiteHuecos bool`, `PorDefecto bool`.

Tabla `LineasSerie`: `Id`, `SerieId` FK, `NumeroInicial varchar(20)`,
`NumeroFinal varchar(20)`, `UltimoNumeroUsado varchar(20)`, `FechaInicial date`,
`Incremento int` default 1, `Bloqueada bool`.

```csharp
public interface IGeneradorNumeroDocumento
{
    Task<Result<string>> SiguienteAsync(string codigoSerie, DateOnly fecha,
                                        CancellationToken ct);
}
```

Con `PermiteHuecos = false` la implementación hace `SELECT ... FOR UPDATE` sobre la
`LineasSerie` aplicable, de modo que el número se reserva hasta el commit. Con
`PermiteHuecos = true` no bloquea. Series de borrador con huecos, series de posteado sin
huecos.

**Verificación de la Fase 2:** build verde; migración aplicada sin pérdida de datos de
`Clientes`/`Productos`; tests de conversión de unidades, de generación de números
concurrente, y de la migración de `Cliente` → `SocioDeNegocio`.

---

## Fase 3 — Inventario: almacenes y libro de movimientos

El cimiento. Todo lo demás (diarios, facturación, costos) produce o lee este libro.

### 3.1 `Almacenes`

`Id uuid` PK, `Codigo varchar(10)` UNIQUE, `Nombre varchar(100)`,
`DireccionLinea1/2 varchar(300)`, `Ciudad varchar(100)`, `PaisCodigo varchar(2)`,
`Bloqueado bool`, `EsPredeterminado bool`, auditoría + `xmin` + soft delete.

Sin ubicaciones ni bins: es el nivel mínimo viable.

### 3.2 `MovimientosProducto` (append-only)

Equivalente a `Item Ledger Entry` (32). Registra **cantidad**, nunca valor (D2).

| Columna | Tipo | Notas |
|---|---|---|
| `Id` | `bigint` PK identidad | secuencial, no uuid |
| `ProductoId` | `uuid` | FK, NOT NULL |
| `AlmacenId` | `uuid` | FK, NOT NULL |
| `TipoMovimiento` | `smallint` | Compra=1, Venta=2, AjustePositivo=3, AjusteNegativo=4, Transferencia=5 |
| `TipoDocumento` | `smallint` | |
| `NumeroDocumento` | `varchar(20)` | |
| `NumeroLineaDocumento` | `int` | |
| `FechaRegistro` | `date` | NOT NULL — la fecha contable |
| `FechaDocumento` | `date` | la fecha del papel |
| `Cantidad` | `numeric(18,6)` | NOT NULL, **con signo**: + entrada, − salida |
| `CantidadRestante` | `numeric(18,6)` | solo en entradas positivas; la consumen las aplicaciones |
| `CantidadFacturada` | `numeric(18,6)` | |
| `UnidadMedidaId` | `uuid` | FK |
| `CantidadPorUnidadMedida` | `numeric(18,6)` | **congelado** (D8) |
| `SocioNegocioId` | `uuid` | nullable, para ventas/compras |
| `TipoOrigen` | `smallint` | D5 — Diario=1, FacturaVenta=2, ... |
| `ClaveOrigen` | `varchar(50)` | D5 |
| `CreatedAtUtc`, `CreatedBy`, `UsuarioId` | | sin `UpdatedAt`: append-only |

**Sin columna `Abierto`** (D7): una entrada está abierta si `CantidadRestante > 0`.

Índices:
- `(ProductoId, AlmacenId, FechaRegistro)` — consulta de existencia
- `(ProductoId, FechaRegistro) WHERE CantidadRestante > 0` — parcial, para el costeo
- `(TipoDocumento, NumeroDocumento)` — trazabilidad de documento
- `(TipoOrigen, ClaveOrigen)` — trazabilidad de origen

**Existencia** (derivada, D1):

```sql
SELECT COALESCE(SUM("Cantidad"), 0)
FROM "MovimientosProducto"
WHERE "ProductoId" = @productoId
  AND (@almacenId IS NULL OR "AlmacenId" = @almacenId)
  AND "FechaRegistro" <= @fecha;
```

### 3.3 `MovimientosValor` (append-only)

Equivalente a `Value Entry` (5802). Relación 1:N desde `MovimientosProducto` (D2).

| Columna | Tipo | Notas |
|---|---|---|
| `Id` | `bigint` PK identidad | |
| `MovimientoProductoId` | `bigint` | FK, **nullable** (hay valores sin cantidad) |
| `ProductoId`, `AlmacenId` | `uuid` | desnormalizados para reporte |
| `TipoValor` | `smallint` | CostoDirecto=1, Redondeo=3 |
| `TipoMovimiento` | `smallint` | copiado del movimiento de producto |
| `FechaRegistro` | `date` | NOT NULL |
| `CantidadValorada` | `numeric(18,6)` | |
| `CantidadFacturada` | `numeric(18,6)` | |
| `ImporteCosto` | `numeric(18,4)` | NOT NULL, con signo |
| `CostoPorUnidad` | `numeric(18,4)` | |
| `ImporteVenta` | `numeric(18,4)` | solo en ventas |
| `ImporteCostoPosteadoContabilidad` | `numeric(18,4)` | default 0 — lo usa el batch idempotente de Fase 5 |
| `Ajuste` | `bool` | marca las filas creadas por la rutina de ajuste |
| `TipoDocumento`, `NumeroDocumento`, `NumeroLineaDocumento` | | |
| `GrupoInventarioId`, `GrupoNegocioId`, `GrupoProductoId` | `uuid` | **congelados** (D8), los consume el batch contable |
| `TipoOrigen`, `ClaveOrigen` | | D5 |
| `CreatedAtUtc`, `CreatedBy`, `UsuarioId` | | append-only |

Índices:
- `(MovimientoProductoId)`
- `(ProductoId, FechaRegistro)`
- `WHERE "ImporteCosto" <> "ImporteCostoPosteadoContabilidad"` — parcial, lo recorre el batch

### 3.4 `AplicacionesMovimientoProducto`

Equivalente a `Item Application Entry`. Enlaza salidas con las entradas que las cubren,
que es lo que permite ajustar el costo después.

`Id bigint` PK, `MovimientoEntradaId bigint` FK, `MovimientoSalidaId bigint` FK,
`Cantidad numeric(18,6)`, `FechaRegistro date`.

### 3.5 Costeo promedio ponderado

**Al postear una salida** se aplica el promedio vigente a la fecha de registro:

```
CostoPromedio(producto, fecha) =
    SUM(mv."ImporteCosto")  /  SUM(mv."CantidadValorada")
    sobre MovimientosValor del producto
    con FechaRegistro <= fecha y CantidadValorada > 0
```

Si el denominador es 0 (no hay entradas todavía), se usa `Producto.CostoUnitario` y el
movimiento queda marcado para ajuste.

**Rutina `AjustarCostoMovimientos`.** Recorre los productos con `CostoAjustado = false`,
recalcula el promedio en orden cronológico y, cuando el costo aplicado difiere del
recalculado, **inserta un movimiento de valor de ajuste** con el delta (`Ajuste = true`).
Nunca modifica filas existentes (D3). Al terminar marca `Producto.CostoAjustado = true` y
actualiza la proyección `Producto.CostoUnitario`.

Es idempotente: correrla dos veces seguidas no produce filas la segunda vez.

**Verificación de la Fase 3:** tests de existencia derivada con movimientos mezclados;
test de que el costo promedio con entradas a precios distintos da el ponderado correcto;
test de que `AjustarCostoMovimientos` es idempotente; test de que ningún camino de código
hace `UPDATE` ni `DELETE` sobre los libros.

---

## Fase 4 — Diarios de inventario

Es como se cargan las existencias iniciales sin tener módulo de compras, y como se hacen
los ajustes. Primer productor del libro.

### 4.1 Tablas

`PlantillasDiario`: `Id uuid`, `Codigo varchar(20)` UNIQUE, `Nombre varchar(100)`,
`Tipo smallint` (Articulo=1, Reclasificacion=2), `SerieId uuid` FK.

`LotesDiario`: `Id uuid`, `PlantillaDiarioId uuid` FK, `Codigo varchar(20)`,
`Nombre varchar(100)`, `SerieId uuid` FK nullable, `Bloqueado bool`.
UNIQUE(`PlantillaDiarioId`, `Codigo`).

`LineasDiario`:

| Columna | Tipo | Notas |
|---|---|---|
| `Id` | `uuid` PK | |
| `LoteDiarioId` | `uuid` | FK |
| `NumeroLinea` | `int` | UNIQUE con `LoteDiarioId` |
| `FechaRegistro`, `FechaDocumento` | `date` | |
| `TipoDocumento` | `smallint` | |
| `NumeroDocumento` | `varchar(20)` | |
| `TipoMovimiento` | `smallint` | AjustePositivo=3, AjusteNegativo=4, Transferencia=5 |
| `ProductoId` | `uuid` | FK |
| `AlmacenId` | `uuid` | FK |
| `AlmacenDestinoId` | `uuid` | FK, solo en reclasificación |
| `UnidadMedidaId` | `uuid` | FK |
| `CantidadPorUnidadMedida` | `numeric(18,6)` | congelado al capturar |
| `Cantidad` | `numeric(18,6)` | siempre positiva; el signo lo da `TipoMovimiento` |
| `CostoUnitario` | `numeric(18,4)` | requerido en ajuste positivo |
| `ImporteCosto` | `numeric(18,4)` | calculado |
| `Descripcion` | `varchar(200)` | |
| auditoría + `xmin` | | es borrador: sí lleva concurrencia |

### 4.2 Flujo de posteo

`PostearLoteDiarioCommand` ejecuta **en una sola transacción** (Fase 1.1):

1. **Validar todas las líneas** antes de escribir nada: producto y almacén existen y no
   están bloqueados, cantidad > 0, ajuste positivo con costo unitario, reclasificación con
   almacén destino distinto del origen, ajuste negativo con existencia suficiente a la
   fecha de registro.
2. Reservar el número de registro de la serie (`FOR UPDATE`, D9).
3. Por cada línea, crear los movimientos:
   - **Ajuste positivo:** un `MovimientoProducto` con `Cantidad = +cantidadBase`,
     `CantidadRestante = +cantidadBase`, y un `MovimientoValor` con
     `ImporteCosto = +cantidadBase * CostoUnitario`.
   - **Ajuste negativo:** un `MovimientoProducto` con `Cantidad = -cantidadBase`, y un
     `MovimientoValor` con `ImporteCosto = -cantidadBase * CostoPromedio(fecha)`. Se
     crean las `AplicacionesMovimientoProducto` contra las entradas abiertas por orden de
     fecha, decrementando su `CantidadRestante`.
   - **Reclasificación:** **dos** movimientos, uno negativo en el almacén origen y otro
     positivo en el destino, con el mismo importe de costo y signo opuesto.
4. Borrar las líneas del lote (ya están en el libro).
5. Crear el `RegistroDiario` con el rango de movimientos generados.
6. Marcar `Producto.CostoAjustado = false` en los productos afectados.

`RegistrosDiario`: `Id bigint`, `NumeroRegistro varchar(20)`, `LoteDiarioId uuid`,
`DesdeMovimientoProducto bigint`, `HastaMovimientoProducto bigint`, `FechaCreacion`,
`CreadoPor`, `UsuarioId`.

**Invariante verificada en el posteo:** la suma de `MovimientoValor.ImporteCosto` de una
reclasificación es exactamente 0.

**Verificación de la Fase 4:** test de que postear un lote con una línea inválida no
escribe **ninguna** fila (atomicidad real, con rollback verificado); test de ajuste
negativo sin existencia suficiente → `Result` fallido; test de reclasificación con suma de
costo 0; test de que el lote queda vacío tras postear.

---

## Fase 5 — Contabilidad simplificada

Aquí es donde se guardan los ingresos y su tipo, que es el objetivo declarado del módulo.

El principio rector es D4: **ninguna cuenta se captura en un documento**. El documento
guarda clasificadores y la cuenta se deriva al postear, con un lookup de clave compuesta.

### 5.1 Plan de cuentas

`CuentasContables`:

| Columna | Tipo | Notas |
|---|---|---|
| `Id` | `uuid` PK | |
| `Numero` | `varchar(20)` | UNIQUE |
| `Nombre` | `varchar(100)` | NOT NULL |
| `TipoCuenta` | `smallint` | Posteo=1, Encabezado=2, Total=3, InicioTotal=4, FinTotal=5 |
| `TipoResultado` | `smallint` | Resultado=1, Balance=2 |
| `PosteoDirecto` | `bool` | si false, solo la posteo automático |
| `Bloqueada` | `bool` | |
| `Sangria` | `int` | nivel en la jerarquía de presentación |
| auditoría + `xmin` + soft delete | | |

**El saldo no se almacena** (D1): se deriva de `MovimientosContables`. Solo las cuentas
con `TipoCuenta = Posteo` admiten movimientos.

### 5.2 Grupos contables

Seis tablas, todas con la misma forma mínima — `Id uuid`, `Codigo varchar(20)` UNIQUE,
`Descripcion varchar(100)`. Están **casi vacías a propósito**: toda la contabilidad vive
en las intersecciones, no en los grupos.

`GruposNegocio`, `GruposProducto`, `GruposIvaNegocio`, `GruposIvaProducto`,
`GruposInventario`, `GruposClienteContable`.

La excepción es `GruposClienteContable`, que sí lleva cuentas porque su lookup es
**unidimensional**: `CuentaCxCId uuid` FK NOT NULL, `CuentaDescuentoId`,
`CuentaInteresId`.

Los tres grupos del producto no son alternativas, son **ejes ortogonales**:

| Grupo | Determina | Se cruza con |
|---|---|---|
| `GrupoProducto` | la cuenta de **resultado** (ventas, costo de ventas) | el grupo de negocio del socio |
| `GrupoIvaProducto` | la **tasa** y la cuenta de impuesto | el grupo de IVA del socio |
| `GrupoInventario` | la cuenta de **activo** donde está la existencia | el **almacén**, no el cliente |

### 5.3 Tablas de setup (las intersecciones)

`SetupsContableGeneral` — UNIQUE(`GrupoNegocioId`, `GrupoProductoId`), ambos nullable
(`NULL` = comodín):

`CuentaVentasId`, `CuentaCostoVentasId`, `CuentaDescuentoVentasId`,
`CuentaAjusteInventarioId` — todas FK a `CuentasContables`.

Nota: **las cuentas de Ventas y de Costo de Ventas salen de la misma fila**.

`SetupsIva` — UNIQUE(`GrupoIvaNegocioId`, `GrupoIvaProductoId`):

`PorcentajeIva numeric(9,5)`, `CuentaIvaVentasId`, `CuentaIvaComprasId`,
`IdentificadorIva varchar(20)`, `TipoCalculoIva smallint` (Normal=1, Exento=2).

El `IdentificadorIva` es la clave de agrupación del cálculo de IVA (ver 6.3).

`SetupsInventario` — UNIQUE(`AlmacenId`, `GrupoInventarioId`), `AlmacenId` nullable
(comodín):

`CuentaInventarioId`, `CuentaAjusteInventarioId`, `CuentaVariacionCostoId`.

### 5.4 Derivador de cuentas

```csharp
public interface IDerivadorCuentas
{
    Task<Result<Guid>> CuentaCxCAsync(Guid grupoClienteContableId, CancellationToken ct);
    Task<Result<Guid>> CuentaVentasAsync(Guid? grupoNegocioId, Guid? grupoProductoId, CancellationToken ct);
    Task<Result<Guid>> CuentaCostoVentasAsync(Guid? grupoNegocioId, Guid? grupoProductoId, CancellationToken ct);
    Task<Result<SetupIvaResuelto>> IvaAsync(Guid? grupoIvaNegocioId, Guid? grupoIvaProductoId, CancellationToken ct);
    Task<Result<Guid>> CuentaInventarioAsync(Guid? almacenId, Guid grupoInventarioId, CancellationToken ct);
}
```

Resolución: primero la fila exacta; si no existe, la fila con comodín `NULL`; si tampoco,
`Result` fallido con `setup_contable.inexistente` y el detalle de qué combinación faltó
(el equivalente al clásico "Posting Setup does not exist" de BC).

### 5.5 Libro contable

`MovimientosContables` (append-only, equivalente a `G/L Entry` 17):

| Columna | Tipo | Notas |
|---|---|---|
| `Id` | `bigint` PK identidad | |
| `CuentaContableId` | `uuid` | FK |
| `NumeroCuenta` | `varchar(20)` | **congelado** (D8) |
| `FechaRegistro`, `FechaDocumento` | `date` | |
| `TipoDocumento` | `smallint` | |
| `NumeroDocumento` | `varchar(20)` | |
| `Descripcion` | `varchar(200)` | |
| `Importe` | `numeric(18,4)` | NOT NULL, con signo: + débito, − crédito |
| `Debito`, `Credito` | `numeric(18,4)` | desglose para reporte |
| `RegistroContableId` | `bigint` | FK |
| `SocioNegocioId`, `ProductoId` | `uuid` | nullable, dimensiones de análisis |
| `GrupoNegocioId`, `GrupoProductoId`, `GrupoIvaNegocioId`, `GrupoIvaProductoId` | `uuid` | congelados |
| `TipoOrigen`, `ClaveOrigen` | | D5 |
| auditoría de creación | | |

Índices: `(CuentaContableId, FechaRegistro)`, `(TipoDocumento, NumeroDocumento)`,
`(RegistroContableId)`, `(TipoOrigen, ClaveOrigen)`.

`RegistrosContables`: `Id bigint`, `NumeroRegistro varchar(20)`,
`DesdeMovimiento bigint`, `HastaMovimiento bigint`, `FechaCreacion`, `CreadoPor`,
`UsuarioId`, `TipoOrigen`, `ClaveOrigen`.

**Invariante dura:** todo `RegistroContable` cumple
`SUM(MovimientosContables.Importe) = 0`. Se verifica en el posteo y el commit falla si no
cuadra.

### 5.6 Batch `PostearCostoInventarioContabilidad`

El costo de ventas **no se contabiliza al facturar** salvo que
`ConfiguracionInventario.PosteoAutomaticoCosto = true`. Por defecto lo hace este batch,
posterior y desacoplado — igual que BC.

Recorre los `MovimientosValor` donde
`ImporteCosto <> ImporteCostoPosteadoContabilidad`, y por cada uno postea el **delta**:

```
delta = ImporteCosto - ImporteCostoPosteadoContabilidad
  débito  CuentaCostoVentas   (de SetupsContableGeneral[GrupoNegocio × GrupoProducto])
  crédito CuentaInventario    (de SetupsInventario[Almacen × GrupoInventario])
luego  ImporteCostoPosteadoContabilidad += delta
```

Es **idempotente por construcción**: una segunda ejecución encuentra delta 0 y no escribe
nada. Requiere haber corrido antes `AjustarCostoMovimientos` (Fase 3.5).

Este batch es la razón por la que el inventario (Fase 3) puede existir y funcionar **antes**
que la contabilidad (Fase 5): el acoplamiento es aditivo y diferido.

### 5.7 Datos semilla

Un plan de cuentas mínimo y los setups que lo acompañan, de modo que el sistema pueda
facturar recién instalado: cuentas de CxC, Ventas, IVA por pagar, Inventario, Costo de
Ventas, Ajuste de Inventario y Caja; grupos `NACIONAL`/`EXTERIOR` (negocio),
`BIENES`/`SERVICIOS` (producto), `ITBIS18`/`EXENTO` (IVA), `GENERAL` (inventario y
cliente); y las filas de intersección correspondientes.

**Verificación de la Fase 5:** test de derivación de cuenta exacta y por comodín; test de
que una combinación sin setup devuelve `Result` fallido y **no** escribe nada; test de que
todo registro contable cuadra a 0; test de idempotencia del batch de costo (dos
ejecuciones seguidas → la segunda no inserta filas).

---

## Fase 6 — Facturación de ventas y cuentas por cobrar

### 6.1 Documento borrador

`FacturasVentaBorrador`:

| Columna | Tipo | Notas |
|---|---|---|
| `Id` | `uuid` PK | |
| `Numero` | `varchar(20)` | UNIQUE, de serie **con huecos** (D9) |
| `SocioNegocioId` | `uuid` | FK — vender-a |
| `SocioNegocioFacturarAId` | `uuid` | FK — facturar-a, por defecto el mismo |
| `NombreFacturacion`, `RazonSocialFacturacion` | `varchar(200)` | snapshot del socio |
| `TipoDocumentoFiscal`, `NumeroDocumentoFiscal` | | snapshot |
| `DireccionFacturacionLinea1/2`, `CiudadFacturacion`, `PaisCodigoFacturacion` | | snapshot |
| `FechaRegistro`, `FechaDocumento`, `FechaVencimiento` | `date` | |
| `TerminoPagoId` | `uuid` | FK, congelado |
| `GrupoNegocioId`, `GrupoIvaNegocioId`, `GrupoClienteContableId` | `uuid` | **congelados al crear** (D8) |
| `AlmacenId` | `uuid` | FK, por defecto para las líneas |
| `Estado` | `smallint` | Abierta=1, Liberada=2 |
| `Moneda` | `varchar(3)` | `DOP` |
| `Descripcion` | `varchar(200)` | |
| auditoría + `xmin` | | |

**Los totales no se almacenan** (D1): se derivan de las líneas mientras es borrador.

`LineasFacturaVentaBorrador`:

| Columna | Tipo | Notas |
|---|---|---|
| `Id` | `uuid` PK | |
| `FacturaVentaBorradorId` | `uuid` | FK |
| `NumeroLinea` | `int` | UNIQUE con el anterior |
| `Tipo` | `smallint` | Producto=1, CuentaContable=2, Comentario=3 |
| `ProductoId` / `CuentaContableId` | `uuid` | nullable según `Tipo` |
| `Descripcion` | `varchar(200)` | |
| `AlmacenId` | `uuid` | FK |
| `UnidadMedidaId` | `uuid` | FK |
| `CantidadPorUnidadMedida` | `numeric(18,6)` | congelado |
| `Cantidad` | `numeric(18,6)` | > 0 |
| `PrecioUnitario` | `numeric(18,4)` | |
| `PorcentajeDescuentoLinea` | `numeric(9,5)` | |
| `ImporteDescuentoLinea`, `ImporteLinea` | `numeric(18,4)` | calculados, redondeados a 2 |
| `GrupoProductoId`, `GrupoIvaProductoId`, `GrupoInventarioId` | `uuid` | congelados |
| `IdentificadorIva` | `varchar(20)` | congelado |
| `PorcentajeIva` | `numeric(9,5)` | congelado |
| auditoría + `xmin` | | |

### 6.2 Documento posteado (inmutable)

`FacturasVenta` y `LineasFacturaVenta` replican la estructura anterior **sin** `Estado`,
**sin** discriminador de tipo y **sin** `xmin` (D6). La PK de la cabecera es
`Numero varchar(20)` a secas, tomado de una serie **sin huecos**.

Los totales sí se congelan aquí, porque son el documento legal:
`ImporteSinIva`, `ImporteIva`, `ImporteTotal` — todos `numeric(18,4)` redondeados a 2.

`LineasIvaFacturaVenta` (equivalente a `VAT Amount Line`): `Id uuid`,
`FacturaVentaNumero varchar(20)` FK, `IdentificadorIva varchar(20)`,
`PorcentajeIva numeric(9,5)`, `BaseImponible numeric(18,4)`, `ImporteIva numeric(18,4)`,
`CuentaIvaId uuid`.

### 6.3 Cálculo del IVA — agrupado, no por línea

Este es el detalle más fácil de equivocar de todo el proyecto. El IVA se calcula sobre la
**suma de las líneas que comparten `IdentificadorIva`**, no línea a línea. Hacerlo por
línea produce diferencias de céntimos contra la factura legal.

```
para cada grupo g de líneas con el mismo IdentificadorIva:
    baseImponible(g) = REDONDEAR( SUM(linea.ImporteLinea), 2 )
    importeIva(g)    = REDONDEAR( baseImponible(g) * PorcentajeIva(g) / 100, 2 )

ImporteSinIva = SUM( baseImponible(g) )
ImporteIva    = SUM( importeIva(g) )
ImporteTotal  = ImporteSinIva + ImporteIva
```

Cada grupo produce una fila en `LineasIvaFacturaVenta` y una pata de crédito de IVA en
el asiento.

### 6.4 Movimientos de cliente

`MovimientosCliente` (append-only, equivalente a `Cust. Ledger Entry` 21):

`Id bigint` PK, `SocioNegocioId uuid` FK, `FechaRegistro`, `FechaDocumento`,
`FechaVencimiento`, `TipoDocumento smallint` (Factura=1, NotaCredito=2, Pago=3, Ajuste=4),
`NumeroDocumento varchar(20)`, `Descripcion varchar(200)`,
`ImporteOriginal numeric(18,4)`, `GrupoClienteContableId uuid`, `CuentaCxCId uuid`
(congelada), `TipoOrigen`, `ClaveOrigen`, auditoría de creación.

**Sin columna `ImporteRestante` y sin columna `Abierta`** (D1, D7): ambas se derivan.

`MovimientosClienteDetalle` (append-only, equivalente a `Detailed Cust. Ledg. Entry` 379):

`Id bigint` PK, `MovimientoClienteId bigint` FK,
`TipoMovimiento smallint` (ImporteInicial=1, Pago=2, Aplicacion=3, Descuento=4,
Redondeo=5), `Importe numeric(18,4)`, `FechaRegistro date`,
`MovimientoClienteAplicadoId bigint` nullable (la contraparte),
`TipoOrigen`, `ClaveOrigen`, auditoría de creación.

```
ImporteRestante(movimiento) = SUM(detalle.Importe) del movimiento
Abierta(movimiento)         = ImporteRestante <> 0
SaldoCliente(socio)         = SUM(detalle.Importe) de todos sus movimientos
```

**Aplicar un pago a una factura inserta dos filas de detalle** —una en la factura y otra
en el pago, de signo opuesto, apuntándose mutuamente con
`MovimientoClienteAplicadoId`— y **nunca actualiza nada** (D3).

### 6.5 Motor de posteo

`PostearFacturaVentaCommand` ejecuta **todo en una transacción**:

1. **Validar:** la factura tiene al menos una línea; el socio existe y no está bloqueado
   para facturación; los productos no están bloqueados para venta; todas las cantidades
   > 0; existe existencia suficiente por línea a la fecha de registro; existen todos los
   setups contables necesarios (se resuelven **antes** de escribir nada).
2. Reservar el número de la serie de posteado (`FOR UPDATE`, sin huecos).
3. Calcular el IVA agrupado por `IdentificadorIva` (6.3).
4. Copiar cabecera y líneas a las tablas posteadas, con totales congelados, y crear las
   `LineasIvaFacturaVenta`.
5. Por cada línea de tipo Producto:
   - `MovimientoProducto` con `TipoMovimiento = Venta`, `Cantidad = -cantidadBase`
   - `MovimientoValor` con `ImporteVenta = +ImporteLinea` e
     `ImporteCosto = -cantidadBase * CostoPromedio(fecha)`
   - `AplicacionesMovimientoProducto` contra las entradas abiertas por orden de fecha
6. `MovimientoCliente` (Factura, `ImporteOriginal = ImporteTotal`) más su
   `MovimientosClienteDetalle` de tipo `ImporteInicial`.
7. `RegistroContable` más sus `MovimientosContables`:

   | Pata | Importe | Cuenta derivada de |
   |---|---|---|
   | Débito CxC | `ImporteTotal` | `GruposClienteContable.CuentaCxC` |
   | Crédito Ventas | `baseImponible` por grupo de producto | `SetupsContableGeneral[GrupoNegocio × GrupoProducto]` |
   | Crédito IVA | `importeIva` por grupo de IVA | `SetupsIva[GrupoIvaNegocio × GrupoIvaProducto]` |
   | Débito Costo de Ventas | costo | `SetupsContableGeneral[...]` — **solo si `PosteoAutomaticoCosto`** |
   | Crédito Inventario | costo | `SetupsInventario[Almacen × GrupoInventario]` — **solo si `PosteoAutomaticoCosto`** |

8. Verificar que `SUM(Importe) = 0` en el registro. Si no cuadra, abortar la transacción.
9. Borrar el borrador.
10. Marcar `Producto.CostoAjustado = false` en los productos afectados.

### 6.6 Cobros

`RegistrarPagoClienteCommand` crea un `MovimientoCliente` de tipo Pago con su detalle, y
el asiento correspondiente (débito Caja / crédito CxC).

`AplicarPagoCommand` inserta las filas de detalle en ambos lados. No genera asiento: el
importe ya está en el libro contable desde el pago y la factura.

**Verificación de la Fase 6:** test de que el IVA agrupado difiere del IVA por línea en un
caso con tres líneas del mismo identificador y da el valor correcto; test de que postear
sin setup contable no escribe **ninguna** fila; test de que el asiento cuadra a 0; test de
que el saldo derivado del cliente coincide tras factura + pago parcial + aplicación; test
de que la serie de posteado no deja huecos bajo dos posteos concurrentes.

---

## Fase 7 — Vistas de movimientos

Páginas Blazor **Static SSR** (sin `@rendermode`, sin `@onclick`), con formularios GET
para filtros y paginación por query string, según la convención del proyecto.

| Vista | Contenido |
|---|---|
| Movimientos de producto | Filtro por producto, almacén y rango de fechas; cantidad y saldo acumulado |
| Movimientos de valor | Costo por movimiento, costo por unidad, marca de ajuste |
| Existencias por almacén | Existencia derivada y valor de inventario, por producto y almacén |
| Movimientos de cliente | Documentos del socio con importe restante derivado y marca de abierto |
| Estado de cuenta | Antigüedad de saldos por tramos (corriente, 1-30, 31-60, 61-90, 90+) |
| Movimientos contables | Por cuenta y rango de fechas |
| Balance de comprobación | Débitos, créditos y saldo por cuenta, derivados |

Todas sobre repositorios Dapper paginados, con allow-list de columnas de ordenación
(Fase 1.8).

---

## Estrategia de verificación

Cada fase termina con `dotnet build test.slnx` verde y sus tests propios.

**Limitación conocida del entorno:** los tests de integración
(`tests/OpenSource1.SmokeTests/Api/`) levantan un contenedor `postgres:17-alpine` vía
`PostgresTestFixture`. Si el daemon de Docker no está disponible, esos tests no pueden
ejecutarse y la verificación se limita a build + tests unitarios. Esto debe reportarse
explícitamente en cada fase, no darse por verificado.

**Invariantes que deben tener test propio:**

1. Ningún camino de código hace `UPDATE` ni `DELETE` sobre `MovimientosProducto`,
   `MovimientosValor`, `MovimientosCliente`, `MovimientosClienteDetalle` ni
   `MovimientosContables` (D3).
2. Todo `RegistroContable` suma 0.
3. La existencia derivada coincide con la suma de los movimientos.
4. `AjustarCostoMovimientos` y `PostearCostoInventarioContabilidad` son idempotentes.
5. Un posteo fallido no deja ninguna fila escrita.
6. La serie sin huecos no produce huecos bajo concurrencia.

## Riesgos

| Riesgo | Mitigación |
|---|---|
| La migración `Cliente` → `SocioDeNegocio` toca API y Blazor, no solo la base | Se hace en Fase 2, antes de que existan facturas que dependan de ella |
| El bloqueo de la serie sin huecos serializa los posteos | Alcance mínimo: el `FOR UPDATE` se toma lo más tarde posible dentro de la transacción |
| El costeo promedio con movimientos retroactivos requiere reproceso | La rutina de ajuste recorre en orden cronológico e inserta deltas; no reescribe |
| Sin Docker no se pueden correr los tests de integración | Se reporta como no verificado; no se declara verde lo que no se ejecutó |
| El cambio del contrato de paginación rompe Blazor | Se hace en Fase 1, con 4 módulos, no con 11 |
