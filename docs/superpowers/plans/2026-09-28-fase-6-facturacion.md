# Fase 6 — Facturación de ventas y cuentas por cobrar · Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** facturas de venta en borrador con líneas, posteo atómico a documento legal inmutable (numeración sin huecos,
IVA agrupado), movimientos de producto/valor, libro de clientes (CxC) con saldo derivado y asiento contable cuadrado; y
cobros con aplicación a facturas.

**Architecture:** borradores como maestros (CRUD con `xmin`); documento posteado, libro de clientes y su detalle como
tablas append-only con trigger; un `CalculadoraIvaFactura` pura para el IVA agrupado; un comando `PostearFacturaVenta` que
en UNA transacción valida, deriva todas las cuentas antes de escribir, reserva número, registra inventario con
`IRegistroMovimientosInventario`, escribe documento, libro de clientes y asiento con `IRegistroContable`, y borra el
borrador. Cobros con su asiento (Caja/banco contra CxC) y aplicación que solo inserta detalle.

**Tech Stack:** .NET 10, EF Core 10 + Npgsql, Dapper, MediatR 13, xUnit, Docker/Postgres 17.

**Spec:** [`docs/superpowers/specs/2026-09-12-axionerp-erp-modules-design.md`](../specs/2026-09-12-axionerp-erp-modules-design.md), sección "Fase 6", más el bloque "Desviaciones acordadas durante la ejecución de la Fase 6" que añade la Task 6.1. Pendientes heredados: sección "Resultado y pendientes que hereda la Fase 6" de [`2026-09-27-fase-5-contabilidad.md`](2026-09-27-fase-5-contabilidad.md).

## Global Constraints

- TFM `net10.0`; nullable e implicit usings. Solución `test.slnx`. Sin paquetes nuevos, sin CPM. PostgreSQL 15+.
- Blazor Static SSR: sin `@rendermode`, `@onclick`, `@bind` interactivo ni JS nuevo; `<EditForm>` de un paso con `FormName`
  estático; `ConfirmDialog` + `?deleteId=` para borrar (y `?postear=true` para confirmar el posteo); selects cargados de la
  API con el valor vigente y bloqueo de Guardar si no cargaron; mensajes reales de la API (todos, con número de línea);
  `EntradaDecimal`/`EntradaFecha`; `[SupplyParameterFromQuery]` no admite enums (usar `int?`); `PageRequest.Descendente`
  es `true` por defecto (fijar el orden explícito); módulos de mantenimiento con agregar, modificar, eliminar, consultar y
  limpiar campos.
- Nombres en español. Cantidades y factores `numeric(18,6)`; importes `numeric(18,4)` (los del documento legal redondeados
  a 2); porcentajes `numeric(9,5)`.
- Errores: `Result`/`Error(Codigo, Mensaje, Campo)`; `.no_encontrado`→404, `.conflicto`→409, resto→400; referencia
  inválida en el cuerpo NO usa `.no_encontrado`; errores de líneas con `Campo = "Lineas[<NumeroLinea>].<Campo>"`.
- Maestros/borradores: `BaseEntity`, índices únicos parciales, PUT con `Xmin`, "null = conservar" en opcionales.
- Libros y documentos posteados: sin soft delete ni `xmin`, sin `IAggregateRoot`, append-only con trigger
  (`libro_inventario_append_only()` o equivalente), protegidos por `LibroInventarioSinUpdateNiDeleteTests`.
- Toda escritura en el libro de inventario pasa por `IRegistroMovimientosInventario`; en el contable, por `IRegistroContable`;
  cuentas SIEMPRE derivadas con `IDerivadorCuentas` (D4).
- Transacciones: los comandos de posteo son `IRequest` (no `ICommand`), abren UNA transacción de `IUnitOfWork` y lanzan
  `InvalidOperationException` si ya hay una activa. Orden de locks global: lote/documento → productos (ordenados) → series →
  almacenes compartidos → cuentas (FOR SHARE, orden de Id) → serie CONTAB.
- Migraciones con `dotnet ef migrations add`; sin `xmin` espurio; `ApplicationDbContextModelTests` y sonda vacía tras cada
  migración. Tests contra Postgres real (`DOCKER_CONTEXT=default` solo como variable de entorno); suite completa al final
  de cada task (hoy 1055). 6 avisos CS0618 preexistentes; no añadir avisos.
- Commits LOCALES autorizados en `feat/erp-fase-6-facturacion` (desde `feat/erp-fase-5-contabilidad`). Nunca merge ni push.
  No tocar `appsettings*.json`.
