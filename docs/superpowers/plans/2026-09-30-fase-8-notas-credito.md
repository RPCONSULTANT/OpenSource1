# Fase 8 — Notas de crédito, reglas de importe, fechas permitidas y limpieza · Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** notas de crédito de venta ligadas a facturas (con devolución de inventario opcional, asiento inverso y
aplicación automática), `Producto.CostoUnitario` siempre al día, nueva regla de importes (precio 0 e importe 0
bloqueados, 100 % de descuento permitido, factura de total 0 solo con inventario), fechas de registro permitidas (general
y por usuario) validadas en todos los posteos, y retirada definitiva de los módulos obsoletos Entradas y AppSettings.

**Architecture:** misma arquitectura que las Fases 3-7. Las notas de crédito reutilizan el patrón completo de la factura:
borrador (maestro con `xmin`), documento posteado append-only con PK `Numero`, IVA agrupado con `CalculadoraIvaFactura`,
inventario vía `IRegistroMovimientosInventario`, libro de clientes vía `IRegistroMovimientosCliente` y asiento vía
`IRegistroContable`. Las fechas permitidas son un servicio `IValidadorFechaRegistro` que consultan todos los comandos de
posteo antes de escribir.

**Tech Stack:** .NET 10, EF Core 10 + Npgsql, Dapper, MediatR 13, Blazor Static SSR, xUnit, Docker/Postgres 17.

**Spec:** [`docs/superpowers/specs/2026-09-12-axionerp-erp-modules-design.md`](../specs/2026-09-12-axionerp-erp-modules-design.md), sección nueva "Fase 8" que añade la Task 8.1. Pendientes heredados: sección "Resultado del proyecto y pendientes" de [`2026-09-29-fase-7-vistas.md`](2026-09-29-fase-7-vistas.md).

## Decisiones del usuario (2026-09-27)

- Factura con total 0 por 100 % de descuento en todas sus líneas: **se permite**; se postea el documento y sale el
  inventario, sin movimiento de cliente ni asiento.
- Notas de crédito **siempre ligadas a una factura posteada**; cantidades ≤ lo facturado menos lo ya acreditado; IVA del
  documento original; devolución de inventario opcional por línea al costo de la venta; asiento inverso y aplicación
  automática a la factura.
- Fechas permitidas: **rango general + excepciones por usuario** asignadas por el administrador; se valida en todos los
  posteos (diarios, facturas, notas de crédito, cobros y aplicaciones).
- Entradas y AppSettings: **se retiran código, API, páginas y tests, y se borran sus tablas** con una migración.

## Global Constraints

- Las mismas de las Fases 5-7: TFM `net10.0`; sin paquetes nuevos ni CPM; PostgreSQL 15+; Blazor Static SSR (sin
  `@rendermode`, `@onclick`, `@bind` ni JS nuevo; `<EditForm>` de un paso con `FormName` estático; formularios de acción
  siempre presentes en el árbol aunque el estado o el permiso no permitan mostrarlos; `ConfirmDialog` con
  `?deleteId=`/`?postear=true`; selects con el valor vigente; mensajes reales de la API con número de línea;
  `[SupplyParameterFromQuery]` sin enums; orden explícito en `PageRequest`); `Result`/`Error` con `.no_encontrado`→404,
  `.conflicto`→409, resto→400; errores de líneas con `Campo = "Lineas[<NumeroLinea>].<Campo>"`.
- Libros y documentos posteados append-only con trigger, sin soft delete ni `xmin` ni `IAggregateRoot`, protegidos por
  `LibroInventarioSinUpdateNiDeleteTests`. Escritura en inventario solo por `IRegistroMovimientosInventario`; en clientes
  por `IRegistroMovimientosCliente`; en contabilidad por `IRegistroContable`; cuentas siempre derivadas (D4) o congeladas.
- Comandos de posteo: `IRequest`, UNA transacción, `InvalidOperationException` si ya hay una activa. Orden global de
  locks: documento/borrador → movimientos de cliente (FOR UPDATE por Id) → socios (FOR SHARE) → productos (ordenados) →
  series → almacenes compartidos → cuentas (FOR SHARE por Id) → serie CONTAB.
