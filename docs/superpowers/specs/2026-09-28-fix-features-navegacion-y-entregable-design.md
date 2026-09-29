# Fix-Features: navegación, acciones por página y entregable Parte 1 — Diseño

Rama: `Fix-Features` (desde `8e81a41`). Fecha: 2026-09-28.

## Decisiones del usuario

| Tema | Decisión |
|---|---|
| Búsqueda | Módulos **y** registros (clientes, productos, documentos). |
| Implementación de la búsqueda | Híbrida: página de resultados en servidor (`/buscar?q=`) **y** paleta Ctrl+K con resultados al escribir, ambas en esta rama. |
| "Agregar" | **D**: página-tarjeta propia (`/x/nuevo`), en todo el sistema. |
| Acciones de página | **B**: barra con `+ Nuevo`, `Crear ▾`, `Ver ▾`, `Editar`, `Eliminar`; en vista Tarjetas, acciones rápidas + `⋯`. |
| Dashboard | **A**: tarjetas de grupo en el inicio; cada grupo abre `/modulos/{grupo}` con sus enlaces e indicadores. |
| Orden | Fase A (estructura) → Fase B (patrón de página) → Fase C (acciones contextuales); Fase D (entregable) en paralelo. |
| Ejecución | Multiagente (subagent-driven), en segundo plano, con revisión por tarea y revisión final. |

## Restricciones globales

- Blazor **Static SSR**: sin `@rendermode`, `@onclick` ni `@bind`. GET/POST con `[SupplyParameterFromQuery]`/`[SupplyParameterFromForm]`, antiforgery y clientes tipados. JS solo vanilla y progresivo (la página funciona sin él); re-inicialización con `Blazor.addEventListener('enhancedload', …)`.
- El JWT nunca llega al navegador (cookie HttpOnly + sesión de servidor). Todo endpoint JSON del host Blazor llama a la API desde el servidor.
- Permisos de la UI iguales a los de la API (`CanConsult`, `CanAdd`, `CanModify`, `CanDelete`, `CanAdministrar`, roles). Una acción sin permiso no se muestra; un POST forzado recibe el 403 real de la API.
- Lecciones vigentes: formularios de acción siempre en el árbol (EditForm vacío con el mismo `FormName`); `[SupplyParameterFromQuery]` sin enums (int?); `PageRequest.Descendente` true por defecto.
- Sin cambios de dominio ni de migraciones salvo lo que pida la búsqueda (solo lectura, Dapper). No tocar `appsettings*.json`.
- Tests: cada task con tests (bUnit/SSR o integración API según capa); suite completa verde y build con 0 avisos al cerrar cada fase.

## Fase A — Estructura común

### A1. Registro de módulos (fuente única)
`src/OpenSource1.Blazor/Navigation/`: `GrupoModulo(Clave, Titulo, Icono, Descripcion, Orden)` y `Modulo(Clave, Grupo, Titulo, Descripcion, Ruta, Icono, Politica?, Roles?, PalabrasClave[])`, más `IRegistroModulos` que devuelve los módulos visibles para el `ClaimsPrincipal` (evaluando política/rol con `IAuthorizationService`). Menú, páginas de grupo, búsqueda de módulos y paleta leen de aquí. Un test verifica que cada `@page` de listado aparece en el registro y que cada `Ruta` del registro existe.

Grupos (alineados con el Paso 8 del entregable: Inicio, Clientes, Productos, Inventario, Ventas, Facturación, Reportes, Administración, más Contabilidad y Configuración):

| Grupo | Módulos |
|---|---|
| Clientes | Clientes, Panel de clientes |
| Productos | Productos, Categorías de producto, Panel de productos |
| Inventario | Existencias, Almacenes, Diarios de inventario, Movimientos de producto, Movimientos de valor |
| Ventas | Cobros, Movimientos de cliente, Estado de cuenta (CxC) |
| Facturación | Borradores de factura, Facturas, Borradores de nota de crédito, Notas de crédito |
| Contabilidad | Plan de cuentas, Movimientos contables, Balance de comprobación, Costo de inventario |
| Reportes | Reportería, Bitácora |
| Configuración | Términos de pago, Unidades de medida, Grupos contables, Grupos de cliente contable, Setups contables, Fechas de registro |
| Administración | Usuarios |