- Permisos: consultar = CanConsult; crear/editar borradores y líneas = CanAdd/CanModify; borrar = CanDelete; **postear una
  factura, registrar y aplicar cobros = CanModify**.
- Runtime obligatorio (API + Blazor + Postgres reales) para cada página nueva, con evidencia en el informe.

## Review Focus

1. **IVA agrupado:** tres líneas con el mismo identificador cuyo IVA por línea suma distinto del IVA del grupo → el
   documento usa el del grupo; totales = suma de grupos. Test en Task 6.3 (calculadora) y 6.4 (documento).
2. **Posteo sin setup** (grupo sin setup general, IVA o inventario; grupo de cliente sin CxC válida): `Result` fallido con
   la combinación que falta y **ninguna** fila escrita en ninguna tabla (documento, clientes, inventario, contable, serie).
   Test en Task 6.4.
3. **Dos posteos concurrentes** de facturas distintas → números consecutivos sin huecos; del mismo borrador → uno gana,
   el otro 404/409 sin duplicar. Test en Task 6.4.
4. **Saldo del cliente** tras factura + pago parcial + aplicación = derivado exacto; aplicar más de lo pendiente → 400; aplicar
   entre socios distintos → 400. Test en Task 6.5.
5. **Existencia insuficiente** en una línea de producto → el posteo falla con el número de línea, sin escribir nada. Test en
   Task 6.4.

---

## Task 6.1 — Desviaciones de diseño (solo docs; la hace el controlador)

```markdown
**Desviaciones acordadas durante la ejecución de la Fase 6:**

- **Sin patas de costo al facturar.** `PosteoAutomaticoCosto` no se implementa (su valor por defecto es falso): el costo lo
  contabiliza el batch de la Fase 5. El asiento de la factura tiene solo CxC, Ventas e IVA.
- **Descuento de línea neto.** `ImporteLinea` ya va neto del descuento y Ventas se acredita neto; no hay pata de
  descuento (la cuenta `CuentaDescuentoVentas` queda para una fase futura).
- **Crédito de Ventas por (GrupoNegocio × GrupoProducto)** con `ROUND(SUM(ImporteLinea), 2)` por grupo; la diferencia de
  redondeo entre la suma de esas bases y `ImporteSinIva` (bases agrupadas por IVA) se ajusta en la pata de Ventas de mayor
  importe, para que el asiento cuadre sin cuenta de redondeo.
- **Líneas de tipo CuentaContable** acreditan esa cuenta (Posteo, no bloqueada y `PosteoDirecto = true`) y llevan su propio
  `GrupoIvaProductoId` (obligatorio) para el IVA; no mueven inventario. Las de tipo Comentario no tienen importes.
- **Series:** borradores `FV-BORR` (con huecos) y facturas posteadas `FV` (sin huecos), sembradas. El generador actual
  serializa también la serie con huecos (limitación aceptada: el alta de borrador es corta).
- **Cobros:** el pago indica la cuenta de caja/banco (Posteo, no bloqueada, `PosteoDirecto = true`; por defecto `1101 Caja`)
  y su asiento es débito caja / crédito CxC derivada del grupo de cliente contable VIGENTE del socio (congelada en el
  movimiento). Serie `COBRO` sin huecos. Aplicar solo entre movimientos del mismo socio, de signo opuesto, por un importe ≤
  el mínimo de los restantes.
- **Cliente facturar-a:** el libro de clientes y la CxC usan `SocioNegocioFacturarAId`; el inventario lleva
  `SocioNegocioId` (vender-a).
- **Guarda de borrado de socios** (pendiente de la Fase 5): un socio con borradores, facturas, movimientos de cliente o
  movimientos contables → 409 `socio_negocio.conflicto`.
```

---

## Task 6.2 — Borradores de factura (dominio, API)

**Files:**
- Create: `src/OpenSource1.Core/Entities/Ventas/FacturaVentaBorrador.cs`, `LineaFacturaVentaBorrador.cs`; enums `EstadoFacturaBorrador` (Abierta=1, Liberada=2), `TipoLineaFactura` (Producto=1, CuentaContable=2, Comentario=3)
- Modify: `ApplicationDbContext.cs`; migración `AddFacturasVentaBorrador` (tablas + series `FV-BORR` y `FV` sembradas por `HasData` con Ids fijos, primer número `FV-BORR`: "00000001"; `FV`: "00000001")
- Create: `src/OpenSource1.Application/Features/FacturasVenta/Borradores/**` (cabecera CRUD; líneas CRUD por borrador; query de totales en vista previa usando la calculadora de la Task 6.3 — si 6.3 aún no existe, esta task crea `CalculadoraIvaFactura` con la firma de 6.3 y 6.3 la completa), repositorios Dapper
- Create: `src/OpenSource1.Api/Controllers/FacturasVentaController.cs` (`api/facturas-venta`: `borradores`, `borradores/{id}`, `borradores/{id}/lineas`, `lineas-borrador/{id}`, `borradores/{id}/totales`)
- Test: handlers y API