- Cantidades exactas: una cantidad base con más decimales que la unidad base se rechaza (`conversion.cantidad_no_exacta`).
- Migraciones con `dotnet ef migrations add`; sin `xmin` espurio; `ApplicationDbContextModelTests` y sonda vacía.
- Tests contra Postgres real (`DOCKER_CONTEXT=default` solo como variable de entorno); "foto" de nada escrito (filas,
  números de serie, secuencias) en cada fallo de posteo; suite completa al final de cada task (hoy 1289). La build tiene 6
  avisos CS0618 hasta la Task 8.2, que los elimina; después, 0 avisos.
- Commits LOCALES autorizados en `feat/erp-fase-8-notas-credito` (desde `feat/erp-fase-7-vistas`). Nunca merge ni push.
  No tocar `appsettings*.json`.
- Permisos: consultar CanConsult; alta CanAdd; modificar CanModify; borrar CanDelete; postear notas de crédito
  CanModify; **configurar fechas permitidas: solo Administrador** (política nueva `CanAdministrar` o la existente que
  distinga al Administrador — comprobar en `PermisosPorRol`).
- Runtime obligatorio (API + Blazor + Postgres reales) para cada página nueva o modificada.

## Review Focus

1. **Nota de crédito que excede lo facturado** (misma línea acreditada dos veces, o dos notas concurrentes sobre la misma
   factura): la segunda falla, nunca se acredita de más. Test en Task 8.6.
2. **Devolución de inventario al costo de la venta:** el valor que vuelve es exactamente el que salió en esa línea
   (proporcional a la cantidad devuelta), y el batch de costo lo contabiliza contra costo de ventas. Test en Task 8.6.
3. **Saldo del cliente** tras factura + nota de crédito parcial (aplicada automáticamente) + pago = derivado exacto; el
   estado de cuenta y el balance cuadran. Test en Task 8.6.
4. **Fecha fuera del rango permitido** del usuario (o del general si no tiene excepción) en cualquier posteo → error
   `registro.fecha_no_permitida` y nada escrito. Test en Task 8.5.
5. **Factura de total 0** (todas las líneas al 100 % de descuento): se postea con inventario y sin cliente ni asiento; una
   línea con precio 0 o con importe 0 sin 100 % de descuento se rechaza. Test en Task 8.4.

---

## Task 8.1 — Spec de la Fase 8 (solo docs; la hace el controlador)

Añadir al spec una sección "Fase 8 — Notas de crédito, reglas de importe, fechas permitidas y limpieza" con las
decisiones del usuario y el diseño de las Tasks 8.2-8.7. Commit junto con este plan.

---

## Task 8.2 — Retirada de Entradas y AppSettings

**Files:** eliminar `Core/Entities/Entrada.cs`, `AppSetting.cs`; `Application/Features/Entradas/**`, `Features/AppSettings/**`, `Services/IAppSettingService.cs`; `Infrastructure/Services/Settings/AppSettingService.cs`, repositorios Dapper, registros DI, mapeos en `ApplicationDbContext`; controladores de la API; páginas, componentes, clientes HTTP y entradas de menú de Blazor; tests. Migración `RetirarEntradasYAppSettings` (`DropTable` de ambas; `Down` las recrea vacías con su esquema).
- [ ] `grep` sin referencias a `Entrada`/`AppSetting` fuera de migraciones antiguas; build con **0 avisos** (desaparecen los 6 CS0618); `ApplicationDbContextModelTests`, sonda vacía, suite completa; runtime: el menú ya no muestra esos módulos y sus rutas dan 404 → commit `refactor: retira definitivamente los modulos obsoletos Entradas y AppSettings`.

---

## Task 8.3 — `Producto.CostoUnitario` siempre al día

Hoy la proyección solo se escribe al crear el producto y en `AjustarCostoMovimientos`.
- `RegistroMovimientosInventario.RegistrarAsync`: tras registrar cualquier movimiento, actualiza `Producto.CostoUnitario`
  al costo promedio vigente del producto a la **última fecha con movimientos** (`V/Q` sobre todos sus movimientos de valor
  si `Q > 0`; si `Q <= 0`, conserva el valor), con un `UPDATE` de una sola columna dentro de la misma transacción (el
  producto ya está bloqueado por el advisory lock).
- `AjusteCostoInventario` sigue actualizándolo con el promedio final ajustado (misma definición).
- Migración de datos `RecalcularCostoUnitario`: recalcula la proyección de todos los productos con movimientos (SQL, sin
  tocar los libros).