### A2. Menú lateral
`MainLayout.razor` pasa a renderizar grupos con `<details>` nativos desde el registro; abierto el grupo de la ruta actual (calculado en servidor). Se conserva el modo colapsado (cookie `axionerp-sidebar`), el tema y el bloque de sesión. El menú se extrae a `Components/Layout/NavMenu.razor`.

### A3. Barra superior y búsqueda en servidor
- Barra superior en todas las páginas autenticadas: caja "Buscar módulos, clientes, productos, documentos… (Ctrl+K)" = `<form method="get" action="/buscar">`.
- API: `GET api/busqueda?q=&limite=5` (`CanConsult`). Application: `BuscarGlobalQuery` → `IBusquedaGlobalRepository` (Dapper): hasta `limite` resultados por tipo, `ILIKE` con comodines escapados, sin borrados lógicos, sobre: socios (código, nombre, nombre comercial, RNC), productos (código, nombre), facturas posteadas (número, cliente), borradores de factura, notas de crédito y sus borradores (número). `q` recortado, 2–100 caracteres (si no, 400 con `Campo = q`). DTO: `{ tipo, id, titulo, subtitulo, ruta }` agrupado por tipo.
- Blazor: página `/buscar?q=` (SSR) con "Módulos" (del registro, filtro por título/palabras clave sin acentos) y un bloque por tipo con "ver todos" (enlace al listado filtrado).

### A4. Paleta Ctrl+K (mejora progresiva)
- `wwwroot/app.search.js`: Ctrl/Cmd+K o `/` abre una capa sobre la caja; módulos filtrados en el navegador desde una isla JSON (`<script type="application/json" id="modulos-data">`) renderizada en servidor con solo los módulos visibles al usuario; registros vía `GET /buscar/sugerencias?q=` (endpoint mínimo del host Blazor, `RequireAuthorization`, llama a `api/busqueda` desde el servidor con la sesión) con debounce 250 ms y cancelación de la petición anterior; flechas/Enter/Esc; Enter sin selección → `/buscar?q=`. Accesible (`role="dialog"`, `aria-activedescendant`). Sin JS, la caja sigue siendo el formulario GET.

### A5. Inicio y páginas de grupo
- `Home.razor`: se elimina el bloque hero azul con animación; franja compacta de KPIs actuales + rejilla de tarjetas de grupo (solo grupos con ≥1 módulo visible).
- `/modulos/{grupo}` (`ModuloGrupo.razor`): tarjetas de sus módulos (título, descripción, icono) y hasta 3 indicadores de endpoints existentes (si uno falla, se omite sin romper la página). Grupo inexistente → 404.

### A6. Flash azul
Causa: hero `bg-gradient-to-br from-brand-700…` con `animate-fade-in` en `Home.razor` y barra `#nav-progress-bar` en cada navegación. Solución: sin hero; la barra de progreso aparece solo si la navegación tarda > 150 ms, 2 px y color neutro de marca atenuado; sin animaciones de entrada a pantalla completa en páginas SSR. Verificación: captura Playwright de una navegación entre páginas sin cuadros azules.

## Fase B — Patrón de página

### B1. Componentes compartidos
- `PageToolbar.razor`: título, migas, `+ Nuevo` (href, permiso), menús `Crear ▾`/`Ver ▾` (`<details>` nativos) con `AccionPagina(Etiqueta, Icono, UrlConId, Permiso, RequiereSeleccion)`, `Editar`, `Eliminar` (usa el ConfirmDialog existente con `?deleteId=`). Sin selección, las acciones que la requieren se muestran deshabilitadas con `title` explicativo.
- `EntityFormPage.razor`: página-tarjeta de creación/edición (migas, tarjeta centrada, `Guardar`, `Cancelar` → vuelve a la lista con sus filtros vía `returnUrl` local validado).
- Selección: en vista Lista cada fila es un enlace a la misma lista con `?sel={id}` (conserva filtros/página); la fila seleccionada se resalta con radio visual y la barra habilita sus acciones con el id. En vista Tarjetas, cada tarjeta trae 2 acciones rápidas + `⋯` (`<details>`) con el resto.