Reglas de cabecera (alta): socio vender-a obligatorio (existe, no borrado, no `Bloqueado` Todo); facturar-a por defecto el
mismo; **snapshot** del socio facturar-a (nombre comercial, razón social, documento fiscal, dirección, ciudad, país) y del
`TerminoPagoId`; **congela** `GrupoNegocioId`, `GrupoIvaNegocioId` (del vender-a) y `GrupoClienteContableId` (del
facturar-a) — nulos → 400 `factura.grupo_faltante` con el grupo; `FechaVencimiento` = `FechaDocumento` + días del término
(si no hay término, = `FechaDocumento`); `AlmacenId` por defecto el predeterminado; `Moneda` "DOP"; `Numero` de `FV-BORR`.
PUT: cambiar el socio vuelve a tomar snapshot y grupos; cambiar fechas recalcula el vencimiento si no se envía.
Reglas de línea: `Tipo` Producto (producto existe, no borrado, no bloqueado Venta/Todo; unidad y factor congelados como en
diarios; `AlmacenId` por defecto el de cabecera; congela `GrupoProductoId`, `GrupoIvaProductoId`, `GrupoInventarioId` del
producto; precio por defecto `PrecioVenta` si no se envía), CuentaContable (cuenta Posteo, no bloqueada, `PosteoDirecto`;
`GrupoIvaProductoId` obligatorio), Comentario (solo descripción; sin importes); `Cantidad` > 0, cotas numeric; precio ≥ 0;
descuento 0–100 ≤5 decimales; `ImporteDescuentoLinea = ROUND(Cantidad × Precio × %/100, 2)`, `ImporteLinea =
ROUND(Cantidad × Precio, 2) − ImporteDescuentoLinea`; IVA congelado de `IDerivadorCuentas.IvaAsync(GrupoIvaNegocio de
cabecera, GrupoIvaProducto)` (sin setup → 400 con la combinación); `NumeroLinea` como en diarios (FOR UPDATE del borrador).
Borrador `Liberada` no admite cambios de líneas (400 `factura.liberada`); endpoints `borradores/{id}/liberar` y `reabrir`
(CanModify). Borrar borrador borra sus líneas (soft).

- [ ] Tests (validaciones por tipo de línea, snapshot y grupos congelados, vencimiento, xmin 409, liberar/reabrir, roles) → implementar → suite → commit `feat: borradores de factura de venta con lineas, snapshot del cliente y grupos congelados`.

---

## Task 6.3 — Documento posteado, libro de clientes e IVA agrupado

**Files:**
- Create: `src/OpenSource1.Core/Entities/Ventas/FacturaVenta.cs`, `LineaFacturaVenta.cs`, `LineaIvaFacturaVenta.cs`; `src/OpenSource1.Core/Entities/Clientes/MovimientoCliente.cs`, `MovimientoClienteDetalle.cs`; enums `TipoDocumentoCliente` (Factura=1, NotaCredito=2, Pago=3, Ajuste=4), `TipoDetalleCliente` (ImporteInicial=1, Pago=2, Aplicacion=3, Descuento=4, Redondeo=5)
- Create: `src/OpenSource1.Application/Features/FacturasVenta/Calculo/CalculadoraIvaFactura.cs` (pura, sin BD)
- Modify: `ApplicationDbContext.cs`; migración `AddFacturasVentaYLibroClientes` (tablas, índices, triggers append-only para las cinco tablas); `LibroInventarioSinUpdateNiDeleteTests` (proteger las cinco)
- Create: consultas `GET api/facturas-venta` (posteadas, paginado), `GET api/facturas-venta/{numero}` (cabecera + líneas + líneas de IVA), `GET api/clientes/{id}/movimientos` y `GET api/clientes/{id}/saldo` (derivados, CanConsult)
- Test: calculadora (unitarios exhaustivos), migración, consultas