- [ ] Tests: entrada a un costo nuevo → `CostoUnitario` actualizado; salida → sigue siendo el promedio; entrada
  retroactiva → proyección coherente con la última fecha; producto sin movimientos conserva su valor; migración con datos.
  → commit `fix: el costo unitario del producto se mantiene al dia con cada movimiento de inventario`.

---

## Task 8.4 — Nueva regla de importes en facturas

- Líneas Producto y CuentaContable: `PrecioUnitario > 0` obligatorio (`factura.precio_invalido`); `ImporteLinea = 0` solo
  si `PorcentajeDescuentoLinea = 100` (si no, `factura.importe_invalido`). Validación al capturar (borrador) y al postear.
- Se elimina `factura.importe_cero` del posteo: una factura con `ImporteTotal = 0` se postea con documento, líneas, líneas
  de IVA (base 0) e inventario, **sin** movimiento de cliente ni asiento (`RegistroContableId` null, ya admitido por el
  esquema). El resto de validaciones (setups incluidos) se mantiene.
- UI: el precio es obligatorio; ayuda "para regalar, use 100 % de descuento".
- [ ] Tests (Review Focus 5; factura mixta con una línea al 100 % y otras con importe; batch de costo contabiliza la
  salida de una factura de total 0) → commit `feat: precio e importe cero bloqueados con descuento del 100 por ciento permitido`.

---

## Task 8.5 — Fechas de registro permitidas

**Files:** entidades `ConfiguracionRegistro` (fila única: `PermitirRegistroDesde`, `PermitirRegistroHasta` nulables) y
`ConfiguracionRegistroUsuario` (`UsuarioId` único — referencia lógica a Identity, `NombreUsuario` desnormalizado,
`PermitirRegistroDesde`, `PermitirRegistroHasta`); migración con la fila general vacía (sin límites); servicio
`IValidadorFechaRegistro.ValidarAsync(DateOnly fecha, CancellationToken)` → `Result` con `registro.fecha_no_permitida`
(mensaje con el rango vigente) usando `IUsuarioActual`: si el usuario tiene fila propia manda su rango, si no el general;
límites nulos = sin límite; API `api/configuracion/fechas-registro` (general GET/PUT; usuarios CRUD) solo Administrador;
página Blazor de configuración (general + tabla de usuarios; select de usuarios desde el módulo de usuarios existente).
- Validar en: `PostearLoteDiario`, `PostearFacturaVenta`, posteo de notas de crédito (Task 8.6), `RegistrarPagoCliente`,
  `AplicarPago` (fecha de la aplicación). No en el batch de costo (sistema) ni en `AjustarCostoMovimientos`.
- [ ] Tests (Review Focus 4 en cada comando: dentro, fuera, sin rango, excepción de usuario más amplia y más estrecha que la
  general; nada escrito); roles (Supervisor/Ejecutor no configuran); runtime → commit `feat: fechas de registro permitidas generales y por usuario`.

---

## Task 8.6 — Notas de crédito de venta (backend)

**Files:** entidades `NotaCreditoVentaBorrador`, `LineaNotaCreditoVentaBorrador`, `NotaCreditoVenta` (PK `Numero`),
`LineaNotaCreditoVenta`, `LineaIvaNotaCreditoVenta`; series `NC-BORR` (con huecos) y `NC` (sin huecos); migración con
triggers append-only para las tablas posteadas; `TipoDocumentoContable.NotaCreditoVenta`, `TipoOrigenMovimiento.NotaCreditoVenta`,
`TipoDocumentoInventario.NotaCreditoVenta` (valores nuevos, sin renumerar); comandos y API `api/notas-credito-venta`
(borradores CRUD con `Xmin`; crear borrador desde una factura; líneas; totales; `postear`; posteadas GET).

Reglas:
- Borrador creado desde una factura posteada (`FacturaVentaNumero` obligatorio): copia cabecera (socios, snapshot, grupos,
  CxC congelada de la factura) y ofrece sus líneas. Cada línea de nota referencia `LineaFacturaVentaId` y lleva
  `Cantidad` ≤ facturada − ya acreditada (por notas posteadas) y `DevolverInventario` (solo líneas Producto). Precio,
  descuento, IVA (identificador, porcentaje, cuenta) y grupos se copian de la línea original (no editables).
  `ImporteLinea = ROUND(Cantidad × Precio, 2) − ROUND(Cantidad × Precio × %/100, 2)`.