### B2. Conversión de listados
Todos los listados con alta pasan a `+ Nuevo` → `/x/nuevo` y `Editar` → `/x/{id}/editar` usando `EntityFormPage` y los `*Fields.razor` existentes; se retiran los formularios de alta/edición en línea. Rutas antiguas (`/clientes/new`, `/productos/new`, `?editId=`) redirigen a las nuevas. Listados: Clientes, Productos, Categorías, Unidades de medida, Términos de pago, Almacenes, Cuentas contables, Grupos contables, Grupos de cliente contable, Setups contables, Diarios de inventario, Borradores de factura, Borradores de nota de crédito, Usuarios.

## Fase C — Acciones contextuales y documentos

| Página | Crear ▾ | Ver ▾ |
|---|---|---|
| Clientes (lista, tarjeta y ficha) | Factura (`/facturas-venta/nueva?socioId=`), Nota de crédito (`/notas-credito-venta/nueva?socioId=`: facturas posteadas del cliente con pendiente), Cobro (`/cobros/nuevo?socioId=`) | Ficha, Movimientos de cliente, Estado de cuenta, Cobros, Facturas del cliente |
| Productos (lista, tarjeta y ficha) | Ajuste en diario de inventario (`/diarios-inventario/nuevo?productoId=`: línea con el producto) | Ficha, Existencias, Movimientos de producto, Movimientos de valor |
| Facturas | Nota de crédito de la factura seleccionada | Detalle, Movimientos del cliente |
| Borradores de factura / nota | — | Abrir borrador, Cliente |

- `/facturas-venta/nueva`: página-tarjeta; con `socioId` precarga cliente (bloqueado a cambio explícito), término de pago y grupo del cliente, fecha de hoy; al guardar crea el borrador y redirige a su editor. Botón **"Nueva factura"** visible en la barra de Facturas y de Borradores para `CanAdd`.
- Si la API de listados no filtra por socio donde hace falta (p. ej. facturas por `socioId`), se añade el filtro en Application/Infrastructure con su test.

## Fase D — Entregable Parte 1 (en paralelo)

Carpeta `docs/entregable-parte-1/`, en el estilo del entregable 3 existente (**sin portada**, texto en español, párrafos justificados):

| Paso | Contenido y fuente |
|---|---|
| 1 Problema | Nombre (AxionERP), situación actual, objetivo general y específicos, usuarios (Administrador, Supervisor, Ejecutor), alcance, limitaciones, beneficios. |
| 2 Tecnología | ASP.NET Core 10 con C#: API REST con controladores MVC + Blazor Static SSR como capa de vistas; justificación (soporte LTS, rendimiento, tipado, ecosistema EF Core/Dapper, mantenibilidad) y por qué SSR en lugar de MVC con Razor Views. |
| 3 Arquitectura | Diagrama Usuario → Interfaz (Blazor) → Controlador (API) → Lógica (Application/MediatR) → Acceso a datos (EF Core + Dapper) → PostgreSQL; capas Core/Application/Infrastructure/Api/Blazor. |
| 4 Base de datos | Modelo ER generado del esquema real (script sobre `information_schema` → Mermaid → PNG/SVG vía Playwright), con tabla de correspondencia: Usuarios/Roles → `AspNetUsers`/`AspNetRoles`; Clientes → `SociosNegocio`; Categorías → `CategoriasProducto`; Inventario → `MovimientosProducto`/`MovimientosValor`/`Almacenes`; Ventas/DetalleVenta/Facturas → `FacturasVenta`/`LineasFacturaVenta` (+ borradores); CuentasPorCobrar → `MovimientosCliente`. PK, FK, tipos principales. |
| 5 Persistencia | Code First con migraciones EF Core (38), justificación; Dapper para lecturas. |
| 6–7 CRUD y búsquedas | Evidencias de Clientes, Productos, Categorías y Usuarios (crear, consultar, modificar, eliminar/desactivar) y búsquedas por varios criterios (productos por código, nombre, categoría, precio, estado) + búsqueda global nueva. |
| 8 Menú | Menú por grupos de la Fase A. |
| 9 Pruebas | Suite xUnit (≥1361) + suite E2E Playwright con capturas: altas, modificaciones, eliminación, validaciones, búsquedas, errores (API caída, 403, 404). |
| 10 Entrega | Índice de código fuente, documento, diagramas, capturas, presentación corta y guion de demostración. |