`FacturaVenta`: PK `Numero varchar(20)`; columnas de la cabecera del borrador sin `Estado`/`xmin`, más `ImporteSinIva`,
`ImporteIva`, `ImporteTotal`, `NumeroBorrador`, `RegistroContableId`, `CreatedAtUtc`, `CreatedBy`, `UsuarioId`.
`LineaFacturaVenta`: `Id bigint` identidad, `FacturaVentaNumero` FK, mismas columnas de la línea borrador sin `xmin`, más
`MovimientoProductoId bigint?`. `LineaIvaFacturaVenta` según spec 6.2 (`Id bigint`). `MovimientoCliente` y
`MovimientoClienteDetalle` según spec 6.4 (`Id bigint`), con índices `(SocioNegocioId, FechaRegistro)`,
`(TipoDocumento, NumeroDocumento)`, `(TipoOrigen, ClaveOrigen)`, `(MovimientoClienteId)`, `(MovimientoClienteAplicadoId)`.
`TipoOrigenMovimiento`: añadir `Cobro = 5` (y usar `FacturaVenta = 2`).

```csharp
public sealed record LineaCalculoIva(int NumeroLinea, string IdentificadorIva, decimal PorcentajeIva, decimal ImporteLinea);
public sealed record GrupoIvaCalculado(string IdentificadorIva, decimal PorcentajeIva, decimal BaseImponible, decimal ImporteIva);
public sealed record TotalesFactura(IReadOnlyList<GrupoIvaCalculado> Grupos, decimal ImporteSinIva, decimal ImporteIva, decimal ImporteTotal);
public static class CalculadoraIvaFactura
{
    // Agrupa por IdentificadorIva (ordinal) — mismo identificador con porcentajes distintos → InvalidOperationException
    // (no debería ocurrir: el identificador es la clave del setup). Redondeo AwayFromZero a 2 (spec 6.3).
    public static TotalesFactura Calcular(IReadOnlyList<LineaCalculoIva> lineas);
}
```

- [ ] Tests: Review Focus 1 con números concretos (p. ej. tres líneas de 0.33 al 18 %: por línea 0.06×3 = 0.18, agrupado
  ROUND(0.99×0.18,2) = 0.18 — busca un caso donde difieran, p. ej. 3 × 10.03 al 18 %: por línea 1.81×3 = 5.43, agrupado
  ROUND(30.09×0.18,2) = 5.42), exento, varios identificadores, lista vacía; migración y triggers; saldo derivado por SQL
  directo sobre detalle → implementar → suite → commit `feat: factura de venta posteada, libro de clientes con detalle y calculadora de IVA agrupado`.

---

## Task 6.4 — Motor de posteo de facturas

**Files:**
- Create: `Features/FacturasVenta/Posteo/PostearFacturaVentaCommand.cs` + handler; endpoint `POST api/facturas-venta/borradores/{id}/postear` (CanModify) → 200 `{ numero, importeTotal, registroContable }`
- Create (si hace falta): `src/OpenSource1.Application/Services/Clientes/IRegistroMovimientosCliente.cs` + Infrastructure (Dapper, único escritor del libro de clientes; lo usa también la Task 6.5)
- Modify: `DeleteSocioNegocioCommandHandler` (guarda heredada) y su servicio de uso
- Test: `PostearFacturaVentaTests` (Postgres) y API

Algoritmo (una transacción; ver desviaciones 6.1): lock del borrador (`FOR UPDATE`) y de sus líneas; validar (al menos una
línea no comentario; socio no bloqueado Facturacion/Todo; productos no bloqueados Venta/Todo; cantidades > 0; factor
vigente = congelado); **derivar TODAS las cuentas antes de escribir** (CxC del grupo de cliente congelado; Ventas por
GrupoNegocio × GrupoProducto; IVA por grupo; cuentas de líneas CuentaContable) → cualquier fallo: `Result` con todos los
errores (con número de línea) y nada escrito; `BloquearProductosAsync`; número de `FV`; `CalculadoraIvaFactura`; insertar
`FacturaVenta`, `LineasFacturaVenta`, `LineasIvaFacturaVenta`; por línea Producto `RegistrarAsync` salida `Venta`
(`TipoDocumento = FacturaVenta`, `NumeroDocumento` = número, `SocioNegocioId` = vender-a, `ImporteVenta` = `ImporteLinea`,
`TipoOrigen = FacturaVenta`, `ClaveOrigen` = número) — existencia insuficiente → `Result` con `Lineas[n].Cantidad` y rollback;
`MovimientoCliente` (Factura, facturar-a, `ImporteOriginal = ImporteTotal`, CxC congelada) + detalle `ImporteInicial`;
asiento con `IRegistroContable` (`TipoDocumentoContable.FacturaVenta`): débito CxC total, créditos Ventas por grupo (con el
ajuste de redondeo), créditos IVA por grupo con importe ≠ 0, créditos de líneas CuentaContable; guardar
`RegistroContableId` en la factura (insertar la factura DESPUÉS del asiento o reservar el id — sin UPDATE de la factura);
borrar lógicamente borrador y líneas. `Producto.CostoAjustado` ya lo marca `RegistrarAsync`.

