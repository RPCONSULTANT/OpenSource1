# Fase 7 — Vistas de movimientos · Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** vistas de consulta sobre los libros ya construidos (inventario, clientes, contabilidad): movimientos de
producto con saldo acumulado, movimientos de valor, existencias y valor por almacén, movimientos de cliente, estado de
cuenta por antigüedad, movimientos contables y balance de comprobación.

**Architecture:** solo lectura. Cada vista es una consulta Dapper paginada con allow-list de columnas (`ColumnasPermitidas`,
orden estable por `Id`), expuesta por la API con `CanConsult` y consumida por una página Blazor Static SSR con filtros por
formulario GET y paginación por query string. Todos los saldos se derivan (D1); nada se almacena.

**Tech Stack:** .NET 10, Dapper, MediatR 13, Blazor Static SSR, xUnit, Docker/Postgres 17.

**Spec:** [`docs/superpowers/specs/2026-09-12-axionerp-erp-modules-design.md`](../specs/2026-09-12-axionerp-erp-modules-design.md), sección "Fase 7", más el bloque "Desviaciones acordadas durante la ejecución de la Fase 7" que añade la Task 7.1. Pendientes heredados: sección "Resultado y pendientes que hereda la Fase 7" de [`2026-09-28-fase-6-facturacion.md`](2026-09-28-fase-6-facturacion.md).

## Global Constraints

- TFM `net10.0`; nullable e implicit usings. Solución `test.slnx`. Sin paquetes nuevos, sin CPM. PostgreSQL 15+.
- Blazor Static SSR: sin `@rendermode`, `@onclick`, `@bind` interactivo ni JS nuevo; filtros con formularios GET y
  paginación por query string; `[SupplyParameterFromQuery]` no admite enums (usar `int?`); `PageRequest.Descendente` es
  `true` por defecto (fijar el orden); fechas con `EntradaFecha`; importes con el formato de las páginas existentes;
  selects cargados de la API con el valor vigente; mensajes reales de la API; si la API falla, la página responde 200 con
  un aviso.
- Sin escrituras: ninguna consulta de esta fase hace INSERT/UPDATE/DELETE; no hay migraciones salvo índices de lectura
  justificados con `EXPLAIN ANALYZE` (y entonces con `ApplicationDbContextModelTests` y sonda vacía).
- Consultas: SQL parametrizado; columnas de orden solo desde `ColumnasPermitidas`; orden estable (`, "Id"`); filtros por
  rango de fechas inclusivos; paginación con total. Rendimiento objetivo < 200 ms con 100 000 movimientos por vista
  paginada (medido con datos generados, `EXPLAIN ANALYZE` en el informe).
- Permisos: todas las vistas `CanConsult`.
- Errores: `Result`/`Error`; parámetros inválidos → 400 con `Campo`; entidad de ruta inexistente → 404.
- Tests contra Postgres real (`DOCKER_CONTEXT=default` solo como variable de entorno); suite completa al final de cada
  task (hoy 1204). 6 avisos CS0618 preexistentes; no añadir avisos.
- Commits LOCALES autorizados en `feat/erp-fase-7-vistas` (desde `feat/erp-fase-6-facturacion`). Nunca merge ni push. No
  tocar `appsettings*.json`.
- Runtime obligatorio (API + Blazor + Postgres reales) para cada página nueva, con evidencia en el informe.

## Review Focus

1. **Saldo acumulado** de movimientos de producto: correcto a través de páginas (la página 3 empieza con el saldo que
   dejó la 2) y con filtros de fecha (el saldo inicial incluye lo anterior al rango). Test en Task 7.2.
2. **Antigüedad:** una factura parcialmente pagada entra en su tramo por su restante, no por su original; los pagos no
   aplicados restan del total del cliente (tramo "sin aplicar"); la fecha de corte excluye movimientos posteriores. Test
   en Task 7.3.
3. **Balance de comprobación** cuadra (suma de saldos = 0) para cualquier rango; saldo inicial + débitos − créditos =
   saldo final por cuenta; cuentas de resultado y balance tratadas igual (sin cierre de ejercicio). Test en Task 7.4.
4. **Existencia y valor por almacén** coinciden con `IConsultaInventario` y con el saldo de la cuenta de inventario tras el
   batch de costo. Test en Task 7.2.
5. **Filtros inválidos** (fechas invertidas, página 0, tamaño > máximo, ordenar por columna no permitida) → 400 o valor
   por defecto documentado, nunca 500. Test en cada task.

---

## Task 7.1 — Desviaciones de diseño (solo docs; la hace el controlador)