Artefactos: `documento-tecnico.md` (fuente) → `documento-tecnico.docx` (único formato de entrega, texto justificado); `presentacion.pptx` (8–10 diapositivas); `diagramas/arquitectura.svg|png`, `diagramas/modelo-er.svg|png`; `capturas/*.png` (Playwright, 1440×900, claro y una muestra en oscuro); `evidencias/` (resumen de tests, reporte E2E); `guion-demo.md`.

E2E: `tests/e2e/` (Node + `@playwright/test`, fuera de `test.slnx`) contra el stack de Docker (`POSTGRES_PORT` configurable); credenciales solo por variables de entorno; genera capturas y reporte HTML. Las capturas finales se toman al cerrar la Fase C; los textos (Pasos 1–5), diagramas y ER pueden hacerse desde el inicio.

## Ejecución multiagente

- Pista UI: A → B → C en serie; dentro de B, conversiones de listados en lotes con archivos disjuntos (varios implementadores en paralelo sobre worktrees o conjuntos de archivos sin solape).
- Pista Entregable: textos, diagramas y ER en paralelo con la pista UI; capturas y E2E final tras C.
- Cada task: implementador (opus) en segundo plano → revisión (opus/sonnet según riesgo) → arreglos; revisión final de rama y una ola de arreglos. Commits locales en la rama; nunca merge ni push.

## Fuera de alcance

Cambios de dominio contable o de posteo; nuevo diseño de marca; i18n; paleta con acciones (solo navegación y registros).

## Confirmado por el usuario

- Documento sin portada, solo en Word (`.docx`).

## Resultado

Implementado en la rama `Fix-Features` (`8e81a41..HEAD`, commits locales) según el plan
[`2026-09-28-fix-features.md`](../plans/2026-09-28-fix-features.md), cuya sección "Resultado de Fix-Features" recoge
los commits por task, las decisiones de ejecución (R1–R17, FFA–FFI) y los residuales aceptados.

- **Fase A:** registro único de módulos y grupos con visibilidad por permiso; menú lateral por grupos; barra superior
  con búsqueda en servidor (`/buscar`, módulos y registros, API de solo lectura con Dapper); paleta Ctrl+K progresiva;
  inicio compacto con tarjetas de grupo y `/modulos/{grupo}` con indicadores; navegación sin destello azul.
- **Fase B:** barra de acciones (`+ Nuevo`, `Crear ▾`, `Ver ▾`, `Editar`, `Eliminar`), selección de fila con `?sel=`,
  página-tarjeta de alta y edición con `returnUrl` local validado, aplicadas a todos los listados de mantenimiento.
- **Fase C:** acciones contextuales de clientes (nueva factura precargada, nota de crédito, facturas filtradas, cobro
  en página propia `/cobros/nuevo`), productos (filtro por existencia, ajuste en diario precargado) y documentos.
- **Fase D:** documento técnico (`.docx`, sin portada, justificado), presentación de 9 diapositivas, guion, diagramas y
  ER generados del esquema real, suite E2E Playwright con capturas y generadores versionados.
- **Verificación de cierre:** suite xUnit 1684/1684, build con 0 avisos, E2E 14 passed + `@api-caida` skipped (verde
  aparte), stack Docker sano; sin interactividad añadida, sin cambios de `appsettings*.json` ni migraciones.
- **Pendiente para el usuario:** rotar y retirar del repositorio la contraseña semilla versionada (hallazgo
  preexistente) y decidir si el `.docx` lleva autor.