- [ ] Tests: Review Focus 1 (documento con el IVA agrupado), 2 (cada tipo de setup faltante → nada escrito, contando filas de
  todas las tablas y el número de serie), 3 (dos posteos concurrentes sin huecos; mismo borrador dos veces), 5; asiento
  cuadrado con importes concretos; facturar-a distinto de vender-a; línea CuentaContable; línea Comentario; descuento;
  redondeo de Ventas vs IVA; borrador borrado tras postear; existencia y valor tras postear; guarda de borrado de socio (409).
  Mutaciones: derivar después de escribir (el test de "nada escrito" debe fallar), IVA por línea (Review Focus 1 falla),
  quitar el ajuste de redondeo (el cuadre falla en el caso preparado) → implementar → suite → commit `feat: posteo atomico de facturas de venta con inventario, libro de clientes y asiento contable`.

---

## Task 6.5 — Cobros y aplicación

**Files:**
- Create: `Features/Cobros/**` (`RegistrarPagoClienteCommand`, `AplicarPagoCommand`), endpoints `POST api/cobros` (CanModify) → `{ numero, movimientoClienteId, registroContable }`, `POST api/cobros/aplicaciones` (CanModify), `GET api/clientes/{id}/movimientos-abiertos` (CanConsult: movimientos con restante ≠ 0)
- Modify: `IRegistroMovimientosCliente` (pago y aplicación)
- Test: `CobrosTests` (Postgres) y API

Pago: socio existe; importe > 0 (≤ 2 decimales, cota numeric); cuenta de caja válida (desviaciones); fecha; número de
`COBRO` (serie sembrada en la migración de esta task); `MovimientoCliente` Pago con `ImporteOriginal = −importe` y detalle
`Pago = −importe`; asiento débito caja / crédito CxC (grupo de cliente contable vigente del socio → sin grupo o sin CxC →
400 sin escribir). Aplicación: dos movimientos del mismo socio, uno con restante > 0 (factura) y otro con restante < 0
(pago); importe > 0 y ≤ min(restante factura, −restante pago); inserta dos detalles `Aplicacion` (−importe en la factura,
+importe en el pago) que se apuntan mutuamente; lock de ambos movimientos (`FOR UPDATE` en orden de Id) para que dos
aplicaciones concurrentes no excedan el restante. Sin asiento.

- [ ] Tests: Review Focus 4 (factura 118 + pago 50 + aplicación 50 → restante factura 68, pago 0, saldo 68; aplicar 60 del
  pago → 400; socios distintos → 400; dos aplicaciones concurrentes de 40 sobre un restante de 50 → una falla); asiento del
  pago cuadrado; saldo derivado = suma de detalle → implementar → suite → commit `feat: cobros de clientes con asiento y aplicacion a facturas`.

---

## Task 6.6 — Páginas Blazor de facturación y cobros

**Files:**
- Create: `Pages/FacturasVentaBorradores.razor` (lista + alta/edición de cabecera), `Pages/FacturaVentaBorrador.razor` (líneas, totales en vista previa, liberar/reabrir, postear con `?postear=true`), `Pages/FacturasVenta.razor` (posteadas, lista y detalle con líneas y líneas de IVA), `Pages/Cobros.razor` (registrar pago; movimientos abiertos del cliente; aplicar), saldo del cliente en la ficha de cliente; clientes HTTP y menú
- Reutilizar `BuscadorProducto` y los patrones de `DiarioInventarioLote.razor`

- [ ] Runtime: crear borrador, líneas de los tres tipos, totales en vista previa, liberar, postear (número FV, existencias,
  saldo del cliente), posteo con setup faltante (mensajes), registrar pago y aplicar (saldo), Ejecutor sin botones de
  posteo/cobro y 403 forzado, API caída en selects → suite → commit `feat: paginas Blazor Static SSR de facturacion y cobros`.

---

## Task 6.7 — Cierre de la Fase 6

- [x] Cadena de migraciones desde vacía y desde el final de la Fase 5 con datos; Down y vuelta a HEAD.
- [x] `ApplicationDbContextModelTests`, sonda vacía, `has-pending-model-changes`; `grep` TODO; suite completa.
- [x] Sección "Resultado y pendientes que hereda la Fase 7" al final de este plan.