```markdown
**Desviaciones acordadas durante la ejecución de la Fase 7:**

- **Saldo acumulado** en movimientos de producto solo cuando el filtro fija un producto (y opcionalmente un almacén): es
  el único caso en que tiene sentido; se calcula con una función de ventana ordenada por `(FechaRegistro, Id)` más el
  saldo anterior al rango (`desde`) y a la página. Sin producto, la columna no se devuelve.
- **Estado de cuenta:** antigüedad por `FechaVencimiento` respecto a una **fecha de corte** (por defecto hoy): corriente
  (no vencido), 1-30, 31-60, 61-90, 90+ días vencidos, sobre el **restante a la fecha de corte** (detalle con
  `FechaRegistro <= corte`); los pagos con restante negativo se muestran como "sin aplicar" y restan del total.
- **Balance de comprobación** por rango de fechas: saldo inicial (antes de `desde`), débitos y créditos del rango y saldo
  final, solo cuentas de Posteo con movimientos o saldo; sin cierre de ejercicio (las cuentas de resultado acumulan).
- **Valor de inventario** por producto y almacén = `SUM(ImporteCosto)` de los movimientos de valor hasta la fecha;
  existencia = `SUM(Cantidad)` de movimientos de producto (misma derivación que `IConsultaInventario`).
- **Limpieza heredada:** `ConvertirABaseAsync` y `ObtenerFactorAsync` (redondean) se retiran de
  `IConversionUnidadMedidaService` si no tienen llamadores.
```

---

## Task 7.2 — Vistas de inventario

**Files:**
- Create: `src/OpenSource1.Application/Features/Inventario/Consultas/**` (queries y DTOs), `src/OpenSource1.Infrastructure/Data/Queries/DapperInventarioConsultasRepository.cs`, endpoints en `InventarioController` (`GET api/inventario/movimientos-producto`, `movimientos-valor`, `existencias`)
- Create: páginas `Pages/MovimientosProducto.razor`, `Pages/MovimientosValor.razor`, `Pages/Existencias.razor` + clientes HTTP + menú "Inventario"
- Test: API contra Postgres (movimientos sembrados con `IRegistroMovimientosInventario` y diarios)

Movimientos de producto: filtros `productoId`, `almacenId`, `desde`, `hasta`, `tipoMovimiento` (int), `tipoOrigen` (int),
`numeroDocumento`; columnas: fecha, tipo, documento, almacén, cantidad, unidad, restante (entradas), origen; con
`productoId`: `saldoAcumulado`. Movimientos de valor: mismos filtros (sin restante) + `soloAjustes`; columnas: cantidad
valorada, importe costo, costo por unidad, importe venta, ajuste, tipo de valor, contabilizado (`ImporteCostoPosteadoContabilidad
= ImporteCosto`). Existencias: filtros `almacenId`, `productoId`/texto, `fecha` (por defecto hoy), `soloConExistencia`;
columnas: producto, almacén, existencia, valor, costo medio (valor/existencia si existencia > 0).

- [ ] Tests (Review Focus 1, 4, 5; paginación y totales; tipos de movimiento) → implementar → `EXPLAIN ANALYZE` con 100 000
  movimientos → runtime de las tres páginas → suite → commit `feat: vistas de movimientos de producto, de valor y existencias por almacen`.

---

## Task 7.3 — Vistas de clientes

**Files:**
- Modify/Create: `ClientesController` (`GET api/clientes/{id}/movimientos` ya existe: añadir filtros `desde`, `hasta`, `soloAbiertos`, `tipoDocumento` si faltan), `GET api/clientes/estado-cuenta?fechaCorte=&socioId=` (todos los clientes o uno) con tramos
- Create: páginas `Pages/MovimientosCliente.razor` (por cliente, con restante y abierto) y `Pages/EstadoCuenta.razor` (tabla por cliente con tramos y total; enlace al detalle)
- Test: API contra Postgres (facturas posteadas con el motor real, pagos y aplicaciones)

- [ ] Tests (Review Focus 2, 5; varios clientes; fecha de corte anterior a un pago; factura vencida parcialmente pagada) →
  implementar → runtime → suite → commit `feat: movimientos de cliente y estado de cuenta por antiguedad`.

---

## Task 7.4 — Vistas contables

**Files:**
- Modify/Create: `ContabilidadController` (`GET api/contabilidad/movimientos` ya existe: completar filtros `cuentaId`, `desde`, `hasta`, `tipoDocumento`, `numeroDocumento`, `socioId`), `GET api/contabilidad/balance-comprobacion?desde=&hasta=`
- Create: páginas `Pages/MovimientosContables.razor` (por cuenta y rango, con enlace al registro) y `Pages/BalanceComprobacion.razor` (saldo inicial, débitos, créditos, saldo final; totales que cuadran)
- Test: API contra Postgres (asientos de facturas, cobros y batch de costo)

- [ ] Tests (Review Focus 3, 5) → implementar → runtime → suite → commit `feat: movimientos contables y balance de comprobacion`.

---

## Task 7.5 — Limpieza y cierre de la Fase 7 (y del proyecto de módulos ERP)

- [ ] Retirar `ConvertirABaseAsync`/`ObtenerFactorAsync` si no tienen llamadores (tests adaptados).
- [ ] `ApplicationDbContextModelTests`, `has-pending-model-changes`, `grep` TODO, suite completa.
- [ ] Sección "Resultado del proyecto y pendientes" al final de este plan: resumen de las Fases 0-7, requisitos de
  despliegue (PostgreSQL 15+), limitaciones aceptadas y pendientes acumulados de todas las fases.