- Posteo (una transacción, patrón de `PostearFacturaVenta`): bloquea el borrador, la factura (sus movimientos de cliente
  FOR UPDATE) y los productos; revalida cantidades pendientes de acreditar bajo el lock (Review Focus 1); valida fecha
  permitida; número `NC`; IVA agrupado; documento posteado; por línea con devolución: entrada de inventario
  `TipoMovimiento = Venta` con cantidad positiva al costo unitario de la salida original (`-ImporteCosto / CantidadBase`
  del movimiento de valor de esa línea; Review Focus 2); `MovimientoCliente` NotaCredito con `ImporteOriginal = −total`
  y detalle `ImporteInicial`, y **aplicación automática** a la factura por min(total, restante de la factura);
  asiento: débito Ventas por grupo (con el ajuste de redondeo), débito IVA por grupo, débito cuentas de líneas
  CuentaContable, crédito CxC congelada de la factura; borra el borrador. Nota de total 0 → rechazada
  (`nota_credito.importe_cero`).
- Guardas de uso: una factura con notas no cambia (ya es inmutable); borrar un socio con notas → 409.
- [ ] Tests (Review Focus 1, 2, 3; nota parcial y total; dos notas concurrentes; devolución con y sin inventario; setup
  faltante → nada escrito; fecha no permitida; cantidad no exacta) → commit `feat: notas de credito de venta ligadas a facturas con devolucion de inventario y asiento inverso`.

---

## Task 8.7 — Páginas de notas de crédito e integración en vistas

- Páginas: borradores de notas de crédito (crear desde una factura: botón en la ficha de la factura posteada), borrador
  con sus líneas (cantidad y devolución por línea), posteo, notas posteadas (lista y detalle); la ficha de factura muestra
  sus notas y lo acreditado; menú "Ventas".
- Vistas de la Fase 7: movimientos de cliente y estado de cuenta muestran las notas (tipo NotaCredito); movimientos de
  producto muestran las devoluciones; comprobar que `CoherenciaVistasApiTests` sigue cuadrando con una nota (ampliarlo).
- [ ] Runtime completo → suite → commit `feat: paginas de notas de credito de venta e integracion en las vistas`.

---

## Task 8.8 — Cierre de la Fase 8

- [x] Cadena de migraciones desde vacía y desde el final de la Fase 7 con datos (incluidas filas de Entradas/AppSettings
  que se borran); Down y vuelta a HEAD.
- [x] `ApplicationDbContextModelTests`, `has-pending-model-changes`, build con 0 avisos, suite completa.
- [x] Actualizar la sección de pendientes del proyecto (plan de la Fase 7) marcando lo resuelto en esta fase.

---

## Resultado de la Fase 8

Commits: `1e406cf` (plan y spec), `68e1481` (8.2), `c790a51` y `e70914a` (8.3), `8fe7ba0` (8.4), `2e4c348` (8.5),
`437161b` y `998a7b2` (8.6), `7ebfc4e` (8.7), `1f25046` (8.8), `f97a1d8` (ola de arreglos final). Suite 1361/1361,
build con 0 avisos. Residuales: sección "Resultado de la Fase 8" del plan de la Fase 7.

Decisiones tomadas durante la ejecución:
- **FA:** el grep de aceptación de la retirada también encuentra la migración y el vocabulario de inventario; aceptado.
- **FB:** `CostoUnitario` = V/Q sobre todo el libro; se conserva con Q ≤ 0 o fuera de [0, 1e14); tests ajustados.
- **FC:** el ajuste de costo usa como respaldo el último promedio ajustado cuando Q ≤ 0.
- **FD:** la API conserva el precio por defecto del producto si no se envía (rechazado si es 0); la UI lo exige.
- **FE:** una nota de crédito de total 0 solo se permite si todas sus líneas devuelven inventario.
- **FF:** aplicación sin fecha valida hoy (UTC); las sesiones Blazor previas obtienen `CanAdministrar` al volver a entrar.
- **FG/FJ/FL:** el validador de fechas falla cerrado si falta la fila general (también con excepción de usuario; 500).
- **FH:** diseño de la nota: `FOR UPDATE` de la factura, costo de devolución = costo de la salida + ajustes, IVA y CxC
  congelados de la factura, nota no anterior a su factura, socio bloqueado impide postear.
- **FI:** topes: nunca acreditar de más en importe, IVA ni costo; la nota que agota la factura toma el remanente exacto.
- **FK:** residuales de la revisión final aceptados y documentados.