---

## Resultado y pendientes que hereda la Fase 7

**Resultado.** La Fase 6 queda cerrada: borradores de factura de venta con líneas de producto, cuenta contable y
comentario, grupos e IVA congelados, liberar/reabrir y serie `FV-BORR` (Task 6.2); documento legal posteado
(`FacturasVenta`, `LineasFacturaVenta`, `LineasIvaFacturaVenta`), libro de clientes con detalle (`MovimientosCliente`,
`MovimientosClienteDetalle`) con saldo e importe restante derivados, y `CalculadoraIvaFactura` pura con el IVA agrupado
por identificador (Task 6.3); motor de posteo atómico `PostearFacturaVenta` — deriva todas las cuentas antes de escribir,
número `FV` sin huecos, salidas de inventario por `IRegistroMovimientosInventario`, movimiento de cliente, asiento por
`IRegistroContable` (CxC / Ventas por grupo con ajuste de redondeo / IVA por grupo / cuentas de línea) y borrado del
borrador — más la guarda de borrado de socios heredada de la Fase 5 (Task 6.4); cobros con asiento caja/CxC, serie
`COBRO` y aplicación a facturas con bloqueo de ambos movimientos (Task 6.5); y páginas Static SSR de borradores, facturas
posteadas y cobros, con el saldo en la ficha del cliente (Task 6.6). Las cinco Review Focus de la fase tienen test
dedicado y pasan.

La cadena de migraciones se verificó en ambos sentidos con un contenedor `postgres:17-alpine` temporal (puerto 65467,
`DOCKER_CONTEXT=default`, borrado al terminar):

1. **BD vacía → HEAD** (34 migraciones, `InitialApplicationDb` … `AddSerieCobro`): aplica limpio.
2. **BD en el estado final de la Fase 5 con datos → HEAD**: migrada hasta `20260926152707_PermitirContabilizacionCosto`
   (30 migraciones) y sembrada por SQL con un almacén, un producto (`BIENES/ITBIS18/GENERAL`), un socio
   (`NACIONAL/ITBIS18/GENERAL`), un movimiento de producto y uno de valor de apertura (`TipoOrigen = 99`, 100 UND a 8.00,
   ya contabilizado) y un asiento contable cuadrado (1301 / 5201 por 800.00, serie `CONTAB` en `00000001`). Tras
   `dotnet ef database update` a HEAD (las 4 migraciones de la Fase 6), comprobado por SQL: las 7 tablas nuevas
   (`FacturasVentaBorrador`, `LineasFacturaVentaBorrador`, `FacturasVenta`, `LineasFacturaVenta`, `LineasIvaFacturaVenta`,
   `MovimientosCliente`, `MovimientosClienteDetalle`); los 10 triggers `TR_<tabla>_AppendOnly`/`TR_<tabla>_NoTruncate`
   de las cinco tablas append-only con `tgenabled = 'O'` sobre `libro_inventario_append_only()` (un `TRUNCATE` de
   `FacturasVenta` y de `MovimientosCliente` se rechaza); series `FV-BORR` (con huecos), `FV` y `COBRO` (sin huecos)
   sembradas en `00000000`; los CHECK de la 6.3/6.4 (`CK_FacturasVenta_Total`, `CK_FacturasVenta_Redondeo`,
   `CK_LineasFacturaVenta_MovimientoProducto`, `_Cantidad`, `_Referencia`, `_Tipo`, `CK_MovimientosCliente_TipoDocumento`,
   `CK_MovimientosClienteDetalle_Aplicado`, `_TipoMovimiento` y los de borradores) y los índices únicos de las redes de la
   6.4 (`IX_FacturasVenta_RegistroContableId`, `IX_MovimientosCliente_TipoDocumento_NumeroDocumento`,
   `IX_FacturasVenta_NumeroBorrador`, `IX_FacturasVentaBorrador_Numero`). El esquema de la Fase 6 es idéntico al de la BD
   migrada desde vacía, y la huella (md5 por tabla) de los datos previos no cambia.
3. **Down a `PermitirContabilizacionCosto`** (Down completo de la Fase 6, quedan 30 migraciones): las 7 tablas, sus
   triggers y las series `FV-BORR`/`FV`/`COBRO` desaparecen; el resultado es byte a byte la misma salida de verificación
   que antes de subir (mismas huellas de almacenes, productos, socios, movimientos de producto y valor, registros y
   movimientos contables, cuentas; `CONTAB` sigue en `00000001`; 13 triggers, los de la Fase 5).
4. **Up de nuevo a HEAD**: salida de verificación idéntica a la del paso 2 (sin duplicados de `HasData`).

Resto de la verificación: `dotnet build test.slnx --no-incremental` con 0 errores y solo los 6 avisos CS0618
preexistentes (se corrigieron 3 avisos de nulabilidad CS8602/CS8629 que había dejado la ronda de arreglos de la Task 6.6
en `FacturasVentaBorradores.razor`/`FacturaVentaBorrador.razor`, sin cambio de comportamiento); `ApplicationDbContextModelTests`
en verde; sonda `ProbeFase67` con `Up()`/`Down()` vacíos y el repositorio sin cambios (borrada);
`dotnet ef migrations has-pending-model-changes` → "No changes have been made to the model since the last migration.";
`grep -rnwE "TODO|FIXME|HACK|XXX"` sobre `src/`/`tests/` sin marcadores reales (solo "TODO" como énfasis en comentarios y
el código de prueba `HACK-1`). Suite completa (`DOCKER_CONTEXT=default dotnet test test.slnx`): **1185/1185** (sin tests nuevos: task de verificación y documentación), ~15 min.

**Limitaciones conocidas que hereda la Fase 7:**

- **Sin patas de costo al facturar.** `PosteoAutomaticoCosto` sigue sin implementarse: el asiento de la factura tiene solo
  CxC, Ventas, IVA y cuentas de línea. El costo de la venta lo contabiliza el batch manual de la Fase 5 (`POST
  api/contabilidad/postear-costo-inventario`); hasta que se ejecute, el libro contable no refleja el costo de ventas de las
  facturas. El posteo exige igualmente el setup de inventario para que el batch pueda contabilizarlas después.
- **Descuento de línea neto, sin cuenta de descuento.** `ImporteLinea` va neto y Ventas se acredita neto;
  `CuentaDescuentoVentas` (y `CuentaDescuentoVentasAsync` del derivador) sigue sin usarse.
- **Números sin prefijo.** Las facturas se numeran `00000001`, `00000002`… (formato de siembra de las series), igual que
  borradores y cobros; `FV`/`COBRO` solo aparecen en el código de la serie, no en el número. Un número de factura y uno de
  cobro pueden coincidir: se distinguen por `TipoDocumento` (el índice único de `MovimientosCliente` es por
  `(TipoDocumento, NumeroDocumento)`).
- **Serie con huecos serializada.** `FV-BORR` permite huecos, pero el generador de números la bloquea igual que las series
  sin huecos, así que las altas de borradores concurrentes se serializan (aceptado: el alta es corta).
- **Guardas check-then-act aceptadas.** Las guardas de uso nuevas o ampliadas (borrado de socios con documentos,
  `SocioNegocioUsoService`; grupos y cuentas usados por borradores/facturas, `GrupoContableUsoService` y
  `CuentaContableUsoService`) son consultas sin bloqueo antes del borrado lógico, el mismo patrón de
  las fases anteriores; el posteo revalida todo bajo lock y rechaza con error explícito, nunca escribe a medias. El alta y
  la edición de borradores toman `FOR SHARE` del socio desde la fix wave final (Ruling DF-4, commit `52e3249`).
- **Unidad de línea siempre la base del producto (en la UI).** La página del borrador envía siempre la unidad base del
  producto (factor 1) y no ofrece unidades alternativas. La API acepta `UnidadMedidaId` opcional (por defecto la base) y
  congela el factor de conversión como en los diarios, pero ese camino no tiene UI ni runtime verificado en esta fase.
- **RESUELTO en la fix wave final (commit `52e3249`): cantidades con más decimales que la unidad base.** Antes, una línea
  de 2.5 UND (`Decimales = 0`) se facturaba por 2.5 y salía del inventario como −3 (redondeo en
  `ConversionUnidadMedidaService`); afectaba también a los diarios (Fase 4). Ahora se RECHAZA, nunca se redondea:
  `IConversionUnidadMedidaService.ObtenerConversionAsync` + `ConversionUnidadMedida.ConvertirExacta` (factor congelado
  redondeado a 6, el mismo que se guarda en la línea y en el movimiento) → `conversion.cantidad_no_exacta` en
  `LineaFacturaVentaBorradorReglas` y `LineaDiarioReglas` (400 en `Cantidad` con los decimales admitidos), en la
  revalidación del posteo de facturas y diarios (`Lineas[n].Cantidad`) y `inventario.cantidad_invalida` en
  `RegistrarAsync` como red final. Los movimientos ya escritos antes de este commit con cantidades redondeadas (si los
  hubiera en una base real) no se corrigen: las vistas de la Fase 7 los mostrarán tal cual.
- **Resueltos también en `52e3249`:** guarda de mismo socio en `AplicarPagoCommandHandler` antes del bloqueo del socio (con
  test de API); `Campo` vacío en `cobro.serie_invalida` y `factura.serie_invalida`; `Campo` de los errores de
  `IRegistroContable` sin el índice de pata; la derivación salta las líneas con error; `FOR SHARE` de los socios en el alta
  y el cambio de socio de borradores (Ruling DF-4); tests de concurrencia con productos distintos, cuentas de IVA
  distintas, facturar-a bloqueado y unidad borrada; orden global de locks documentado en las desviaciones de la Fase 6.
  Siguen diferidos los menores: N+1 en `RecalcularIvaLineasAsync`, número de `COBRO` con la fecha de hoy, búsqueda de
  productos en cada GET de la página de borrador, mensajes "no encontrado" cuando lo que falló fue la carga, duplicación
  de campos de línea/buscador con los diarios, modo oscuro.
- **M-7: CxC congelada de la factura vs. CxC vigente del pago.** La factura congela la CxC del grupo de cliente contable
  de su cabecera y el pago usa la CxC del grupo VIGENTE del socio. Si el grupo cambia entre ambos, aplicar el pago a la
  factura deja el libro de clientes cuadrado, pero en contabilidad quedan dos CxC con saldo contrario (una deudora y otra
  acreedora por el mismo importe). Se resolverá con una reclasificación de CxC futura (asiento entre ambas cuentas al
  aplicar o al cambiar el grupo).
- **Mensaje del trigger ante `TRUNCATE`** (preexistente, Fase 3): `libro_inventario_append_only()` responde "UPDATE no
  permitido" también a un `TRUNCATE` (no distingue `TG_OP = 'TRUNCATE'`). El rechazo es correcto; solo el texto confunde.
- **Down destructivo.** El `Down` de `AddFacturasVentaYLibroClientes` borra facturas posteadas y libro de clientes, y no
  revierte lo que el posteo escribió en inventario ni en el libro contable (que pertenecen a fases anteriores).

**Lo que la Fase 7 (vistas de movimientos) debe reutilizar, no reinventar:**

- **Facturas posteadas:** `IFacturaVentaReadRepository` (`ListAsync` paginado con `FacturaVentaSearchCriteria`,
  `GetByNumeroAsync` con líneas y líneas de IVA) detrás de `GET api/facturas-venta` y `GET api/facturas-venta/{numero}`.
- **Movimientos y saldo de cliente:** `IMovimientoClienteReadRepository` — `ListAsync` paginado con importe restante
  derivado y marca de abierto (filtros desde/hasta/soloAbiertos), `ListAbiertosAsync` y `GetSaldoAsync` — expuestos en
  `GET api/clientes/{id}/movimientos`, `/movimientos-abiertos` y `/saldo`. `MovimientosCliente.FechaVencimiento` ya está
  en el libro: el estado de cuenta por tramos de antigüedad se puede derivar de restante × vencimiento sin tocar esquema.
- **Movimientos contables:** `IContabilidadReadRepository.ListMovimientosAsync` / `ListRegistrosAsync` (paginados, `GET
  api/contabilidad/movimientos` y `/registros`); el balance de comprobación es una agregación de `MovimientosContables`
  por cuenta (`Debito`/`Credito`/`Importe` ya separados por el CHECK `CK_MovimientosContables_DebitoCredito`).
- **Libro de inventario:** `IConsultaInventario` (`ExistenciaAsync`, `ExistenciasPorAlmacenAsync`, `CostoPromedioAsync`)
  y `GET api/productos/{id}/existencias`, con los índices del libro de `AddIndicesLibroInventario` y
  `AddIndiceExistenciaProducto`. No hay aún un listado paginado de `MovimientosProducto`/`MovimientosValor`: la
  Fase 7 lo crea con el mismo patrón de repositorio Dapper.
- **Patrón de listados:** repositorios Dapper con `PageRequest`/`PagedResult`, `ColumnasPermitidas` (lista blanca de
  columnas de orden/filtro, citadas sin alias sobre una subconsulta aplanada) y `FilterExpressionBuilder`; en Blazor,
  filtros GET por query string con `[SupplyParameterFromQuery]` (`int?` para enums, orden explícito porque
  `PageRequest.Descendente` es `true` por defecto), como `FacturasVenta.razor` y `DiariosInventario.razor`.
