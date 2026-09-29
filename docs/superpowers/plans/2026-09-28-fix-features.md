# Fix-Features — Navegación, acciones por página y entregable Parte 1 · Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** dar a AxionERP una estructura de navegación común (registro de módulos, menú por grupos, búsqueda global en
servidor y paleta Ctrl+K, inicio con tarjetas de grupo, sin flash azul), un patrón de página único (barra de acciones,
alta/edición en página-tarjeta propia, selección de filas) aplicado a todos los listados, acciones contextuales entre
clientes, productos y documentos, y el entregable académico Parte 1 (documento Word, diagramas, E2E con capturas,
presentación y guion).

**Architecture:** el host Blazor (Static SSR) gana una carpeta `Navigation/` con un catálogo único de grupos y módulos
(`CatalogoModulos`) y un servicio `IRegistroModulos` que filtra por política/rol con `IAuthorizationService`; menú,
inicio, páginas de grupo, `/buscar` y la isla JSON de la paleta leen de ahí. La búsqueda de registros es una consulta
Dapper de solo lectura (`GET api/busqueda`) expuesta por MediatR y consumida por un cliente tipado; la paleta es JS
vanilla progresivo que llama a un endpoint mínimo del host (`/buscar/sugerencias`) para no exponer el JWT. El patrón de
página se apoya en componentes compartidos (`PageToolbar`, `EntityFormPage`, `FormularioAcciones`, `SeleccionFila`,
`TarjetaAcciones`) y cada listado mueve sus formularios de alta/edición a una página `/x/nuevo` + `/x/{id}/editar`.
El entregable vive en `docs/entregable-parte-1/` y su tooling (E2E, render de Mermaid, extracción del esquema) en
`tests/e2e/` (Node, fuera de `test.slnx`).

**Tech Stack:** .NET 10, Blazor Static SSR + Tailwind Play CDN, ASP.NET Core Web API, MediatR 13, Dapper, PostgreSQL 17,
xUnit + Moq + WebApplicationFactory + `HtmlRenderer`, Testcontainers (Postgres), Node 26 + `@playwright/test` + `mermaid`,
Python 3 (extracción del esquema), skills `anthropic-skills:docx` y `anthropic-skills:pptx` (documento y presentación).

**Spec:** [`docs/superpowers/specs/2026-09-28-fix-features-navegacion-y-entregable-design.md`](../specs/2026-09-28-fix-features-navegacion-y-entregable-design.md)
(vinculante). Contexto de convenciones: `CLAUDE.md`, plan anterior
[`2026-09-30-fase-8-notas-credito.md`](2026-09-30-fase-8-notas-credito.md).

## Global Constraints

- Blazor **Static SSR**: sin `@rendermode`, `@onclick` ni `@bind`. GET/POST con `[SupplyParameterFromQuery]`/`[SupplyParameterFromForm]`, antiforgery y clientes tipados. JS solo vanilla y progresivo (la página funciona sin él); re-inicialización con `Blazor.addEventListener('enhancedload', …)` (nunca `document.addEventListener('enhancedload', …)` como único mecanismo).
- El JWT nunca llega al navegador (cookie HttpOnly + sesión de servidor). Todo endpoint JSON del host Blazor llama a la API desde el servidor.
- Permisos de la UI iguales a los de la API (`CanConsult`, `CanAdd`, `CanModify`, `CanDelete`, `CanAdministrar`, roles). Una acción sin permiso no se muestra; un POST forzado recibe el 403 real de la API.
- Lecciones vigentes: formularios de acción siempre en el árbol (EditForm vacío con el mismo `FormName`); `[SupplyParameterFromQuery]` sin enums (int?); `PageRequest.Descendente` true por defecto (orden explícito siempre).
- Sin cambios de dominio ni de migraciones salvo lo que pida la búsqueda (solo lectura, Dapper). No tocar `appsettings*.json`.
- Tests: cada task con tests (bUnit/SSR o integración API según capa); suite completa verde y build con 0 avisos al cerrar cada fase.
- TFM `net10.0`; `PackageReference` directo por proyecto, sin Central Package Management; **sin paquetes NuGet nuevos** (no hay bUnit: los tests de componentes usan `Microsoft.AspNetCore.Components.Web.HtmlRenderer` y los de páginas `WebApplicationFactory<BlazorApp::Program>`, ambos ya disponibles).
- Tests contra Postgres real con Testcontainers: `DOCKER_CONTEXT=default` **solo como variable de entorno** del comando (`env DOCKER_CONTEXT=default dotnet test …`), nunca `docker context use`.
- Suite de partida: **1361** tests verdes; build con **0 avisos**. Cada fase termina con la suite completa verde y 0 avisos.
- Commits LOCALES en la rama `Fix-Features`, mensajes en español con el trailer de la sesión. Nunca merge ni push.
- Documento del entregable: **sin portada**, texto en español, párrafos justificados, **solo `.docx`** como formato de entrega (el `.md` es la fuente). Presentación `.pptx` de 8–10 diapositivas.
- E2E: `tests/e2e/` (Node + `@playwright/test`), fuera de `test.slnx`; credenciales solo por variables de entorno; stack de Docker con `POSTGRES_PORT` leído de `.env`.
- Fuera de alcance: cambios de dominio contable o de posteo; nuevo diseño de marca; i18n; acciones dentro de la paleta (solo navegación y registros).

## Review Focus

1. **Metacaracteres en la búsqueda** (`%`, `_`, `\`, comillas, espacios alrededor): se buscan literalmente, sin error de SQL ni resultados de más; `q` se recorta antes de validar 2–100. Test en Task A3a (`Metacaracteres_SeBuscanLiteralmente`, `Validacion_Q_Limite_YPermisos`).
2. **`returnUrl` hostil** (`//evil.com`, `https://evil.com`, `/\evil`, `javascript:`, caracteres de control): el botón Cancelar y la redirección tras guardar vuelven siempre a la lista local por defecto. Test en Task B1 (`RetornoLocalTests`) y en Task B2a (`Guardar_ConReturnUrlExterno_VuelveALaLista`).
3. **Usuario sin permiso** (Ejecutor/Supervisor): no ve en menú, paleta (isla JSON), barra ni tarjetas los módulos o acciones que no puede usar; un POST forzado a un formulario oculto llega al handler y devuelve el mensaje real (403) de la API. Tests en Task A1 (visibilidad), A4 (isla), B1 (barra) y B2a (`PostForzado_SinPermiso_DevuelveMensajeDeLaApi`).
4. **Selección obsoleta** (`?sel=` de un registro borrado o que no está en la página actual): la barra trata la selección como vacía (acciones deshabilitadas con `title` explicativo), nunca enlaza a un id que la lista no muestra. Test en Task B2a (`SeleccionQueNoEstaEnLaPagina_DeshabilitaAcciones`).
5. **API caída o sin JS**: `/buscar` sigue mostrando los módulos y un aviso para los registros; `/buscar/sugerencias` responde 502 y la paleta muestra los módulos igualmente; `/modulos/{grupo}` omite el indicador que falla sin romper la página. Tests en Task A3b (`Buscar_ConApiCaida_MuestraModulosYAviso`), A4 (`Sugerencias_ApiCaida_502`) y A5 (`Grupo_IndicadorQueFalla_SeOmite`).

---

## Estructura de archivos

**Host Blazor — `src/OpenSource1.Blazor/`**

| Archivo | Responsabilidad | Task |
|---|---|---|
| `Security/PoliticasBlazor.cs` | Definición única de las 5 políticas coarse (Program.cs y tests). | A1 |
| `Navigation/GrupoModulo.cs` | Records `GrupoModulo` y `Modulo`. | A1 |
| `Navigation/IconosModulo.cs` | Trazos SVG (heroicons) usados por grupos, módulos y acciones. | A1 |
| `Navigation/CatalogoModulos.cs` | Catálogo único de grupos y módulos (fuente de verdad). | A1 |
| `Navigation/IRegistroModulos.cs`, `Navigation/RegistroModulos.cs` | Visibilidad por política/rol, grupos visibles, módulo de una ruta. | A1 |
| `Navigation/TextoBusqueda.cs` | Normalización sin acentos y filtro de módulos. | A1 |
| `Components/Layout/NavMenu.razor` | Menú lateral por grupos con `<details>`. | A2 |
| `Components/Layout/MainLayout.razor` | Usa `NavMenu`, añade la barra superior de búsqueda. | A2, A3b |
| `Services/IBusquedaApiClient.cs`, `Services/BusquedaApiClient.cs` | Cliente tipado de `api/busqueda`. | A3b |
| `Components/Layout/BarraBusqueda.razor` | Caja de búsqueda (form GET `/buscar`), capa de la paleta e isla JSON. | A3b, A4 |
| `Components/Pages/Buscar.razor` | Página de resultados `/buscar?q=`. | A3b |
| `wwwroot/app.search.js` | Paleta Ctrl+K / `/` (mejora progresiva). | A4 |
| `Navigation/IIndicadoresModulos.cs`, `Navigation/IndicadoresModulos.cs` | Hasta 3 indicadores por grupo desde endpoints existentes. | A5 |
| `Components/Pages/Home.razor` | Inicio compacto: KPIs + tarjetas de grupo. | A5 |
| `Components/Pages/ModuloGrupo.razor` | `/modulos/{grupo}`. | A5 |
| `wwwroot/app.css`, `wwwroot/app.interactions.js` | Barra de progreso tardía, 2 px, color atenuado. | A6 |
| `Components/AccionPagina.cs` | Records `AccionPagina`, `Miga` y filtro de acciones por permiso. | B1 |
| `Components/RetornoLocal.cs` | Validación de `returnUrl` local y composición de query. | B1 |
| `Components/PageToolbar.razor` | Título, migas, `+ Nuevo`, `Crear ▾`, `Ver ▾`, `Editar`, `Eliminar`. | B1 |
| `Components/EntityFormPage.razor`, `Components/FormularioAcciones.razor` | Página-tarjeta de alta/edición y sus botones. | B1 |
| `Components/SeleccionFila.razor`, `Components/TarjetaAcciones.razor` | Radio visual de fila y acciones de tarjeta (2 rápidas + `⋯`). | B1 |
| `wwwroot/app.menus.js` | Cierra menús `<details data-menu>` con clic fuera / Esc. | B1 |
| `Components/Pages/*Editor.razor`, `*Nueva.razor`, `*Nuevo.razor` | Páginas-tarjeta de alta/edición por entidad. | B2a–B2d, C1, C3 |

**Backend**

| Archivo | Responsabilidad | Task |
|---|---|---|
| `src/OpenSource1.Application/Features/Busqueda/Dtos/BusquedaGlobalResponse.cs` | DTOs `ResultadoBusqueda`, `GrupoResultadosBusqueda`, `BusquedaGlobalResponse`, `TiposResultadoBusqueda`. | A3a |
| `src/OpenSource1.Application/Features/Busqueda/IBusquedaGlobalRepository.cs` | Contrato de lectura. | A3a |
| `src/OpenSource1.Application/Features/Busqueda/Queries/BuscarGlobalQuery.cs` | Query MediatR. | A3a |
| `src/OpenSource1.Application/Features/Busqueda/Handlers/BuscarGlobalQueryHandler.cs` | Validación `q` 2–100 / `limite` 1–20. | A3a |
| `src/OpenSource1.Infrastructure/Data/Queries/DapperBusquedaGlobalRepository.cs` | SQL `ILIKE … ESCAPE` por tipo, sin borrados lógicos. | A3a |
| `src/OpenSource1.Api/Controllers/BusquedaController.cs` | `GET api/busqueda` (CanConsult). | A3a |

**Tests — `tests/OpenSource1.SmokeTests/`**

| Archivo | Uso | Task |
|---|---|---|
| `TestInfrastructure/PermisosTestAuthHandler.cs` | Autenticación de prueba con roles y claims `permission`. | A1 |
| `TestInfrastructure/BlazorSsrFactory.cs` | `WebApplicationFactory<BlazorApp::Program>` con clientes tipados simulados (Moq). | A1 |
| `TestInfrastructure/RenderizadorComponentes.cs` | `HtmlRenderer` + estado de autenticación en cascada. | A1 |
| `TestInfrastructure/FormulariosSsr.cs` | GET + token antiforgery + POST de un `EditForm`. | A1 |
| `Blazor/*Tests.cs`, `Api/BusquedaApiTests.cs`, `Features/Busqueda/*Tests.cs` | Tests por task (nombres en cada task). | todas |

**Entregable y tooling**

| Ruta | Contenido | Task |
|---|---|---|
| `docs/entregable-parte-1/documento-tecnico.md` | Fuente del documento (Pasos 1–10). | D1, D4 |
| `docs/entregable-parte-1/diagramas/` | `arquitectura.mmd/.svg/.png`, `modelo-er.mmd/.svg/.png`, `diccionario-datos.md`. | D2 |
| `tests/e2e/` | `package.json`, `playwright.config.ts`, `scripts/render-mermaid.mjs`, `scripts/esquema_a_mermaid.py`, `specs/*.spec.ts`. | D2, D3 |
| `docs/entregable-parte-1/capturas/`, `evidencias/` | Capturas 1440×900 y resúmenes de pruebas. | D3, D4 |
| `docs/entregable-parte-1/documento-tecnico.docx`, `presentacion.pptx`, `guion-demo.md`, `README.md` | Entregables finales. | D4 |

---

## Orden de ejecución y paralelismo

```text
Pista UI (serie entre fases)                                   Pista entregable
─────────────────────────────                                  ────────────────
 A1 ──► A2 ──► A3b ──► A4 ──┐                                   D1 (textos Pasos 1–5)  ─┐  inmediatas, en paralelo
  │                          ├──► A5 ─┐                          D2 (diagramas + ER)    ─┘  con toda la pista UI
 A3a (API, paralela a A1/A2)─┘        ├──► G-A (puerta de fase)
 A6 (tras A2, paralela a A3b/A4/A5) ──┘
                                        │
                                        ▼
                                       B1 ──► { B2a ‖ B2b ‖ B2c ‖ B2d } ──► G-B
                                                                             │
                                                                             ▼
                                                                { C1 ‖ C2 ‖ C3 } ──► G-C ──► D3 (E2E + capturas) ──► D4 (docx, pptx, guion) ──► Z (cierre)
```

| Grupo | Tasks | ¿Paralelo? | Condición |
|---|---|---|---|
| Inicio | A1, A3a, D1, D2 | Sí, las cuatro a la vez | A3a solo toca Application/Infrastructure/Api/tests Api; D1/D2 solo `docs/` y `tests/e2e/`. |
| A-serie | A2 → A3b → A4 → A5 | Serie | Comparten `MainLayout.razor`, `BarraBusqueda.razor`, `Program.cs`. A3b necesita A3a terminada. |
| A6 | A6 | Paralela a A3b, A4 y A5 | Solo `app.css`, `app.interactions.js` y clases `animate-*` de páginas **distintas de** `Home.razor` (que es de A5). No toca `MainLayout`, `App.razor` ni `Program.cs`. |
| G-A | Puerta | Serie | Todas las A terminadas. |
| B1 | B1 | Serie | Tras G-A. |
| B2 | B2a, B2b, B2c, B2d | **Sí, las cuatro a la vez** (worktrees o conjuntos de archivos sin solape) | Cada lote toca solo sus páginas, sus editores nuevos y su archivo de tests. **Ningún lote toca** `MainLayout.razor`, `NavMenu.razor`, `BarraBusqueda.razor`, `Navigation/*` (registro), `Program.cs`, `App.razor`, `_Imports.razor` ni los componentes de B1. Si un lote necesita cambiar uno de ellos, lo reporta y no lo edita. |
| G-B | Puerta | Serie | B2a–B2d integrados. |
| C | C1, C2, C3 | **Sí, las tres a la vez** | C1: `Clientes.razor`, `ClienteDetail.razor`, `Cobros.razor`, `CobroNuevo.razor`. C2: `Productos.razor`, `ProductoDetail.razor`, `LoteDiarioEditor.razor`. C3: `FacturasVenta.razor`, `FacturaVentaNueva.razor`, `FacturasVentaBorradores.razor`, `NotasCreditoVentaBorradores.razor`, `NotaCreditoNueva.razor`. Mismas prohibiciones de archivos compartidos que B2. |
| G-C | Puerta | Serie | C1–C3 integrados. |
| D3 → D4 → Z | Serie | Tras G-C | D3 necesita la UI final (capturas); D4 necesita capturas y evidencias; Z cierra la rama. |

---
## Fase A — Estructura común

### Task A1: Registro de módulos (fuente única) e infraestructura de pruebas Blazor

**Grupo de paralelismo:** Inicio (en paralelo con A3a, D1, D2).

**Files:**
- Create: `src/OpenSource1.Blazor/Security/PoliticasBlazor.cs`
- Create: `src/OpenSource1.Blazor/Navigation/GrupoModulo.cs`
- Create: `src/OpenSource1.Blazor/Navigation/IconosModulo.cs`
- Create: `src/OpenSource1.Blazor/Navigation/CatalogoModulos.cs`
- Create: `src/OpenSource1.Blazor/Navigation/IRegistroModulos.cs`
- Create: `src/OpenSource1.Blazor/Navigation/RegistroModulos.cs`
- Create: `src/OpenSource1.Blazor/Navigation/TextoBusqueda.cs`
- Modify: `src/OpenSource1.Blazor/Program.cs` (bloque `builder.Services.AddAuthorization(...)`, líneas 158-165; registro de `IRegistroModulos`)
- Modify: `src/OpenSource1.Blazor/Components/_Imports.razor` (añadir `@using OpenSource1.Blazor.Navigation`)
- Create: `tests/OpenSource1.SmokeTests/TestInfrastructure/PermisosTestAuthHandler.cs`
- Create: `tests/OpenSource1.SmokeTests/TestInfrastructure/BlazorSsrFactory.cs`
- Create: `tests/OpenSource1.SmokeTests/TestInfrastructure/RenderizadorComponentes.cs`
- Create: `tests/OpenSource1.SmokeTests/TestInfrastructure/FormulariosSsr.cs`
- Test: `tests/OpenSource1.SmokeTests/Blazor/RegistroModulosTests.cs`

**Interfaces:**
- Consumes: `ApplicationPolicies.*`, `ApplicationRoles.*` (`OpenSource1.Application.Security`).
- Produces (usadas por A2–A5, B, C):
  - `namespace OpenSource1.Blazor.Navigation`: `record GrupoModulo(string Clave, string Titulo, string Icono, string Descripcion, int Orden)`; `record Modulo(string Clave, string Grupo, string Titulo, string Descripcion, string Ruta, string Icono, string? Politica, IReadOnlyList<string>? Roles, IReadOnlyList<string> PalabrasClave)`.
  - `static class CatalogoModulos { IReadOnlyList<GrupoModulo> Grupos; IReadOnlyList<Modulo> Modulos; }`.
  - `interface IRegistroModulos { IReadOnlyList<GrupoModulo> Grupos { get; } IReadOnlyList<Modulo> Todos { get; } Task<IReadOnlyList<Modulo>> VisiblesAsync(ClaimsPrincipal usuario); Task<IReadOnlyList<GrupoModulo>> GruposVisiblesAsync(ClaimsPrincipal usuario); GrupoModulo? BuscarGrupo(string? clave); Modulo? ModuloDeRuta(string? ruta); }` (registrado scoped).
  - `static class TextoBusqueda { string Normalizar(string? texto); IReadOnlyList<Modulo> FiltrarModulos(IEnumerable<Modulo> modulos, string? consulta); }`.
  - `static class IconosModulo` con constantes `Inicio, Persona, Personas, Cubo, Lista, Tabla, Tarjeta, Documento, Libro, Grafico, Calendario, Etiqueta, Escudo, Reloj, Buscar, Mas, Descargar, Ajustes, Flecha` (trazos `d` de heroicons 24×24 outline).
  - `static class PoliticasBlazor { string[] Todas; void Configurar(AuthorizationOptions options); }` (`OpenSource1.Blazor.Security`).
  - Tests: `PermisosTestAuthHandler` (esquema `"TestPermisos"`, cabeceras `X-Test-Anonymous`, `X-Test-Roles`, `X-Test-Permisos`, `static string PermisosDeRol(string roles)`, `static ClaimsPrincipal Principal(string roles, string? permisos = null)`), `BlazorSsrFactory` (`Mock<T> Simular<T>()`, `HttpClient Cliente(string roles = "Administrador", string? permisos = null, bool anonimo = false)`), `RenderizadorComponentes.RenderizarAsync<TComponente>(ClaimsPrincipal usuario, IDictionary<string, object?> parametros)`, `FormulariosSsr.EnviarAsync(HttpClient cliente, string urlGet, string formName, IReadOnlyDictionary<string, string> campos, string? urlPost = null)` y `FormulariosSsr.Destino(HttpResponseMessage respuesta)` (path+query del `Location`).

- [ ] **Step 1: Escribir la infraestructura de pruebas**

`tests/OpenSource1.SmokeTests/TestInfrastructure/PermisosTestAuthHandler.cs`:

```csharp
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenSource1.Application.Security;

namespace OpenSource1.SmokeTests.TestInfrastructure;

/// <summary>
/// Autenticación de prueba del host Blazor. A diferencia de <see cref="TestAuthHandler"/> (API), las políticas del host
/// exigen el claim <c>permission</c> (CanConsult, CanAdd…), igual que la cookie real que crea Login.razor. Sin
/// "X-Test-Permisos" se emiten los permisos coarse del rol, con la misma tabla que <c>AuthService.GetPermissions</c>.
/// </summary>
public sealed class PermisosTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Esquema = "TestPermisos";
    public const string CabeceraAnonimo = "X-Test-Anonymous";
    public const string CabeceraRoles = "X-Test-Roles";
    public const string CabeceraPermisos = "X-Test-Permisos";

    public static string PermisosDeRol(string roles)
    {
        var permisos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rol in roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (rol)
            {
                case ApplicationRoles.Administrator:
                    permisos.UnionWith([ApplicationPolicies.CanAdd, ApplicationPolicies.CanModify, ApplicationPolicies.CanDelete, ApplicationPolicies.CanConsult, ApplicationPolicies.CanAdministrar]);
                    break;
                case ApplicationRoles.Supervisor:
                    permisos.UnionWith([ApplicationPolicies.CanModify, ApplicationPolicies.CanConsult]);
                    break;
                case ApplicationRoles.Executor:
                    permisos.UnionWith([ApplicationPolicies.CanAdd, ApplicationPolicies.CanConsult]);
                    break;
            }
        }

        return string.Join(',', permisos.Order(StringComparer.Ordinal));
    }

    public static ClaimsPrincipal Principal(string roles, string? permisos = null, string esquema = Esquema)
    {
        var listaRoles = roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var usuario = listaRoles.Length == 0 ? "anonimo" : listaRoles[0].ToLowerInvariant();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario),
            new(ClaimTypes.Name, usuario),
            new(ClaimTypes.Email, $"{usuario}@test.local"),
        };
        claims.AddRange(listaRoles.Select(rol => new Claim(ClaimTypes.Role, rol)));
        claims.AddRange((permisos ?? PermisosDeRol(roles))
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => new Claim("permission", p)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, esquema));
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.ContainsKey(CabeceraAnonimo))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var roles = Request.Headers.TryGetValue(CabeceraRoles, out var r) ? r.ToString() : ApplicationRoles.Administrator;
        string? permisos = Request.Headers.TryGetValue(CabeceraPermisos, out var p) ? p.ToString() : null;
        var principal = Principal(roles, permisos, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
```

`tests/OpenSource1.SmokeTests/TestInfrastructure/BlazorSsrFactory.cs`:

```csharp
extern alias BlazorApp;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace OpenSource1.SmokeTests.TestInfrastructure;

/// <summary>
/// Host real de OpenSource1.Blazor (pipeline, router, layout, antiforgery) sin API ni Postgres: los clientes tipados que la
/// prueba necesita se sustituyen por <see cref="Mock{T}"/>. Llamar a <see cref="Simular{T}"/> ANTES del primer
/// <see cref="Cliente"/> (el host se construye con la primera petición). Sin Docker.
/// </summary>
public sealed class BlazorSsrFactory : WebApplicationFactory<BlazorApp::Program>
{
    private readonly Dictionary<Type, Mock> _simulados = [];
    private bool _iniciado;

    public Mock<T> Simular<T>() where T : class
    {
        if (_simulados.TryGetValue(typeof(T), out var existente))
        {
            return (Mock<T>)existente;
        }

        if (_iniciado)
        {
            throw new InvalidOperationException("Simular<T>() debe llamarse antes de crear el primer cliente.");
        }

        var mock = new Mock<T>();
        _simulados[typeof(T)] = mock;
        return mock;
    }

    public HttpClient Cliente(string roles = "Administrador", string? permisos = null, bool anonimo = false)
    {
        _iniciado = true;
        var cliente = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (anonimo)
        {
            cliente.DefaultRequestHeaders.Add(PermisosTestAuthHandler.CabeceraAnonimo, "true");
        }

        cliente.DefaultRequestHeaders.Add(PermisosTestAuthHandler.CabeceraRoles, roles);
        cliente.DefaultRequestHeaders.Add(PermisosTestAuthHandler.CabeceraPermisos, permisos ?? PermisosTestAuthHandler.PermisosDeRol(roles));
        return cliente;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // Raíz de contenido explícita (wwwroot, appsettings del host): no depende del manifiesto de Mvc.Testing ni del alias.
        builder.UseContentRoot(Path.Combine(RaizRepositorio(), "src", "OpenSource1.Blazor"));
        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(PermisosTestAuthHandler.Esquema)
                .AddScheme<AuthenticationSchemeOptions, PermisosTestAuthHandler>(PermisosTestAuthHandler.Esquema, _ => { });

            // Autentica con el esquema de prueba; el Challenge/Forbid siguen siendo los de la cookie real (302 a
            // /account/login y /access-denied), igual que en producción.
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = PermisosTestAuthHandler.Esquema;
                options.DefaultScheme = PermisosTestAuthHandler.Esquema;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultForbidScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            });
        });

        // ConfigureTestServices corre DESPUÉS de los registros de Program.cs: el mock reemplaza al AddHttpClient<…>.
        builder.ConfigureTestServices(services =>
        {
            foreach (var (tipo, mock) in _simulados)
            {
                services.RemoveAll(tipo);
                services.AddSingleton(tipo, mock.Object);
            }
        });
    }

    private static string RaizRepositorio()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "test.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio (test.slnx).");
    }
}
```

`tests/OpenSource1.SmokeTests/TestInfrastructure/RenderizadorComponentes.cs`:

```csharp
extern alias BlazorApp;

using System.Security.Claims;
using BlazorApp::OpenSource1.Blazor.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace OpenSource1.SmokeTests.TestInfrastructure;

/// <summary>
/// Renderiza un componente a HTML con <see cref="HtmlRenderer"/> (sin host): servicios mínimos (logging y las políticas
/// reales de <see cref="PoliticasBlazor"/>) y el <c>Task&lt;AuthenticationState&gt;</c> en cascada con el usuario dado.
/// Para componentes sin NavigationManager (barras, tarjetas, páginas-tarjeta).
/// </summary>
public static class RenderizadorComponentes
{
    public static async Task<string> RenderizarAsync<TComponente>(
        ClaimsPrincipal usuario, IDictionary<string, object?> parametros, Action<IServiceCollection>? servicios = null)
        where TComponente : IComponent
    {
        var coleccion = new ServiceCollection();
        coleccion.AddLogging();
        coleccion.AddAuthorizationCore(PoliticasBlazor.Configurar);
        servicios?.Invoke(coleccion);

        await using var proveedor = coleccion.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(proveedor, proveedor.GetRequiredService<ILoggerFactory>());
        var estado = Task.FromResult(new AuthenticationState(usuario));

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            RenderFragment hijo = builder =>
            {
                builder.OpenComponent<TComponente>(0);
                var secuencia = 1;
                foreach (var (clave, valor) in parametros)
                {
                    builder.AddComponentParameter(secuencia++, clave, valor);
                }

                builder.CloseComponent();
            };

            var salida = await renderer.RenderComponentAsync<CascadingValue<Task<AuthenticationState>>>(
                ParameterView.FromDictionary(new Dictionary<string, object?> { ["Value"] = estado, ["ChildContent"] = hijo }));
            return salida.ToHtmlString();
        });
    }
}
```

`tests/OpenSource1.SmokeTests/TestInfrastructure/FormulariosSsr.cs`:

```csharp
using System.Net;
using System.Text.RegularExpressions;

namespace OpenSource1.SmokeTests.TestInfrastructure;

/// <summary>
/// Envía un EditForm SSR como lo haría el navegador: GET de la página (fija la cookie de antiforgery en el cliente),
/// extrae el token oculto y hace POST con <c>_handler</c> = FormName. Devuelve la respuesta sin seguir redirecciones.
/// </summary>
public static partial class FormulariosSsr
{
    public static async Task<HttpResponseMessage> EnviarAsync(
        HttpClient cliente, string urlGet, string formName, IReadOnlyDictionary<string, string> campos, string? urlPost = null)
    {
        var pagina = await cliente.GetAsync(urlGet);
        var html = await pagina.Content.ReadAsStringAsync();
        Assert.True(pagina.StatusCode == HttpStatusCode.OK, $"GET {urlGet} devolvió {pagina.StatusCode}: {html}");

        var etiqueta = TokenInput().Match(html);
        Assert.True(etiqueta.Success, $"La página {urlGet} no contiene el token antiforgery.");
        var valor = ValorAtributo().Match(etiqueta.Value);
        Assert.True(valor.Success, "El input del token no tiene value.");

        var cuerpo = new Dictionary<string, string>(campos)
        {
            ["_handler"] = formName,
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(valor.Groups[1].Value),
        };

        return await cliente.PostAsync(urlPost ?? urlGet, new FormUrlEncodedContent(cuerpo));
    }

    /// <summary>Ruta y query del <c>Location</c> de una redirección (absoluto o relativo).</summary>
    public static string Destino(HttpResponseMessage respuesta)
    {
        var location = respuesta.Headers.Location ?? throw new InvalidOperationException($"La respuesta {respuesta.StatusCode} no trae Location.");
        return location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString;
    }

    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>")]
    private static partial Regex TokenInput();

    [GeneratedRegex("value=\"([^\"]+)\"")]
    private static partial Regex ValorAtributo();
}
```

- [ ] **Step 2: Escribir los tests del registro (fallan: los tipos no existen)**

`tests/OpenSource1.SmokeTests/Blazor/RegistroModulosTests.cs`:

```csharp
extern alias BlazorApp;

using System.Reflection;
using BlazorApp::OpenSource1.Blazor.Navigation;
using BlazorApp::OpenSource1.Blazor.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>
/// Task A1: el catálogo de módulos es la fuente única de navegación. Cada listado (@page sin parámetros que no sea de
/// cuenta, error, inicio, búsqueda ni alta) aparece en el registro, y cada ruta del registro existe como @page.
/// Visibilidad evaluada con las políticas reales del host (claim "permission") y los roles.
/// </summary>
public sealed class RegistroModulosTests
{
    private static readonly string[] RutasFueraDelRegistro = ["/", "/buscar", "/Error", "/not-found", "/access-denied"];

    [Fact]
    public void CadaListadoTieneModulo_YCadaRutaDelRegistroExiste()
    {
        var plantillas = typeof(BlazorApp::OpenSource1.Blazor.Components.App).Assembly.GetTypes()
            .SelectMany(t => t.GetCustomAttributes<RouteAttribute>())
            .Select(a => "/" + a.Template.Trim('/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var listados = plantillas
            .Where(p => !p.Contains('{'))
            .Where(p => !RutasFueraDelRegistro.Contains(p, StringComparer.OrdinalIgnoreCase))
            .Where(p => !p.StartsWith("/account/", StringComparison.OrdinalIgnoreCase))
            .Where(p => !p.EndsWith("/new", StringComparison.OrdinalIgnoreCase)
                        && !p.EndsWith("/nuevo", StringComparison.OrdinalIgnoreCase)
                        && !p.EndsWith("/nueva", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var rutasRegistro = CatalogoModulos.Modulos.Select(m => m.Ruta).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.All(listados, ruta => Assert.True(rutasRegistro.Contains(ruta), $"El listado {ruta} no está en CatalogoModulos."));
        Assert.All(rutasRegistro, ruta => Assert.True(plantillas.Contains(ruta), $"La ruta {ruta} del registro no existe como @page."));
    }

    [Fact]
    public void Catalogo_ClavesUnicas_YGruposExistentes()
    {
        Assert.Equal(CatalogoModulos.Modulos.Count, CatalogoModulos.Modulos.Select(m => m.Clave).Distinct().Count());
        var grupos = CatalogoModulos.Grupos.Select(g => g.Clave).ToHashSet();
        Assert.All(CatalogoModulos.Modulos, m => Assert.Contains(m.Grupo, grupos));
        Assert.Equal(
            ["clientes", "productos", "inventario", "ventas", "facturacion", "contabilidad", "reportes", "configuracion", "administracion"],
            CatalogoModulos.Grupos.OrderBy(g => g.Orden).Select(g => g.Clave));
    }

    [Theory]
    [InlineData("Administrador", true, true, true)]
    [InlineData("Supervisor", false, false, true)]
    [InlineData("Ejecutor", false, false, false)]
    public async Task Visibles_SegunRolYPolitica(string rol, bool usuarios, bool fechas, bool bitacora)
    {
        var registro = Registro();

        var visibles = (await registro.VisiblesAsync(PermisosTestAuthHandler.Principal(rol))).Select(m => m.Clave).ToList();

        Assert.Contains("clientes", visibles);
        Assert.Equal(usuarios, visibles.Contains("usuarios"));
        Assert.Equal(fechas, visibles.Contains("fechas-registro"));
        Assert.Equal(bitacora, visibles.Contains("bitacora"));
    }

    [Fact]
    public async Task Anonimo_NoVeNada_YSinCanConsultSoloVeLoQueNoLaExige()
    {
        var registro = Registro();

        Assert.Empty(await registro.VisiblesAsync(new System.Security.Claims.ClaimsPrincipal()));

        // Supervisor sin CanConsult: solo la Bitácora (rol) sigue visible.
        var sinConsulta = await registro.VisiblesAsync(PermisosTestAuthHandler.Principal("Supervisor", permisos: ""));
        Assert.Equal(["bitacora"], sinConsulta.Select(m => m.Clave));
    }

    [Fact]
    public async Task GruposVisibles_SoloLosQueTienenModulosVisibles()
    {
        var registro = Registro();

        var ejecutor = (await registro.GruposVisiblesAsync(PermisosTestAuthHandler.Principal("Ejecutor"))).Select(g => g.Clave).ToList();
        var admin = (await registro.GruposVisiblesAsync(PermisosTestAuthHandler.Principal("Administrador"))).Select(g => g.Clave).ToList();

        Assert.DoesNotContain("administracion", ejecutor);
        Assert.Contains("configuracion", ejecutor);
        Assert.Equal(9, admin.Count);
    }

    [Theory]
    [InlineData("facturas-venta/borradores/3f2b8c1e-0000-0000-0000-000000000001", "borradores-factura")]
    [InlineData("/facturas-venta/FV000001", "facturas")]
    [InlineData("/contabilidad/movimientos", "movimientos-contables")]
    [InlineData("/contabilidad", "costo-inventario")]
    [InlineData("/clientes/3f2b8c1e-0000-0000-0000-000000000001/editar", "clientes")]
    [InlineData("/unidades-medida?pagina=2", "unidades-medida")]
    public void ModuloDeRuta_ElPrefijoMasLargoGana(string ruta, string clave)
    {
        Assert.Equal(clave, Registro().ModuloDeRuta(ruta)?.Clave);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("/clientesx")]
    [InlineData(null)]
    public void ModuloDeRuta_SinCoincidencia_EsNull(string? ruta)
    {
        Assert.Null(Registro().ModuloDeRuta(ruta));
    }

    [Fact]
    public void Normalizar_QuitaAcentosYMayusculas()
    {
        Assert.Equal("categorias de producto", TextoBusqueda.Normalizar("  Categorías de PRODUCTO "));
        Assert.Equal(string.Empty, TextoBusqueda.Normalizar(null));
    }

    [Fact]
    public void FiltrarModulos_PorTituloPalabraClaveYGrupo_SinAcentos()
    {
        Assert.Contains(TextoBusqueda.FiltrarModulos(CatalogoModulos.Modulos, "categoria"), m => m.Clave == "categorias-producto");
        Assert.Equal(
            ["movimientos-cliente", "estado-cuenta", "grupos-cliente-contable"],
            TextoBusqueda.FiltrarModulos(CatalogoModulos.Modulos, "CXC").Select(m => m.Clave));
        Assert.Contains(TextoBusqueda.FiltrarModulos(CatalogoModulos.Modulos, "facturación"), m => m.Clave == "notas-credito");
        Assert.Empty(TextoBusqueda.FiltrarModulos(CatalogoModulos.Modulos, "  "));
    }

    private static RegistroModulos Registro()
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddAuthorizationCore(PoliticasBlazor.Configurar);
        var autorizacion = servicios.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
        return new RegistroModulos(autorizacion);
    }
}
```

- [ ] **Step 3: Ejecutar y ver que falla**

Run: `dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~RegistroModulosTests"`
Expected: FAIL de compilación (`OpenSource1.Blazor.Navigation` y `PoliticasBlazor` no existen).

- [ ] **Step 4: Implementar políticas, registro y catálogo**

`src/OpenSource1.Blazor/Security/PoliticasBlazor.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using OpenSource1.Application.Security;

namespace OpenSource1.Blazor.Security;

/// <summary>
/// Políticas coarse del host Blazor: cada una exige el claim "permission" que la API emite al iniciar sesión (ver
/// AuthService.GetPermissions). Definición única para Program.cs y para los tests del registro de módulos.
/// </summary>
public static class PoliticasBlazor
{
    public static readonly string[] Todas =
    [
        ApplicationPolicies.CanAdd,
        ApplicationPolicies.CanModify,
        ApplicationPolicies.CanDelete,
        ApplicationPolicies.CanConsult,
        ApplicationPolicies.CanAdministrar,
    ];

    public static void Configurar(AuthorizationOptions options)
    {
        foreach (var politica in Todas)
        {
            options.AddPolicy(politica, policy => policy.RequireClaim("permission", politica));
        }
    }
}
```

En `src/OpenSource1.Blazor/Program.cs`, sustituir el bloque

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(ApplicationPolicies.CanAdd, policy => policy.RequireClaim("permission", ApplicationPolicies.CanAdd));
    options.AddPolicy(ApplicationPolicies.CanModify, policy => policy.RequireClaim("permission", ApplicationPolicies.CanModify));
    options.AddPolicy(ApplicationPolicies.CanDelete, policy => policy.RequireClaim("permission", ApplicationPolicies.CanDelete));
    options.AddPolicy(ApplicationPolicies.CanConsult, policy => policy.RequireClaim("permission", ApplicationPolicies.CanConsult));
    options.AddPolicy(ApplicationPolicies.CanAdministrar, policy => policy.RequireClaim("permission", ApplicationPolicies.CanAdministrar));
});
```

por

```csharp
builder.Services.AddAuthorization(PoliticasBlazor.Configurar);
builder.Services.AddScoped<IRegistroModulos, RegistroModulos>();
```

y añadir `using OpenSource1.Blazor.Navigation;` junto a los `using` del principio (`OpenSource1.Blazor.Security` ya está).

`src/OpenSource1.Blazor/Navigation/GrupoModulo.cs`:

```csharp
namespace OpenSource1.Blazor.Navigation;

/// <summary>Grupo del menú, del inicio y de <c>/modulos/{Clave}</c>. <c>Icono</c> es el trazo <c>d</c> de un SVG 24×24.</summary>
public sealed record GrupoModulo(string Clave, string Titulo, string Icono, string Descripcion, int Orden);

/// <summary>
/// Módulo navegable. Visible si el usuario cumple <c>Politica</c> (si la hay) Y tiene alguno de <c>Roles</c> (si los hay).
/// <c>Ruta</c> es la ruta absoluta del listado sin query; <c>PalabrasClave</c> alimenta la búsqueda de módulos.
/// </summary>
public sealed record Modulo(
    string Clave,
    string Grupo,
    string Titulo,
    string Descripcion,
    string Ruta,
    string Icono,
    string? Politica,
    IReadOnlyList<string>? Roles,
    IReadOnlyList<string> PalabrasClave);
```

`src/OpenSource1.Blazor/Navigation/IconosModulo.cs`:

```csharp
namespace OpenSource1.Blazor.Navigation;

/// <summary>Trazos <c>d</c> de heroicons (outline, 24×24, stroke 1.8) usados por la navegación y las acciones.</summary>
public static class IconosModulo
{
    public const string Inicio = "m2.25 12 8.954-8.955c.44-.439 1.152-.439 1.591 0L21.75 12M4.5 9.75v10.125c0 .621.504 1.125 1.125 1.125H9.75v-4.875c0-.621.504-1.125 1.125-1.125h2.25c.621 0 1.125.504 1.125 1.125V21h4.125c.621 0 1.125-.504 1.125-1.125V9.75M8.25 21h8.25";
    public const string Persona = "M15.75 6a3.75 3.75 0 1 1-7.5 0 3.75 3.75 0 0 1 7.5 0ZM4.501 20.118a7.5 7.5 0 0 1 14.998 0A17.933 17.933 0 0 1 12 21.75c-2.676 0-5.216-.584-7.499-1.632Z";
    public const string Personas = "M15 19.128a9.38 9.38 0 0 0 2.625.372 9.337 9.337 0 0 0 4.121-.952 4.125 4.125 0 0 0-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 0 1 8.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0 1 11.964-3.07M12 6.375a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0Zm8.25 2.25a2.625 2.625 0 1 1-5.25 0 2.625 2.625 0 0 1 5.25 0Z";
    public const string Cubo = "m21 7.5-9-5.25L3 7.5m18 0-9 5.25m9-5.25v9l-9 5.25m0-9L3 7.5m9 5.25v9M3 7.5v9l9 5.25";
    public const string Lista = "M8.25 6.75h12m-12 5.25h12m-12 5.25h12M3.75 6.75h.008v.008H3.75V6.75Zm0 5.25h.008v.008H3.75V12Zm0 5.25h.008v.008H3.75v-.008Z";
    public const string Tabla = "M3.75 12h16.5m-16.5 3.75h16.5M3.75 19.5h16.5M5.625 4.5h12.75a1.875 1.875 0 0 1 0 3.75H5.625a1.875 1.875 0 0 1 0-3.75Z";
    public const string Tarjeta = "M2.25 8.25h19.5M2.25 9h19.5m-16.5 5.25h6m-6 2.25h3M3.75 6h16.5a1.5 1.5 0 0 1 1.5 1.5v9a1.5 1.5 0 0 1-1.5 1.5H3.75a1.5 1.5 0 0 1-1.5-1.5v-9a1.5 1.5 0 0 1 1.5-1.5Z";
    public const string Documento = "M19.5 14.25v-2.625a3.375 3.375 0 0 0-3.375-3.375h-1.5A1.125 1.125 0 0 1 13.5 7.125v-1.5a3.375 3.375 0 0 0-3.375-3.375H8.25m0 12.75h7.5m-7.5 3H12M10.5 2.25H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 0 0-9-9Z";
    public const string Libro = "M12 6.042A8.967 8.967 0 0 0 6 3.75c-1.052 0-2.062.18-3 .512v14.25A8.987 8.987 0 0 1 6 18c2.305 0 4.408.867 6 2.292m0-14.25a8.966 8.966 0 0 1 6-2.292c1.052 0 2.062.18 3 .512v14.25A8.987 8.987 0 0 0 18 18a8.967 8.967 0 0 0-6 2.292m0-14.25v14.25";
    public const string Grafico = "M3 13.125C3 12.504 3.504 12 4.125 12h2.25c.621 0 1.125.504 1.125 1.125v6.75C7.5 20.496 6.996 21 6.375 21h-2.25A1.125 1.125 0 0 1 3 19.875v-6.75ZM9.75 8.625c0-.621.504-1.125 1.125-1.125h2.25c.621 0 1.125.504 1.125 1.125v11.25c0 .621-.504 1.125-1.125 1.125h-2.25a1.125 1.125 0 0 1-1.125-1.125V8.625ZM16.5 4.125c0-.621.504-1.125 1.125-1.125h2.25C20.496 3 21 3.504 21 4.125v15.75c0 .621-.504 1.125-1.125 1.125h-2.25a1.125 1.125 0 0 1-1.125-1.125V4.125Z";
    public const string Calendario = "M6.75 3v2.25M17.25 3v2.25M3 18.75V7.5a2.25 2.25 0 0 1 2.25-2.25h13.5A2.25 2.25 0 0 1 21 7.5v11.25m-18 0A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75m-18 0v-7.5A2.25 2.25 0 0 1 5.25 9h13.5A2.25 2.25 0 0 1 21 11.25v7.5";
    public const string Etiqueta = "M9.568 3H5.25A2.25 2.25 0 0 0 3 5.25v4.318c0 .597.237 1.17.659 1.591l9.581 9.581c.699.699 1.78.872 2.607.33a18.095 18.095 0 0 0 5.223-5.223c.542-.827.369-1.908-.33-2.607L11.16 3.66A2.25 2.25 0 0 0 9.568 3ZM6 6h.008v.008H6V6Z";
    public const string Escudo = "M9 12.75 11.25 15 15 9.75m-3-7.036A11.959 11.959 0 0 1 3.598 6 11.99 11.99 0 0 0 3 9.749c0 5.592 3.824 10.29 9 11.623 5.176-1.332 9-6.03 9-11.622 0-1.31-.21-2.571-.598-3.751h-.152c-3.196 0-6.1-1.248-8.25-3.285Z";
    public const string Reloj = "M12 6v6h4.5m4.5 0a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z";
    public const string Buscar = "m21 21-4.35-4.35m0 0A7.95 7.95 0 1 0 5.4 5.4a7.95 7.95 0 0 0 11.25 11.25Z";
    public const string Mas = "M12 9v6m3-3H9m12 0a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z";
    public const string Descargar = "M3 16.5v2.25A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75V16.5M16.5 12 12 16.5m0 0L7.5 12m4.5 4.5V3";
    public const string Ajustes = "M10.5 6h9.75M10.5 6a1.5 1.5 0 1 1-3 0m3 0a1.5 1.5 0 1 0-3 0M3.75 6H7.5m3 12h9.75m-9.75 0a1.5 1.5 0 0 1-3 0m3 0a1.5 1.5 0 0 0-3 0m-3.75 0H7.5m9-6h3.75m-3.75 0a1.5 1.5 0 0 1-3 0m3 0a1.5 1.5 0 0 0-3 0m-9.75 0h9.75";
    public const string Flecha = "M13.5 4.5 21 12m0 0-7.5 7.5M21 12H3";
}
```

`src/OpenSource1.Blazor/Navigation/CatalogoModulos.cs`:

```csharp
using OpenSource1.Application.Security;

namespace OpenSource1.Blazor.Navigation;

/// <summary>
/// Fuente única de grupos y módulos (Fix-Features A1). Menú, inicio, páginas de grupo, /buscar y la paleta Ctrl+K leen de
/// aquí; <c>RegistroModulosTests</c> comprueba que cada listado con @page está registrado y que cada ruta existe.
/// </summary>
public static class CatalogoModulos
{
    private static readonly string[] Admin = [ApplicationRoles.Administrator];
    private static readonly string[] AdminSupervisor = [ApplicationRoles.Administrator, ApplicationRoles.Supervisor];
    private const string Consultar = ApplicationPolicies.CanConsult;

    public static IReadOnlyList<GrupoModulo> Grupos { get; } =
    [
        new("clientes", "Clientes", IconosModulo.Personas, "Fichas de clientes y su panel de indicadores.", 1),
        new("productos", "Productos", IconosModulo.Cubo, "Catálogo de productos, categorías y panel.", 2),
        new("inventario", "Inventario", IconosModulo.Tabla, "Existencias, almacenes, diarios y movimientos.", 3),
        new("ventas", "Ventas", IconosModulo.Tarjeta, "Cobros, libro de clientes y estado de cuenta.", 4),
        new("facturacion", "Facturación", IconosModulo.Documento, "Borradores, facturas y notas de crédito.", 5),
        new("contabilidad", "Contabilidad", IconosModulo.Libro, "Plan de cuentas, movimientos, balance y costo de inventario.", 6),
        new("reportes", "Reportes", IconosModulo.Grafico, "Reportería y bitácora de actividad.", 7),
        new("configuracion", "Configuración", IconosModulo.Ajustes, "Catálogos auxiliares y parámetros contables.", 8),
        new("administracion", "Administración", IconosModulo.Escudo, "Usuarios, roles y estado de las cuentas.", 9),
    ];

    public static IReadOnlyList<Modulo> Modulos { get; } =
    [
        new("clientes", "clientes", "Clientes", "Fichas, búsqueda, filtros y reportes de clientes.", "/clientes", IconosModulo.Persona, Consultar, null, ["socios", "cliente", "rnc", "cedula"]),
        new("panel-clientes", "clientes", "Panel de clientes", "Indicadores y gráficos de clientes.", "/dashboard/clientes", IconosModulo.Grafico, Consultar, null, ["dashboard", "graficos"]),

        new("productos", "productos", "Productos", "Catálogo con precio, existencia y costo.", "/productos", IconosModulo.Cubo, Consultar, null, ["articulos", "items"]),
        new("categorias-producto", "productos", "Categorías de producto", "Jerarquía de categorías.", "/categorias-producto", IconosModulo.Etiqueta, Consultar, null, ["categoria", "familia"]),
        new("panel-productos", "productos", "Panel de productos", "Indicadores y gráficos de productos.", "/dashboard/productos", IconosModulo.Grafico, Consultar, null, ["dashboard", "graficos"]),

        new("existencias", "inventario", "Existencias", "Existencia por almacén a una fecha.", "/inventario/existencias", IconosModulo.Tabla, Consultar, null, ["stock", "disponible"]),
        new("almacenes", "inventario", "Almacenes", "Almacenes y almacén predeterminado.", "/almacenes", IconosModulo.Lista, Consultar, null, ["bodega", "deposito"]),
        new("diarios-inventario", "inventario", "Diarios de inventario", "Lotes de ajustes y transferencias.", "/diarios-inventario", IconosModulo.Lista, Consultar, null, ["ajuste", "transferencia", "lote"]),
        new("movimientos-producto", "inventario", "Movimientos de producto", "Libro de cantidades por producto.", "/inventario/movimientos-producto", IconosModulo.Lista, Consultar, null, ["kardex", "entradas", "salidas"]),
        new("movimientos-valor", "inventario", "Movimientos de valor", "Libro de costo por producto.", "/inventario/movimientos-valor", IconosModulo.Lista, Consultar, null, ["costo", "valor"]),

        new("cobros", "ventas", "Cobros", "Registro y aplicación de pagos de clientes.", "/cobros", IconosModulo.Tarjeta, Consultar, null, ["pagos", "recibos"]),
        new("movimientos-cliente", "ventas", "Movimientos de cliente", "Libro de clientes con importes pendientes.", "/ventas/movimientos-cliente", IconosModulo.Lista, Consultar, null, ["cxc", "cuentas por cobrar"]),
        new("estado-cuenta", "ventas", "Estado de cuenta (CxC)", "Saldos por cliente a una fecha de corte.", "/ventas/estado-cuenta", IconosModulo.Documento, Consultar, null, ["saldo", "cxc", "antiguedad"]),

        new("borradores-factura", "facturacion", "Borradores de factura", "Facturas de venta en preparación.", "/facturas-venta/borradores", IconosModulo.Documento, Consultar, null, ["factura", "borrador", "venta"]),
        new("facturas", "facturacion", "Facturas", "Facturas de venta posteadas.", "/facturas-venta", IconosModulo.Documento, Consultar, null, ["factura", "venta", "fv"]),
        new("borradores-nota-credito", "facturacion", "Borradores de nota de crédito", "Notas de crédito en preparación.", "/notas-credito-venta/borradores", IconosModulo.Documento, Consultar, null, ["nota de credito", "devolucion"]),
        new("notas-credito", "facturacion", "Notas de crédito", "Notas de crédito posteadas.", "/notas-credito-venta", IconosModulo.Documento, Consultar, null, ["nc", "devolucion"]),

        new("plan-cuentas", "contabilidad", "Plan de cuentas", "Catálogo de cuentas contables.", "/cuentas-contables", IconosModulo.Libro, Consultar, null, ["cuentas", "catalogo"]),
        new("movimientos-contables", "contabilidad", "Movimientos contables", "Libro de movimientos contables.", "/contabilidad/movimientos", IconosModulo.Lista, Consultar, null, ["asientos", "mayor", "diario"]),
        new("balance-comprobacion", "contabilidad", "Balance de comprobación", "Saldos por cuenta a una fecha.", "/contabilidad/balance-comprobacion", IconosModulo.Tabla, Consultar, null, ["balanza"]),
        new("costo-inventario", "contabilidad", "Costo de inventario", "Ajuste y posteo del costo de inventario.", "/contabilidad", IconosModulo.Reloj, Consultar, null, ["costo", "ajuste"]),

        new("reporteria", "reportes", "Reportería", "Reportes PDF y Excel de clientes y productos.", "/reporteria", IconosModulo.Descargar, Consultar, null, ["pdf", "excel", "reporte"]),
        new("bitacora", "reportes", "Bitácora", "Registro de actividad del sistema.", "/bitacora", IconosModulo.Reloj, null, AdminSupervisor, ["auditoria", "log", "actividad"]),

        new("terminos-pago", "configuracion", "Términos de pago", "Vencimiento y descuento por pronto pago.", "/terminos-pago", IconosModulo.Calendario, Consultar, null, ["credito", "dias"]),
        new("unidades-medida", "configuracion", "Unidades de medida", "Unidades y decimales de redondeo.", "/unidades-medida", IconosModulo.Ajustes, Consultar, null, ["um", "unidad"]),
        new("grupos-contables", "configuracion", "Grupos contables", "Grupos de negocio, producto, IVA e inventario.", "/grupos-contables", IconosModulo.Ajustes, Consultar, null, ["grupo", "iva", "itbis"]),
        new("grupos-cliente-contable", "configuracion", "Grupos de cliente contable", "Cuenta de CxC por grupo de cliente.", "/grupos-cliente-contable", IconosModulo.Ajustes, Consultar, null, ["cxc", "grupo cliente"]),
        new("setups-contables", "configuracion", "Setups contables", "Cuentas por combinación de grupos.", "/setups-contables", IconosModulo.Ajustes, Consultar, null, ["setup", "configuracion contable"]),
        new("fechas-registro", "configuracion", "Fechas de registro", "Rango de fechas de registro permitidas.", "/admin/fechas-registro", IconosModulo.Calendario, ApplicationPolicies.CanAdministrar, null, ["periodo", "cierre", "fechas"]),

        new("usuarios", "administracion", "Usuarios", "Cuentas, roles y estado de los usuarios.", "/admin/users", IconosModulo.Personas, null, Admin, ["roles", "cuentas"]),
    ];
}
```

`src/OpenSource1.Blazor/Navigation/IRegistroModulos.cs`:

```csharp
using System.Security.Claims;

namespace OpenSource1.Blazor.Navigation;

public interface IRegistroModulos
{
    IReadOnlyList<GrupoModulo> Grupos { get; }

    IReadOnlyList<Modulo> Todos { get; }

    /// <summary>Módulos que el usuario puede abrir (política Y rol), en el orden del catálogo. Anónimo: ninguno.</summary>
    Task<IReadOnlyList<Modulo>> VisiblesAsync(ClaimsPrincipal usuario);

    /// <summary>Grupos con al menos un módulo visible, por <c>Orden</c>.</summary>
    Task<IReadOnlyList<GrupoModulo>> GruposVisiblesAsync(ClaimsPrincipal usuario);

    GrupoModulo? BuscarGrupo(string? clave);

    /// <summary>Módulo cuya ruta es el prefijo (por segmentos) más largo de <paramref name="ruta"/>; query ignorada.</summary>
    Modulo? ModuloDeRuta(string? ruta);
}
```

`src/OpenSource1.Blazor/Navigation/RegistroModulos.cs`:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace OpenSource1.Blazor.Navigation;

public sealed class RegistroModulos(IAuthorizationService autorizacion) : IRegistroModulos
{
    public IReadOnlyList<GrupoModulo> Grupos => CatalogoModulos.Grupos;

    public IReadOnlyList<Modulo> Todos => CatalogoModulos.Modulos;

    public async Task<IReadOnlyList<Modulo>> VisiblesAsync(ClaimsPrincipal usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        if (usuario.Identity?.IsAuthenticated != true)
        {
            return [];
        }

        var visibles = new List<Modulo>();
        foreach (var modulo in Todos)
        {
            if (modulo.Roles is { Count: > 0 } roles && !roles.Any(usuario.IsInRole))
            {
                continue;
            }

            if (modulo.Politica is { } politica && !(await autorizacion.AuthorizeAsync(usuario, politica)).Succeeded)
            {
                continue;
            }

            visibles.Add(modulo);
        }

        return visibles;
    }

    public async Task<IReadOnlyList<GrupoModulo>> GruposVisiblesAsync(ClaimsPrincipal usuario)
    {
        var claves = (await VisiblesAsync(usuario)).Select(m => m.Grupo).ToHashSet(StringComparer.Ordinal);
        return Grupos.Where(g => claves.Contains(g.Clave)).OrderBy(g => g.Orden).ToList();
    }

    public GrupoModulo? BuscarGrupo(string? clave) =>
        Grupos.FirstOrDefault(g => string.Equals(g.Clave, clave, StringComparison.OrdinalIgnoreCase));

    public Modulo? ModuloDeRuta(string? ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta))
        {
            return null;
        }

        var sinQuery = ruta.Split('?', '#')[0];
        var camino = "/" + sinQuery.Trim('/');
        if (camino == "/")
        {
            return null;
        }

        return Todos
            .Where(m => string.Equals(camino, m.Ruta, StringComparison.OrdinalIgnoreCase)
                        || camino.StartsWith(m.Ruta + "/", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(m => m.Ruta.Length)
            .FirstOrDefault();
    }
}
```

`src/OpenSource1.Blazor/Navigation/TextoBusqueda.cs`:

```csharp
using System.Globalization;
using System.Text;

namespace OpenSource1.Blazor.Navigation;

/// <summary>Búsqueda de módulos sin acentos ni mayúsculas (misma regla que <c>app.search.js</c>).</summary>
public static class TextoBusqueda
{
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var descompuesto = texto.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    /// <summary>Módulos cuyo título, grupo o palabras clave contienen la consulta. Consulta vacía: ninguno.</summary>
    public static IReadOnlyList<Modulo> FiltrarModulos(IEnumerable<Modulo> modulos, string? consulta)
    {
        var q = Normalizar(consulta);
        if (q.Length == 0)
        {
            return [];
        }

        var titulosGrupo = CatalogoModulos.Grupos.ToDictionary(g => g.Clave, g => g.Titulo, StringComparer.Ordinal);
        return modulos
            .Where(m => Normalizar($"{m.Titulo} {titulosGrupo.GetValueOrDefault(m.Grupo)} {string.Join(' ', m.PalabrasClave)}").Contains(q, StringComparison.Ordinal))
            .ToList();
    }
}
```

Nota sobre el test `FiltrarModulos_…`: con el catálogo de arriba "cxc" coincide exactamente con `movimientos-cliente`, `estado-cuenta` y `grupos-cliente-contable` (en ese orden de catálogo); "facturación" coincide con todo el grupo Facturación por el título del grupo.

En `src/OpenSource1.Blazor/Components/_Imports.razor` añadir al final:

```razor
@using OpenSource1.Blazor.Navigation
```

- [ ] **Step 5: Ejecutar los tests y verlos pasar**

Run: `dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~RegistroModulosTests"`
Expected: PASS (todos). Si `CadaListadoTieneModulo_…` falla, el mensaje nombra la ruta: corregir el catálogo, nunca la lista de exclusiones.

- [ ] **Step 6: Build sin avisos y regresión del host**

Run: `dotnet build test.slnx -warnaserror` y `dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~OpenSource1.SmokeTests.Blazor"`
Expected: 0 avisos; tests del host Blazor en verde (incluido `UploadsAuthorizationTests`, que sigue usando `TestAuthHandler`).

- [ ] **Step 7: Commit**

```bash
git add src/OpenSource1.Blazor/Security/PoliticasBlazor.cs src/OpenSource1.Blazor/Navigation src/OpenSource1.Blazor/Program.cs src/OpenSource1.Blazor/Components/_Imports.razor tests/OpenSource1.SmokeTests/TestInfrastructure/PermisosTestAuthHandler.cs tests/OpenSource1.SmokeTests/TestInfrastructure/BlazorSsrFactory.cs tests/OpenSource1.SmokeTests/TestInfrastructure/RenderizadorComponentes.cs tests/OpenSource1.SmokeTests/TestInfrastructure/FormulariosSsr.cs tests/OpenSource1.SmokeTests/Blazor/RegistroModulosTests.cs
git commit -m "feat: registro unico de modulos y grupos de navegacion con visibilidad por permiso"
```

---
### Task A2: Menú lateral por grupos (`NavMenu.razor`)

**Grupo de paralelismo:** A-serie (tras A1). Puede correr a la vez que A3a, D1 y D2.

**Files:**
- Create: `src/OpenSource1.Blazor/Components/Layout/NavMenu.razor`
- Modify: `src/OpenSource1.Blazor/Components/Layout/MainLayout.razor:53-310` (bloque `<!-- Nav links -->` … `</nav>`)
- Test: `tests/OpenSource1.SmokeTests/Blazor/NavMenuTests.cs`

**Interfaces:**
- Consumes: `IRegistroModulos.VisiblesAsync`, `IRegistroModulos.Grupos`, `IRegistroModulos.ModuloDeRuta` (A1); `BlazorSsrFactory` (A1).
- Produces: componente `<NavMenu />` sin parámetros; marca `<details data-grupo="{clave}" open>` en el grupo actual y `aria-current="page"` en el módulo actual. La ruta `/modulos/{grupo}` (A5) abre ese grupo.

- [ ] **Step 1: Escribir el test que falla**

`tests/OpenSource1.SmokeTests/Blazor/NavMenuTests.cs`:

```csharp
using System.Net;
using System.Text.RegularExpressions;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>
/// Task A2: el menú lateral se genera desde el registro de módulos, agrupado con &lt;details&gt; nativos; el grupo de la ruta
/// actual llega abierto desde el servidor y el módulo actual marcado. /reporteria no llama a la API: sirve de página neutra.
/// </summary>
public sealed class NavMenuTests
{
    [Fact]
    public async Task Admin_GrupoDeLaRutaActualAbierto_YModuloMarcado()
    {
        using var app = new BlazorSsrFactory();
        var html = await HtmlAsync(app.Cliente("Administrador"), "/reporteria");

        Assert.Matches(new Regex("<details data-grupo=\"reportes\" open"), html);
        Assert.DoesNotMatch(new Regex("<details data-grupo=\"clientes\" open"), html);
        Assert.Matches(new Regex("<a href=\"/reporteria\"[^>]*aria-current=\"page\""), html);
        foreach (var grupo in new[] { "clientes", "productos", "inventario", "ventas", "facturacion", "contabilidad", "reportes", "configuracion", "administracion" })
        {
            Assert.Contains($"data-grupo=\"{grupo}\"", html);
        }

        Assert.Contains("href=\"/admin/users\"", html);
        Assert.Contains("href=\"/admin/fechas-registro\"", html);
    }

    [Fact]
    public async Task Ejecutor_NoVeAdministracionFechasNiBitacora()
    {
        using var app = new BlazorSsrFactory();
        var html = await HtmlAsync(app.Cliente("Ejecutor"), "/reporteria");

        Assert.DoesNotContain("data-grupo=\"administracion\"", html);
        Assert.DoesNotContain("href=\"/admin/users\"", html);
        Assert.DoesNotContain("href=\"/admin/fechas-registro\"", html);
        Assert.DoesNotContain("href=\"/bitacora\"", html);
        Assert.Contains("href=\"/clientes\"", html);
    }

    [Fact]
    public async Task Supervisor_VeBitacora()
    {
        using var app = new BlazorSsrFactory();
        var html = await HtmlAsync(app.Cliente("Supervisor"), "/reporteria");

        Assert.Contains("href=\"/bitacora\"", html);
        Assert.DoesNotContain("href=\"/admin/users\"", html);
    }

    [Fact]
    public async Task ModoColapsado_TemaYSesion_SeConservan()
    {
        using var app = new BlazorSsrFactory();
        var cliente = app.Cliente("Administrador");
        cliente.DefaultRequestHeaders.Add("Cookie", "axionerp-sidebar=collapsed; axionerp-theme=dark");

        var html = await HtmlAsync(cliente, "/reporteria");

        Assert.Contains("title=\"Mostrar menú\"", html);
        Assert.Contains("<html lang=\"es\" class=\"dark\"", html);
        Assert.Contains("action=\"/account/logout\"", html);
    }

    private static async Task<string> HtmlAsync(HttpClient cliente, string url)
    {
        var respuesta = await cliente.GetAsync(url);
        var html = await respuesta.Content.ReadAsStringAsync();
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, $"GET {url}: {respuesta.StatusCode}\n{html}");
        return html;
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~NavMenuTests"`
Expected: FAIL (`data-grupo` no existe en el HTML actual).

- [ ] **Step 3: Implementar `NavMenu.razor`**

`src/OpenSource1.Blazor/Components/Layout/NavMenu.razor`:

```razor
@* NavMenu — menú lateral por grupos (Fix-Features A2). Fuente: IRegistroModulos. El grupo de la ruta actual llega abierto
   desde el servidor (<details open>) y el módulo actual con aria-current; no hay JS. *@
@inject IRegistroModulos Registro
@inject NavigationManager Nav

<nav class="flex-1 space-y-1 overflow-y-auto px-3 py-4" role="navigation" aria-label="Menú principal" data-testid="nav-menu">
    <a href="/" class="@ClaseEnlace(esInicio)" aria-current="@(esInicio ? "page" : null)">
        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.8" stroke="currentColor" class="size-4 shrink-0" aria-hidden="true">
            <path stroke-linecap="round" stroke-linejoin="round" d="@IconosModulo.Inicio" />
        </svg>
        Inicio
    </a>
    @foreach (var grupo in grupos)
    {
        var abierto = grupo.Clave == grupoActual;
        <details data-grupo="@grupo.Clave" open="@abierto" class="group/grupo">
            <summary class="flex cursor-pointer list-none items-center gap-2.5 rounded-lg px-3 py-2.5 text-sm font-semibold text-slate-700 hover:bg-slate-100 dark:text-slate-200 dark:hover:bg-slate-800 [&::-webkit-details-marker]:hidden">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.8" stroke="currentColor" class="size-4 shrink-0 text-slate-400" aria-hidden="true">
                    <path stroke-linecap="round" stroke-linejoin="round" d="@grupo.Icono" />
                </svg>
                <span class="flex-1">@grupo.Titulo</span>
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="size-3.5 shrink-0 text-slate-400 transition-transform group-open/grupo:rotate-90" aria-hidden="true">
                    <path stroke-linecap="round" stroke-linejoin="round" d="m8.25 4.5 7.5 7.5-7.5 7.5" />
                </svg>
            </summary>
            <div class="mb-2 mt-0.5 space-y-0.5 border-l border-slate-100 pl-3 ml-5 dark:border-slate-800">
                <a href="@($"/modulos/{grupo.Clave}")" class="block rounded-md px-2.5 py-1.5 text-xs font-semibold text-slate-400 hover:text-brand-600">Ver grupo</a>
                @foreach (var modulo in modulosPorGrupo[grupo.Clave])
                {
                    var actual = modulo.Clave == moduloActual;
                    <a href="@modulo.Ruta" class="@ClaseSubenlace(actual)" aria-current="@(actual ? "page" : null)">@modulo.Titulo</a>
                }
            </div>
        </details>
    }
</nav>

@code {
    [CascadingParameter] private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    private IReadOnlyList<GrupoModulo> grupos = [];
    private Dictionary<string, List<Modulo>> modulosPorGrupo = [];
    private string? grupoActual;
    private string? moduloActual;
    private bool esInicio;

    protected override async Task OnParametersSetAsync()
    {
        var ruta = Nav.ToBaseRelativePath(Nav.Uri);
        var camino = ruta.Split('?', '#')[0].Trim('/');
        esInicio = camino.Length == 0;

        var modulo = Registro.ModuloDeRuta(ruta);
        moduloActual = modulo?.Clave;
        grupoActual = modulo?.Grupo
            ?? (camino.StartsWith("modulos/", StringComparison.OrdinalIgnoreCase) ? camino["modulos/".Length..].ToLowerInvariant() : null);

        if (AuthenticationStateTask is null)
        {
            return;
        }

        var usuario = (await AuthenticationStateTask).User;
        var visibles = await Registro.VisiblesAsync(usuario);
        modulosPorGrupo = visibles.GroupBy(m => m.Grupo).ToDictionary(g => g.Key, g => g.ToList());
        grupos = Registro.Grupos.Where(g => modulosPorGrupo.ContainsKey(g.Clave)).OrderBy(g => g.Orden).ToList();
    }

    private static string ClaseEnlace(bool actual) => actual
        ? "flex items-center gap-2.5 rounded-lg px-3 py-2.5 text-sm font-semibold bg-brand-50 text-brand-700 dark:bg-brand-500/10 dark:text-brand-400"
        : "flex items-center gap-2.5 rounded-lg px-3 py-2.5 text-sm font-medium text-slate-600 hover:bg-slate-100 hover:text-slate-900 dark:text-slate-300 dark:hover:bg-slate-800 dark:hover:text-slate-100";

    private static string ClaseSubenlace(bool actual) => actual
        ? "block rounded-md px-2.5 py-1.5 text-sm font-semibold bg-brand-50 text-brand-700 dark:bg-brand-500/10 dark:text-brand-400"
        : "block rounded-md px-2.5 py-1.5 text-sm text-slate-600 hover:bg-slate-100 hover:text-slate-900 dark:text-slate-300 dark:hover:bg-slate-800";
}
```

En `MainLayout.razor`, sustituir TODO el bloque desde la línea `    <!-- Nav links -->` hasta su `    </nav>` de cierre (líneas 53-310) por:

```razor
    <!-- Nav links (Fix-Features A2: generado desde el registro de módulos) -->
    <NavMenu />
```

No tocar la marca, el botón "Ocultar menú", el bloque de sesión, el tema ni el botón "Mostrar menú".

- [ ] **Step 4: Ejecutar los tests**

Run: `dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~NavMenuTests|FullyQualifiedName~RegistroModulosTests"`
Expected: PASS.

- [ ] **Step 5: Build sin avisos**

Run: `dotnet build test.slnx -warnaserror`
Expected: 0 avisos, 0 errores.

- [ ] **Step 6: Commit**

```bash
git add src/OpenSource1.Blazor/Components/Layout/NavMenu.razor src/OpenSource1.Blazor/Components/Layout/MainLayout.razor tests/OpenSource1.SmokeTests/Blazor/NavMenuTests.cs
git commit -m "feat: menu lateral por grupos desde el registro de modulos con el grupo actual abierto"
```

---

### Task A3a: Búsqueda global — API (`GET api/busqueda`)

**Grupo de paralelismo:** Inicio (en paralelo con A1, A2, D1, D2). Solo toca Application, Infrastructure, Api y sus tests.

**Files:**
- Create: `src/OpenSource1.Application/Features/Busqueda/Dtos/BusquedaGlobalResponse.cs`
- Create: `src/OpenSource1.Application/Features/Busqueda/IBusquedaGlobalRepository.cs`
- Create: `src/OpenSource1.Application/Features/Busqueda/Queries/BuscarGlobalQuery.cs`
- Create: `src/OpenSource1.Application/Features/Busqueda/Handlers/BuscarGlobalQueryHandler.cs`
- Create: `src/OpenSource1.Infrastructure/Data/Queries/DapperBusquedaGlobalRepository.cs`
- Modify: `src/OpenSource1.Infrastructure/Data/DependencyInjection.cs` (tras `services.AddScoped<IFacturaVentaReadRepository, DapperFacturaVentaReadRepository>();`)
- Create: `src/OpenSource1.Api/Controllers/BusquedaController.cs`
- Test: `tests/OpenSource1.SmokeTests/Features/Busqueda/BuscarGlobalQueryHandlerTests.cs`
- Test: `tests/OpenSource1.SmokeTests/Api/BusquedaApiTests.cs` (Postgres, Docker)

**Interfaces:**
- Consumes: `IDbSession`, `FilterExpressionBuilder.EscaparMetacaracteresLike` (internal, misma asamblea), `ResultExtensions.ToActionResult`.
- Produces (A3b, A4):
  - `namespace OpenSource1.Application.Features.Busqueda.Dtos`: `record ResultadoBusqueda(string Tipo, string Id, string Titulo, string? Subtitulo, string Ruta)`, `record GrupoResultadosBusqueda(string Tipo, string Titulo, IReadOnlyList<ResultadoBusqueda> Items)`, `record BusquedaGlobalResponse(IReadOnlyList<GrupoResultadosBusqueda> Grupos)`, `static class TiposResultadoBusqueda { Clientes = "clientes", Productos = "productos", Facturas = "facturas", BorradoresFactura = "borradoresFactura", NotasCredito = "notasCredito", BorradoresNotaCredito = "borradoresNotaCredito" }`.
  - Siempre 6 grupos en ese orden (vacíos incluidos). Rutas: `/clientes/{id}`, `/productos/{id}`, `/facturas-venta/{numero}`, `/facturas-venta/borradores/{id}`, `/notas-credito-venta/{numero}`, `/notas-credito-venta/borradores/{id}`.
  - `GET api/busqueda?q=&limite=5` (CanConsult): 200 `BusquedaGlobalResponse`; 400 con `errors.q` (q recortado fuera de 2–100) o `errors.limite` (fuera de 1–20).

- [ ] **Step 1: Test unitario del handler (falla: tipos inexistentes)**

`tests/OpenSource1.SmokeTests/Features/Busqueda/BuscarGlobalQueryHandlerTests.cs`:

```csharp
using Moq;
using OpenSource1.Application.Features.Busqueda;
using OpenSource1.Application.Features.Busqueda.Dtos;
using OpenSource1.Application.Features.Busqueda.Handlers;
using OpenSource1.Application.Features.Busqueda.Queries;

namespace OpenSource1.SmokeTests.Features.Busqueda;

public sealed class BuscarGlobalQueryHandlerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" a ")]
    public async Task QFueraDeRango_FallaConCampoQ_SinLlamarAlRepositorio(string? q)
    {
        var repositorio = new Mock<IBusquedaGlobalRepository>(MockBehavior.Strict);

        var resultado = await new BuscarGlobalQueryHandler(repositorio.Object).Handle(new BuscarGlobalQuery(q), default);

        Assert.True(resultado.EsFallo);
        Assert.Equal(("busqueda.q_invalida", "q"), (resultado.Errores[0].Codigo, resultado.Errores[0].Campo));
    }

    [Fact]
    public async Task QDeMasDe100Caracteres_Falla()
    {
        var repositorio = new Mock<IBusquedaGlobalRepository>(MockBehavior.Strict);

        var resultado = await new BuscarGlobalQueryHandler(repositorio.Object).Handle(new BuscarGlobalQuery(new string('x', 101)), default);

        Assert.Equal("q", resultado.Errores[0].Campo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public async Task LimiteFueraDeRango_FallaConCampoLimite(int limite)
    {
        var repositorio = new Mock<IBusquedaGlobalRepository>(MockBehavior.Strict);

        var resultado = await new BuscarGlobalQueryHandler(repositorio.Object).Handle(new BuscarGlobalQuery("abc", limite), default);

        Assert.Equal(("busqueda.limite_invalido", "limite"), (resultado.Errores[0].Codigo, resultado.Errores[0].Campo));
    }

    [Fact]
    public async Task QValido_SeRecortaYSeConsultaConElLimite()
    {
        var esperado = new BusquedaGlobalResponse([]);
        var repositorio = new Mock<IBusquedaGlobalRepository>();
        repositorio.Setup(r => r.BuscarAsync("tornillo 50%", 7, It.IsAny<CancellationToken>())).ReturnsAsync(esperado);

        var resultado = await new BuscarGlobalQueryHandler(repositorio.Object).Handle(new BuscarGlobalQuery("  tornillo 50%  ", 7), default);

        Assert.True(resultado.EsExito);
        Assert.Same(esperado, resultado.Valor);
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~BuscarGlobalQueryHandlerTests"`
Expected: FAIL de compilación (`OpenSource1.Application.Features.Busqueda` no existe).

- [ ] **Step 3: Implementar DTOs, contrato, query y handler**

`src/OpenSource1.Application/Features/Busqueda/Dtos/BusquedaGlobalResponse.cs`:

```csharp
namespace OpenSource1.Application.Features.Busqueda.Dtos;

/// <summary>Un resultado de la búsqueda global: <c>Ruta</c> es la ruta de la UI (Blazor) que abre el registro.</summary>
public sealed record ResultadoBusqueda(string Tipo, string Id, string Titulo, string? Subtitulo, string Ruta);

public sealed record GrupoResultadosBusqueda(string Tipo, string Titulo, IReadOnlyList<ResultadoBusqueda> Items);

/// <summary>Siempre los 6 grupos de <see cref="TiposResultadoBusqueda"/>, en ese orden, aunque estén vacíos.</summary>
public sealed record BusquedaGlobalResponse(IReadOnlyList<GrupoResultadosBusqueda> Grupos);

public static class TiposResultadoBusqueda
{
    public const string Clientes = "clientes";
    public const string Productos = "productos";
    public const string Facturas = "facturas";
    public const string BorradoresFactura = "borradoresFactura";
    public const string NotasCredito = "notasCredito";
    public const string BorradoresNotaCredito = "borradoresNotaCredito";
}
```

`src/OpenSource1.Application/Features/Busqueda/IBusquedaGlobalRepository.cs`:

```csharp
using OpenSource1.Application.Features.Busqueda.Dtos;

namespace OpenSource1.Application.Features.Busqueda;

/// <summary>
/// Búsqueda global de solo lectura (Fix-Features A3): hasta <paramref name="limite"/> resultados por tipo, <c>ILIKE</c> con
/// los metacaracteres del término escapados, sin registros con borrado lógico.
/// </summary>
public interface IBusquedaGlobalRepository
{
    Task<BusquedaGlobalResponse> BuscarAsync(string termino, int limite, CancellationToken cancellationToken = default);
}
```

`src/OpenSource1.Application/Features/Busqueda/Queries/BuscarGlobalQuery.cs`:

```csharp
using MediatR;
using OpenSource1.Application.Features.Busqueda.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Busqueda.Queries;

public sealed record BuscarGlobalQuery(string? Q, int Limite = BuscarGlobalQuery.LimitePorDefecto)
    : IRequest<Result<BusquedaGlobalResponse>>
{
    public const int LimitePorDefecto = 5;
    public const int LimiteMaximo = 20;
    public const int LongitudMinima = 2;
    public const int LongitudMaxima = 100;
}
```

`src/OpenSource1.Application/Features/Busqueda/Handlers/BuscarGlobalQueryHandler.cs`:

```csharp
using MediatR;
using OpenSource1.Application.Features.Busqueda.Dtos;
using OpenSource1.Application.Features.Busqueda.Queries;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Busqueda.Handlers;

public sealed class BuscarGlobalQueryHandler(IBusquedaGlobalRepository repositorio)
    : IRequestHandler<BuscarGlobalQuery, Result<BusquedaGlobalResponse>>
{
    public async Task<Result<BusquedaGlobalResponse>> Handle(BuscarGlobalQuery request, CancellationToken cancellationToken)
    {
        var termino = request.Q?.Trim() ?? string.Empty;
        if (termino.Length is < BuscarGlobalQuery.LongitudMinima or > BuscarGlobalQuery.LongitudMaxima)
        {
            return Result<BusquedaGlobalResponse>.Fallo(new Error(
                "busqueda.q_invalida",
                $"El texto a buscar debe tener entre {BuscarGlobalQuery.LongitudMinima} y {BuscarGlobalQuery.LongitudMaxima} caracteres.",
                "q"));
        }

        if (request.Limite is < 1 or > BuscarGlobalQuery.LimiteMaximo)
        {
            return Result<BusquedaGlobalResponse>.Fallo(new Error(
                "busqueda.limite_invalido", $"El límite por tipo debe estar entre 1 y {BuscarGlobalQuery.LimiteMaximo}.", "limite"));
        }

        return Result<BusquedaGlobalResponse>.Exito(await repositorio.BuscarAsync(termino, request.Limite, cancellationToken));
    }
}
```

- [ ] **Step 4: Ejecutar el test unitario**

Run: `dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~BuscarGlobalQueryHandlerTests"`
Expected: PASS.

- [ ] **Step 5: Test de integración contra Postgres (falla: sin endpoint)**

`tests/OpenSource1.SmokeTests/Api/BusquedaApiTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using OpenSource1.Application.Features.Busqueda.Dtos;
using OpenSource1.SmokeTests.TestInfrastructure;
using static OpenSource1.SmokeTests.TestInfrastructure.LibroClientesSemilla;

namespace OpenSource1.SmokeTests.Api;

/// <summary>
/// Fix-Features A3: GET api/busqueda contra Postgres real. Cada test usa un marcador único para no depender de los datos de
/// otros tests de la colección. Las notas de crédito (posteadas y borradores) se ejercitan por ejecución de su SQL (grupos
/// presentes y vacíos para un marcador que no existe). REQUIERE DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class BusquedaApiTests : IClassFixture<PostgresTestFixture>
{
    private readonly PostgresTestFixture _fixture;
    private readonly HttpClient _client;

    public BusquedaApiTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateFactory().CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Encuentra_Socios_Productos_Facturas_YBorradores_ConSusRutas()
    {
        var marcador = Marcador();
        var client = Rol("Administrador");
        await using var conexion = await AbrirAsync();
        var socio = await InsertarSocioAsync(conexion, $"Comercial {marcador}");
        var producto = await CrearProductoAsync(client, $"Tornillo {marcador}");
        var numero = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        await InsertarFacturaAsync(conexion, numero, socio, new DateOnly(2026, 9, 10), [], [new LineaIva("EXENTO", 0m, 1m, 0m)], null, $"Cliente {marcador}");
        var borrador = await client.PostAsJsonAsync("/api/facturas-venta/borradores", new { socioNegocioId = socio });
        Assert.True(borrador.StatusCode == HttpStatusCode.Created, await borrador.Content.ReadAsStringAsync());

        var resultado = await BuscarAsync(client, marcador);

        Assert.Equal(
            [TiposResultadoBusqueda.Clientes, TiposResultadoBusqueda.Productos, TiposResultadoBusqueda.Facturas,
             TiposResultadoBusqueda.BorradoresFactura, TiposResultadoBusqueda.NotasCredito, TiposResultadoBusqueda.BorradoresNotaCredito],
            resultado.Grupos.Select(g => g.Tipo));
        var cliente = Assert.Single(Grupo(resultado, TiposResultadoBusqueda.Clientes));
        Assert.Equal((socio.ToString(), $"/clientes/{socio}"), (cliente.Id, cliente.Ruta));
        var productoItem = Assert.Single(Grupo(resultado, TiposResultadoBusqueda.Productos));
        Assert.Equal($"/productos/{producto}", productoItem.Ruta);
        var factura = Assert.Single(Grupo(resultado, TiposResultadoBusqueda.Facturas));
        Assert.Equal((numero, $"/facturas-venta/{numero}"), (factura.Titulo, factura.Ruta));
        var borradorItem = Assert.Single(Grupo(resultado, TiposResultadoBusqueda.BorradoresFactura));
        Assert.StartsWith("/facturas-venta/borradores/", borradorItem.Ruta);
        Assert.Empty(Grupo(resultado, TiposResultadoBusqueda.NotasCredito));
        Assert.Empty(Grupo(resultado, TiposResultadoBusqueda.BorradoresNotaCredito));
    }

    [Fact]
    public async Task LimitePorTipo_YExcluyeBorradosLogicos()
    {
        var marcador = Marcador();
        await using var conexion = await AbrirAsync();
        await InsertarSocioAsync(conexion, $"Uno {marcador}");
        await InsertarSocioAsync(conexion, $"Dos {marcador}");
        var borrado = await InsertarSocioAsync(conexion, $"Tres {marcador}");
        await conexion.ExecuteAsync("UPDATE \"SociosNegocio\" SET \"IsDeleted\" = true WHERE \"Id\" = @Id", new { Id = borrado });

        var todos = await BuscarAsync(Rol("Administrador"), marcador);
        var limitado = await BuscarAsync(Rol("Administrador"), marcador, "&limite=1");

        Assert.Equal(2, Grupo(todos, TiposResultadoBusqueda.Clientes).Count);
        Assert.DoesNotContain(Grupo(todos, TiposResultadoBusqueda.Clientes), r => r.Id == borrado.ToString());
        Assert.Single(Grupo(limitado, TiposResultadoBusqueda.Clientes));
    }

    [Fact]
    public async Task Metacaracteres_SeBuscanLiteralmente()
    {
        var marcador = Marcador();
        await using var conexion = await AbrirAsync();
        var literal = await InsertarSocioAsync(conexion, $"Cien%_\\{marcador}");
        await InsertarSocioAsync(conexion, $"CienXY{marcador}");

        var resultado = await BuscarAsync(Rol("Administrador"), $"%_\\{marcador}");

        var unico = Assert.Single(Grupo(resultado, TiposResultadoBusqueda.Clientes));
        Assert.Equal(literal.ToString(), unico.Id);
        Assert.Empty(Grupo(await BuscarAsync(Rol("Administrador"), $"'{marcador}"), TiposResultadoBusqueda.Clientes));
    }

    [Fact]
    public async Task Validacion_Q_Limite_YPermisos()
    {
        var client = Rol("Administrador");
        await AssertErrorAsync(await client.GetAsync("/api/busqueda?q=a"), "q");
        await AssertErrorAsync(await client.GetAsync("/api/busqueda?q=%20%20a%20%20"), "q");
        await AssertErrorAsync(await client.GetAsync($"/api/busqueda?q={new string('x', 101)}"), "q");
        await AssertErrorAsync(await client.GetAsync("/api/busqueda"), "q");
        await AssertErrorAsync(await client.GetAsync("/api/busqueda?q=abc&limite=0"), "limite");
        await AssertErrorAsync(await client.GetAsync("/api/busqueda?q=abc&limite=21"), "limite");

        Assert.Equal(HttpStatusCode.OK, (await Rol("Ejecutor").GetAsync("/api/busqueda?q=abc")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Rol("Supervisor").GetAsync("/api/busqueda?q=abc")).StatusCode);

        var anonimo = new HttpRequestMessage(HttpMethod.Get, "/api/busqueda?q=abc");
        anonimo.Headers.Add("X-Test-Anonymous", "true");
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(anonimo)).StatusCode);
    }

    private static string Marcador() => "Zq" + Guid.NewGuid().ToString("N")[..10];

    private static IReadOnlyList<ResultadoBusqueda> Grupo(BusquedaGlobalResponse respuesta, string tipo) =>
        respuesta.Grupos.Single(g => g.Tipo == tipo).Items;

    private static async Task<BusquedaGlobalResponse> BuscarAsync(HttpClient client, string q, string extra = "")
    {
        var response = await client.GetAsync($"/api/busqueda?q={Uri.EscapeDataString(q)}{extra}");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<BusquedaGlobalResponse>())!;
    }

    private static async Task<Guid> CrearProductoAsync(HttpClient client, string nombre)
    {
        var response = await client.PostAsJsonAsync(
            "/api/productos", new { codigo = $"BG{Guid.NewGuid():N}"[..11], nombre, precioVenta = 10.03m });
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, string campo)
    {
        var cuerpo = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"Se esperaba 400 y llegó {response.StatusCode}: {cuerpo}");
        Assert.True(JsonDocument.Parse(cuerpo).RootElement.GetProperty("errors").TryGetProperty(campo, out _), cuerpo);
    }

    private async Task<NpgsqlConnection> AbrirAsync()
    {
        var conexion = new NpgsqlConnection(_fixture.AppConnectionString);
        await conexion.OpenAsync();
        return conexion;
    }

    private HttpClient Rol(string role)
    {
        var client = _client;
        client.DefaultRequestHeaders.Remove("X-Test-Anonymous");
        client.DefaultRequestHeaders.Remove("X-Test-User");
        client.DefaultRequestHeaders.Remove("X-Test-Roles");
        client.DefaultRequestHeaders.Add("X-Test-User", role.ToLowerInvariant());
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        return client;
    }
}
```

- [ ] **Step 6: Ejecutar y ver que falla**

Run: `env DOCKER_CONTEXT=default dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~BusquedaApiTests"`
Expected: FAIL (404 en `/api/busqueda`).

- [ ] **Step 7: Implementar repositorio Dapper, DI y controlador**

`src/OpenSource1.Infrastructure/Data/Queries/DapperBusquedaGlobalRepository.cs`:

```csharp
using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.Busqueda;
using OpenSource1.Application.Features.Busqueda.Dtos;

namespace OpenSource1.Infrastructure.Data.Queries;

/// <summary>
/// Búsqueda global (Fix-Features A3). Un SELECT por tipo con <c>LIMIT @Limite</c>; el término se escapa con
/// <see cref="FilterExpressionBuilder.EscaparMetacaracteresLike"/> y se envuelve en <c>%…%</c> (sin sintaxis de comodines del
/// usuario: todo es literal). Socios, productos y borradores excluyen el borrado lógico; los documentos posteados no lo tienen.
/// </summary>
public sealed class DapperBusquedaGlobalRepository(IDbSession session) : IBusquedaGlobalRepository
{
    private const string SqlClientes = """
        SELECT s."Id"::text AS "Id", s."NombreComercial" AS "Titulo",
               concat_ws(' · ', s."Codigo", NULLIF(s."RazonSocial", ''), NULLIF(s."NumeroDocumentoFiscal", '')) AS "Subtitulo"
        FROM "SociosNegocio" s
        WHERE s."IsDeleted" = false
          AND (s."Codigo" ILIKE @Patron ESCAPE '\' OR s."NombreComercial" ILIKE @Patron ESCAPE '\'
               OR s."RazonSocial" ILIKE @Patron ESCAPE '\' OR s."NumeroDocumentoFiscal" ILIKE @Patron ESCAPE '\')
        ORDER BY s."NombreComercial", s."Id"
        LIMIT @Limite
        """;

    private const string SqlProductos = """
        SELECT p."Id"::text AS "Id", p."Nombre" AS "Titulo", p."Codigo" AS "Subtitulo"
        FROM "Productos" p
        WHERE p."IsDeleted" = false
          AND (p."Codigo" ILIKE @Patron ESCAPE '\' OR p."Nombre" ILIKE @Patron ESCAPE '\')
        ORDER BY p."Nombre", p."Id"
        LIMIT @Limite
        """;

    private const string SqlFacturas = """
        SELECT f."Numero" AS "Id", f."Numero" AS "Titulo",
               concat_ws(' · ', f."NombreFacturacion", to_char(f."FechaRegistro", 'YYYY-MM-DD')) AS "Subtitulo"
        FROM "FacturasVenta" f
        WHERE f."Numero" ILIKE @Patron ESCAPE '\' OR f."NombreFacturacion" ILIKE @Patron ESCAPE '\'
        ORDER BY f."Numero" DESC
        LIMIT @Limite
        """;

    private const string SqlBorradoresFactura = """
        SELECT b."Id"::text AS "Id", b."Numero" AS "Titulo", b."NombreFacturacion" AS "Subtitulo"
        FROM "FacturasVentaBorrador" b
        WHERE b."IsDeleted" = false
          AND (b."Numero" ILIKE @Patron ESCAPE '\' OR b."NombreFacturacion" ILIKE @Patron ESCAPE '\')
        ORDER BY b."CreatedAtUtc" DESC, b."Id"
        LIMIT @Limite
        """;

    private const string SqlNotasCredito = """
        SELECT n."Numero" AS "Id", n."Numero" AS "Titulo",
               concat_ws(' · ', n."NombreFacturacion", 'Factura ' || n."FacturaVentaNumero") AS "Subtitulo"
        FROM "NotasCreditoVenta" n
        WHERE n."Numero" ILIKE @Patron ESCAPE '\'
        ORDER BY n."Numero" DESC
        LIMIT @Limite
        """;

    private const string SqlBorradoresNotaCredito = """
        SELECT n."Id"::text AS "Id", n."Numero" AS "Titulo",
               concat_ws(' · ', n."NombreFacturacion", 'Factura ' || n."FacturaVentaNumero") AS "Subtitulo"
        FROM "NotasCreditoVentaBorrador" n
        WHERE n."IsDeleted" = false AND n."Numero" ILIKE @Patron ESCAPE '\'
        ORDER BY n."CreatedAtUtc" DESC, n."Id"
        LIMIT @Limite
        """;

    public async Task<BusquedaGlobalResponse> BuscarAsync(string termino, int limite, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(termino);
        var parametros = new { Patron = $"%{FilterExpressionBuilder.EscaparMetacaracteresLike(termino)}%", Limite = limite };
        await session.EnsureOpenAsync(cancellationToken);

        return new BusquedaGlobalResponse(
        [
            await GrupoAsync(TiposResultadoBusqueda.Clientes, "Clientes", SqlClientes, id => $"/clientes/{id}"),
            await GrupoAsync(TiposResultadoBusqueda.Productos, "Productos", SqlProductos, id => $"/productos/{id}"),
            await GrupoAsync(TiposResultadoBusqueda.Facturas, "Facturas", SqlFacturas, id => $"/facturas-venta/{Uri.EscapeDataString(id)}"),
            await GrupoAsync(TiposResultadoBusqueda.BorradoresFactura, "Borradores de factura", SqlBorradoresFactura, id => $"/facturas-venta/borradores/{id}"),
            await GrupoAsync(TiposResultadoBusqueda.NotasCredito, "Notas de crédito", SqlNotasCredito, id => $"/notas-credito-venta/{Uri.EscapeDataString(id)}"),
            await GrupoAsync(TiposResultadoBusqueda.BorradoresNotaCredito, "Borradores de nota de crédito", SqlBorradoresNotaCredito, id => $"/notas-credito-venta/borradores/{id}"),
        ]);

        async Task<GrupoResultadosBusqueda> GrupoAsync(string tipo, string titulo, string sql, Func<string, string> ruta)
        {
            var filas = await session.Connection.QueryAsync<Fila>(
                new CommandDefinition(sql, parametros, session.CurrentTransaction, cancellationToken: cancellationToken));
            return new GrupoResultadosBusqueda(
                tipo, titulo, filas.Select(f => new ResultadoBusqueda(tipo, f.Id, f.Titulo, f.Subtitulo, ruta(f.Id))).ToList());
        }
    }

    private sealed class Fila
    {
        public string Id { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string? Subtitulo { get; set; }
    }
}
```

En `src/OpenSource1.Infrastructure/Data/DependencyInjection.cs`, justo después de `services.AddScoped<IFacturaVentaReadRepository, DapperFacturaVentaReadRepository>();`:

```csharp
        services.AddScoped<IBusquedaGlobalRepository, DapperBusquedaGlobalRepository>();
```

con `using OpenSource1.Application.Features.Busqueda;` en la cabecera del archivo.

`src/OpenSource1.Api/Controllers/BusquedaController.cs`:

```csharp
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenSource1.Api.Infrastructure;
using OpenSource1.Application.Features.Busqueda.Dtos;
using OpenSource1.Application.Features.Busqueda.Queries;
using OpenSource1.Application.Security;

namespace OpenSource1.Api.Controllers;

/// <summary>
/// Búsqueda global (Fix-Features A3): clientes, productos, facturas posteadas, borradores de factura, notas de crédito y sus
/// borradores. Hasta <c>limite</c> (1–20, por defecto 5) resultados por tipo; <c>q</c> recortado de 2 a 100 caracteres.
/// </summary>
[ApiController]
[Route("api/busqueda")]
public sealed class BusquedaController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<BusquedaGlobalResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Buscar(
        [FromQuery] string? q,
        [FromQuery] int limite = BuscarGlobalQuery.LimitePorDefecto,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new BuscarGlobalQuery(q, limite), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }
}
```

- [ ] **Step 8: Ejecutar los tests de la búsqueda**

Run: `env DOCKER_CONTEXT=default dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~BusquedaApiTests|FullyQualifiedName~BuscarGlobalQueryHandlerTests"`
Expected: PASS. Si `Metacaracteres_SeBuscanLiteralmente` devuelve 2 filas, el `ESCAPE '\'` no llegó literal a SQL: revisar que las cadenas sean raw string literals (`"""`).

- [ ] **Step 9: Build sin avisos y documento OpenAPI**

Run: `dotnet build test.slnx -warnaserror` y `dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~OpenApi|FullyQualifiedName~Swagger"`
Expected: 0 avisos; si existe test del documento OpenAPI, sigue verde con la ruta nueva.

- [ ] **Step 10: Commit**

```bash
git add src/OpenSource1.Application/Features/Busqueda src/OpenSource1.Infrastructure/Data/Queries/DapperBusquedaGlobalRepository.cs src/OpenSource1.Infrastructure/Data/DependencyInjection.cs src/OpenSource1.Api/Controllers/BusquedaController.cs tests/OpenSource1.SmokeTests/Features/Busqueda tests/OpenSource1.SmokeTests/Api/BusquedaApiTests.cs
git commit -m "feat: busqueda global de solo lectura en la API para clientes, productos y documentos"
```

---

### Task A3b: Búsqueda global — cliente tipado, barra superior y página `/buscar`

**Grupo de paralelismo:** A-serie (tras A2 y A3a).

**Files:**
- Create: `src/OpenSource1.Blazor/Services/IBusquedaApiClient.cs`
- Create: `src/OpenSource1.Blazor/Services/BusquedaApiClient.cs`
- Modify: `src/OpenSource1.Blazor/Program.cs` (registro del cliente tipado, junto a los demás `AddHttpClient<…>`)
- Create: `src/OpenSource1.Blazor/Components/Layout/BarraBusqueda.razor`
- Modify: `src/OpenSource1.Blazor/Components/Layout/MainLayout.razor` (barra superior dentro de la columna de contenido, línea 384)
- Create: `src/OpenSource1.Blazor/Components/Pages/Buscar.razor`
- Test: `tests/OpenSource1.SmokeTests/Blazor/BusquedaApiClientTests.cs`
- Test: `tests/OpenSource1.SmokeTests/Blazor/BuscarPaginaTests.cs`

**Interfaces:**
- Consumes: `BusquedaGlobalResponse`, `TiposResultadoBusqueda` (A3a); `IRegistroModulos`, `TextoBusqueda.FiltrarModulos` (A1); `ConsultasApi.GetAsync`, `ConsultaResultado<T>` (existentes).
- Produces:
  - `interface IBusquedaApiClient { Task<ConsultaResultado<BusquedaGlobalResponse>> BuscarAsync(string q, int limite = 5, CancellationToken cancellationToken = default); }` — red caída = excepción.
  - `<BarraBusqueda />` con `<form method="get" action="/buscar" role="search">` e `<input id="busqueda-global" name="q">` (A4 le añade la paleta y la isla).
  - Página `/buscar?q=` (`[Authorize]`) y `public static string Buscar.VerTodosUrl(string tipo, string q)`.

- [ ] **Step 1: Tests del cliente tipado (fallan)**

`tests/OpenSource1.SmokeTests/Blazor/BusquedaApiClientTests.cs`:

```csharp
extern alias BlazorApp;

using System.Net;
using System.Text;
using BlazorApp::OpenSource1.Blazor.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace OpenSource1.SmokeTests.Blazor;

public sealed class BusquedaApiClientTests
{
    [Fact]
    public async Task Buscar_EscapaQ_EnviaLimite_YDeserializa()
    {
        const string json = """
            {"grupos":[{"tipo":"clientes","titulo":"Clientes","items":[{"tipo":"clientes","id":"1","titulo":"Comercial A&B","subtitulo":null,"ruta":"/clientes/1"}]}]}
            """;
        var handler = new Grabador(HttpStatusCode.OK, json);
        var cliente = new BusquedaApiClient(Http(handler), NullLogger<BusquedaApiClient>.Instance);

        var resultado = await cliente.BuscarAsync("  a&b c ", 7);

        Assert.True(resultado.Succeeded);
        Assert.Equal("/api/busqueda", handler.Uri!.AbsolutePath);
        Assert.Equal("?q=a%26b%20c&limite=7", handler.Uri.Query);
        Assert.Equal("Comercial A&B", resultado.Valor!.Grupos[0].Items[0].Titulo);
    }

    [Fact]
    public async Task Buscar_400_DevuelveLosMensajesReales()
    {
        const string cuerpo = """{"title":"x","status":400,"errors":{"q":["El texto a buscar debe tener entre 2 y 100 caracteres."]}}""";
        var cliente = new BusquedaApiClient(Http(new Grabador(HttpStatusCode.BadRequest, cuerpo)), NullLogger<BusquedaApiClient>.Instance);

        var resultado = await cliente.BuscarAsync("ab");

        Assert.False(resultado.Succeeded);
        Assert.Contains("El texto a buscar debe tener entre 2 y 100 caracteres.", resultado.Errors!);
    }

    [Fact]
    public async Task Buscar_403_MensajeDePermisos()
    {
        var cliente = new BusquedaApiClient(Http(new Grabador(HttpStatusCode.Forbidden, "")), NullLogger<BusquedaApiClient>.Instance);

        var resultado = await cliente.BuscarAsync("ab");

        Assert.Equal("No tiene permisos para buscar registros.", resultado.Message);
    }

    private static HttpClient Http(HttpMessageHandler handler) => new(handler) { BaseAddress = new Uri("http://api.test/") };

    private sealed class Grabador(HttpStatusCode estado, string cuerpo) : HttpMessageHandler
    {
        public Uri? Uri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(estado) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") });
        }
    }
}
```

- [ ] **Step 2: Tests de la página y la barra (fallan)**

`tests/OpenSource1.SmokeTests/Blazor/BuscarPaginaTests.cs`:

```csharp
extern alias BlazorApp;

using System.Net;
using BlazorApp::OpenSource1.Blazor.Components.Pages;
using BlazorApp::OpenSource1.Blazor.Services;
using Moq;
using OpenSource1.Application.Features.Busqueda.Dtos;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>Fix-Features A3: página /buscar (módulos del registro + un bloque por tipo) y barra superior de búsqueda.</summary>
public sealed class BuscarPaginaTests
{
    [Fact]
    public async Task Buscar_MuestraRegistrosConEnlace_YVerTodos()
    {
        using var app = new BlazorSsrFactory();
        var productoId = Guid.NewGuid();
        app.Simular<IBusquedaApiClient>()
            .Setup(c => c.BuscarAsync("tornillo", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Respuesta(new ResultadoBusqueda(TiposResultadoBusqueda.Productos, productoId.ToString(), "Tornillo 3/8", "TOR-38", $"/productos/{productoId}")));

        var html = await HtmlAsync(app.Cliente(), "/buscar?q=tornillo");

        Assert.Contains($"href=\"/productos/{productoId}\"", html);
        Assert.Contains("Tornillo 3/8", html);
        Assert.Contains("href=\"/productos?nombre=tornillo\"", html);
        Assert.Contains("Ningún módulo coincide", html);
        Assert.DoesNotContain("data-testid=\"resultados-clientes\"", html);
    }

    [Fact]
    public async Task Buscar_ModulosSinAcentos_SegunPermisos()
    {
        using var app = new BlazorSsrFactory();
        app.Simular<IBusquedaApiClient>()
            .Setup(c => c.BuscarAsync(It.IsAny<string>(), 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Respuesta());

        var admin = await HtmlAsync(app.Cliente("Administrador"), "/buscar?q=categoria");
        var ejecutor = await HtmlAsync(app.Cliente("Ejecutor"), "/buscar?q=usuarios");
        var adminUsuarios = await HtmlAsync(app.Cliente("Administrador"), "/buscar?q=usuarios");

        Assert.Contains("href=\"/categorias-producto\"", admin);
        // Solo el bloque de resultados (el menú lateral ya se probó en A2).
        Assert.DoesNotContain("href=\"/admin/users\"", ejecutor[ejecutor.IndexOf("data-testid=\"resultados-modulos\"", StringComparison.Ordinal)..]);
        Assert.Contains("data-testid=\"resultados-modulos\"", adminUsuarios);
    }

    [Fact]
    public async Task Buscar_ConApiCaida_MuestraModulosYAviso()
    {
        using var app = new BlazorSsrFactory();
        app.Simular<IBusquedaApiClient>()
            .Setup(c => c.BuscarAsync(It.IsAny<string>(), 5, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API caída"));

        var html = await HtmlAsync(app.Cliente(), "/buscar?q=clientes");

        Assert.Contains("No fue posible buscar registros en este momento", html);
        Assert.Contains("data-testid=\"resultados-modulos\"", html);
        Assert.Contains("href=\"/clientes\"", html);
    }

    [Fact]
    public async Task Buscar_ConsultaCorta_NoLlamaALaApi()
    {
        using var app = new BlazorSsrFactory();
        var api = app.Simular<IBusquedaApiClient>();

        var html = await HtmlAsync(app.Cliente(), "/buscar?q=%20a%20");

        Assert.Contains("Escriba al menos 2 caracteres", html);
        api.Verify(c => c.BuscarAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BarraSuperior_EnPaginasAutenticadas_YConservaLaConsulta()
    {
        using var app = new BlazorSsrFactory();
        app.Simular<IBusquedaApiClient>()
            .Setup(c => c.BuscarAsync(It.IsAny<string>(), 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Respuesta());

        var reporteria = await HtmlAsync(app.Cliente(), "/reporteria");
        var buscar = await HtmlAsync(app.Cliente(), "/buscar?q=abc");
        var login = await app.Cliente(anonimo: true).GetStringAsync("/account/login");

        Assert.Contains("data-testid=\"busqueda-global-form\"", reporteria);
        Assert.Contains("action=\"/buscar\"", reporteria);
        Assert.Contains("id=\"busqueda-global\" name=\"q\"", reporteria);
        Assert.Contains("value=\"abc\"", buscar);
        Assert.DoesNotContain("data-testid=\"busqueda-global-form\"", login);
    }

    [Theory]
    [InlineData(TiposResultadoBusqueda.Clientes, "ana", "/clientes?nombre=ana")]
    [InlineData(TiposResultadoBusqueda.Productos, "tor", "/productos?nombre=tor")]
    [InlineData(TiposResultadoBusqueda.Facturas, "FV01", "/facturas-venta?numero=FV01")]
    [InlineData(TiposResultadoBusqueda.Facturas, "comercial", "/facturas-venta?nombre=comercial")]
    [InlineData(TiposResultadoBusqueda.BorradoresFactura, "B 1", "/facturas-venta/borradores?numero=B%201")]
    [InlineData(TiposResultadoBusqueda.NotasCredito, "NC", "/notas-credito-venta?numero=NC")]
    [InlineData(TiposResultadoBusqueda.BorradoresNotaCredito, "NC", "/notas-credito-venta/borradores?numero=NC")]
    public void VerTodosUrl_ApuntaAlListadoFiltrado(string tipo, string q, string esperado)
    {
        Assert.Equal(esperado, Buscar.VerTodosUrl(tipo, q));
    }

    private static ConsultaResultado<BusquedaGlobalResponse> Respuesta(params ResultadoBusqueda[] items) =>
        new(true, string.Empty, new BusquedaGlobalResponse(
            items.GroupBy(i => i.Tipo).Select(g => new GrupoResultadosBusqueda(g.Key, g.Key, g.ToList())).ToList()));

    private static async Task<string> HtmlAsync(HttpClient cliente, string url)
    {
        var respuesta = await cliente.GetAsync(url);
        var html = await respuesta.Content.ReadAsStringAsync();
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, $"GET {url}: {respuesta.StatusCode}\n{html}");
        return html;
    }
}
```

`ConsultaResultado<T>` es el record existente de `src/OpenSource1.Blazor/Services/IInventarioConsultasApiClient.cs`.

- [ ] **Step 3: Ejecutar y ver que fallan**

Run: `dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~BusquedaApiClientTests|FullyQualifiedName~BuscarPaginaTests"`
Expected: FAIL de compilación (`IBusquedaApiClient`, `Buscar` no existen).

- [ ] **Step 4: Implementar el cliente tipado y registrarlo**

`src/OpenSource1.Blazor/Services/IBusquedaApiClient.cs`:

```csharp
using OpenSource1.Application.Features.Busqueda.Dtos;

namespace OpenSource1.Blazor.Services;

/// <summary>
/// Cliente tipado de <c>GET api/busqueda</c> (Fix-Features A3). Un 400 trae los mensajes reales de la API en
/// <see cref="ConsultaResultado{T}.Errors"/>; un fallo de red se propaga como excepción (la página lo captura).
/// </summary>
public interface IBusquedaApiClient
{
    Task<ConsultaResultado<BusquedaGlobalResponse>> BuscarAsync(string q, int limite = 5, CancellationToken cancellationToken = default);
}
```

`src/OpenSource1.Blazor/Services/BusquedaApiClient.cs`:

```csharp
using System.Globalization;
using OpenSource1.Application.Features.Busqueda.Dtos;

namespace OpenSource1.Blazor.Services;

public sealed class BusquedaApiClient(HttpClient httpClient, ILogger<BusquedaApiClient> logger) : IBusquedaApiClient
{
    public Task<ConsultaResultado<BusquedaGlobalResponse>> BuscarAsync(string q, int limite = 5, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(q);
        var url = $"api/busqueda?q={Uri.EscapeDataString(q.Trim())}&limite={limite.ToString(CultureInfo.InvariantCulture)}";
        return ConsultasApi.GetAsync<BusquedaGlobalResponse>(
            httpClient, url, "los resultados de la búsqueda", "No tiene permisos para buscar registros.", logger,
            cancellationToken: cancellationToken);
    }
}
```

En `Program.cs`, después del registro de `IFechasRegistroApiClient`:

```csharp
builder.Services.AddHttpClient<IBusquedaApiClient, BusquedaApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ApiClientOptions>>().Value;
    client.BaseAddress = options.BaseAddress;
}).AddHttpMessageHandler<BearerTokenHandler>();
```

- [ ] **Step 5: Implementar `BarraBusqueda.razor` y la barra superior**

`src/OpenSource1.Blazor/Components/Layout/BarraBusqueda.razor`:

```razor
@* BarraBusqueda — caja de búsqueda global (Fix-Features A3). Sin JS es un formulario GET a /buscar; la paleta Ctrl+K (A4)
   se monta sobre este mismo input. *@
@using Microsoft.AspNetCore.WebUtilities
@inject NavigationManager Nav

<form method="get" action="/buscar" role="search" class="relative w-full max-w-xl" data-testid="busqueda-global-form">
    <label for="busqueda-global" class="sr-only">Buscar módulos, clientes, productos y documentos</label>
    <span class="pointer-events-none absolute inset-y-0 left-3 flex items-center text-slate-400" aria-hidden="true">
        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.8" stroke="currentColor" class="size-4">
            <path stroke-linecap="round" stroke-linejoin="round" d="@IconosModulo.Buscar" />
        </svg>
    </span>
    <input id="busqueda-global" name="q" type="search" autocomplete="off" maxlength="100" value="@consultaActual"
           placeholder="Buscar módulos, clientes, productos, documentos… (Ctrl+K)"
           class="block w-full rounded-lg border border-slate-300 bg-slate-50 py-2 pl-9 pr-3 text-sm text-slate-800 shadow-sm placeholder:text-slate-400 focus:border-brand-600 focus:bg-white focus:outline-none focus:ring-2 focus:ring-brand-600/20 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-100" />
</form>

@code {
    private string? consultaActual;

    protected override void OnParametersSet()
    {
        var uri = new Uri(Nav.Uri);
        consultaActual = uri.AbsolutePath.Equals("/buscar", StringComparison.OrdinalIgnoreCase)
                         && QueryHelpers.ParseQuery(uri.Query).TryGetValue("q", out var q)
            ? q.ToString()
            : null;
    }
}
```

En `MainLayout.razor`, inmediatamente después de `<div class="flex min-h-screen flex-col @(SidebarCollapsed ? "" : "lg:pl-64")">` (línea 384) y antes de `<main …>`:

```razor
    <AuthorizeView>
        <Authorized>
            <div class="border-b border-slate-200 bg-white/95 px-4 py-2.5 backdrop-blur sm:px-6 lg:sticky lg:top-0 lg:z-20 lg:px-8 dark:border-slate-700 dark:bg-slate-900/95" data-testid="barra-superior">
                <div class="mx-auto flex w-full max-w-7xl items-center gap-3">
                    <BarraBusqueda />
                </div>
            </div>
        </Authorized>
    </AuthorizeView>
```

- [ ] **Step 6: Implementar la página `/buscar`**

`src/OpenSource1.Blazor/Components/Pages/Buscar.razor`:

```razor
@page "/buscar"
@attribute [Authorize]
@using Microsoft.AspNetCore.Authorization
@using OpenSource1.Application.Features.Busqueda.Dtos
@using OpenSource1.Application.Security
@using OpenSource1.Blazor.Services
@inject IRegistroModulos Registro
@inject IBusquedaApiClient BusquedaApiClient
@inject IAuthorizationService AuthorizationService
@inject ILogger<Buscar> Logger

<PageTitle>Buscar – AxionERP</PageTitle>

<div class="mb-6">
    <p class="text-xs font-bold uppercase tracking-widest text-brand-600 dark:text-brand-400">Búsqueda</p>
    <h1 class="text-2xl font-extrabold tracking-tight text-slate-900 dark:text-slate-100">@(consulta is null ? "Buscar" : $"Resultados para «{consulta}»")</h1>
</div>

@if (consulta is null)
{
    <MessageBox Type="info" Message="Escriba al menos 2 caracteres para buscar módulos, clientes, productos o documentos." />
}
else
{
    <section class="mb-6 rounded-2xl border border-slate-200 bg-white shadow-sm dark:border-slate-700 dark:bg-slate-800" data-testid="resultados-modulos">
        <h2 class="border-b border-slate-100 px-5 py-3 text-xs font-bold uppercase tracking-wide text-slate-500 dark:border-slate-700 dark:text-slate-400">Módulos</h2>
        @if (modulos.Count == 0)
        {
            <p class="px-5 py-4 text-sm text-slate-500 dark:text-slate-400">Ningún módulo coincide con la búsqueda.</p>
        }
        else
        {
            <ul class="divide-y divide-slate-100 dark:divide-slate-700/60">
                @foreach (var modulo in modulos)
                {
                    <li><a href="@modulo.Ruta" class="flex flex-col px-5 py-3 hover:bg-slate-50 dark:hover:bg-slate-700/40"><span class="text-sm font-semibold text-slate-800 dark:text-slate-100">@modulo.Titulo</span><span class="text-xs text-slate-500 dark:text-slate-400">@modulo.Descripcion</span></a></li>
                }
            </ul>
        }
    </section>

    @if (errorRegistros is not null)
    {
        <MessageBox Type="warning" Message="@errorRegistros" Errors="@erroresRegistros" />
    }
    else if (canConsult)
    {
        var conResultados = grupos.Where(g => g.Items.Count > 0).ToList();
        @if (conResultados.Count == 0)
        {
            <p class="text-sm text-slate-500 dark:text-slate-400" data-testid="sin-registros">No se encontraron clientes, productos ni documentos.</p>
        }
        @foreach (var grupo in conResultados)
        {
            <section class="mb-6 rounded-2xl border border-slate-200 bg-white shadow-sm dark:border-slate-700 dark:bg-slate-800" data-testid="@($"resultados-{grupo.Tipo}")">
                <div class="flex items-center justify-between border-b border-slate-100 px-5 py-3 dark:border-slate-700">
                    <h2 class="text-xs font-bold uppercase tracking-wide text-slate-500 dark:text-slate-400">@grupo.Titulo</h2>
                    <a href="@VerTodosUrl(grupo.Tipo, consulta)" class="text-xs font-semibold text-brand-600 hover:text-brand-700 dark:text-brand-400">Ver todos</a>
                </div>
                <ul class="divide-y divide-slate-100 dark:divide-slate-700/60">
                    @foreach (var item in grupo.Items)
                    {
                        <li><a href="@item.Ruta" class="flex flex-col px-5 py-3 hover:bg-slate-50 dark:hover:bg-slate-700/40"><span class="text-sm font-semibold text-slate-800 dark:text-slate-100">@item.Titulo</span>@if (!string.IsNullOrWhiteSpace(item.Subtitulo)){<span class="text-xs text-slate-500 dark:text-slate-400">@item.Subtitulo</span>}</a></li>
                    }
                </ul>
            </section>
        }
    }
}

@code {
    [SupplyParameterFromQuery(Name = "q")] private string? Q { get; set; }
    [CascadingParameter] private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    private string? consulta;
    private IReadOnlyList<Modulo> modulos = [];
    private IReadOnlyList<GrupoResultadosBusqueda> grupos = [];
    private bool canConsult;
    private string? errorRegistros;
    private IReadOnlyList<string> erroresRegistros = [];

    protected override async Task OnInitializedAsync()
    {
        var q = Q?.Trim() ?? string.Empty;
        if (q.Length < 2 || AuthenticationStateTask is null)
        {
            return;
        }

        consulta = q.Length > 100 ? q[..100] : q;
        var usuario = (await AuthenticationStateTask).User;
        modulos = TextoBusqueda.FiltrarModulos(await Registro.VisiblesAsync(usuario), consulta);
        canConsult = (await AuthorizationService.AuthorizeAsync(usuario, ApplicationPolicies.CanConsult)).Succeeded;
        if (!canConsult)
        {
            return;
        }

        try
        {
            var resultado = await BusquedaApiClient.BuscarAsync(consulta);
            if (resultado.Succeeded)
            {
                grupos = resultado.Valor!.Grupos;
            }
            else
            {
                errorRegistros = resultado.Message;
                erroresRegistros = resultado.Errors ?? [];
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "No fue posible consultar la búsqueda global.");
            errorRegistros = "No fue posible buscar registros en este momento. Los módulos siguen disponibles.";
        }
    }

    /// <summary>"Ver todos": el listado del tipo filtrado por la consulta (número si la consulta tiene dígitos, si no nombre).</summary>
    public static string VerTodosUrl(string tipo, string q)
    {
        var valor = Uri.EscapeDataString(q);
        var campo = q.Any(char.IsDigit) ? "numero" : "nombre";
        return tipo switch
        {
            TiposResultadoBusqueda.Clientes => $"/clientes?nombre={valor}",
            TiposResultadoBusqueda.Productos => $"/productos?nombre={valor}",
            TiposResultadoBusqueda.Facturas => $"/facturas-venta?{campo}={valor}",
            TiposResultadoBusqueda.BorradoresFactura => $"/facturas-venta/borradores?{campo}={valor}",
            TiposResultadoBusqueda.NotasCredito => $"/notas-credito-venta?numero={valor}",
            TiposResultadoBusqueda.BorradoresNotaCredito => $"/notas-credito-venta/borradores?numero={valor}",
            _ => $"/buscar?q={valor}",
        };
    }
}
```

- [ ] **Step 7: Ejecutar los tests**

Run: `dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~BusquedaApiClientTests|FullyQualifiedName~BuscarPaginaTests|FullyQualifiedName~NavMenuTests"`
Expected: PASS.

- [ ] **Step 8: Build sin avisos**

Run: `dotnet build test.slnx -warnaserror`
Expected: 0 avisos.

- [ ] **Step 9: Commit**

```bash
git add src/OpenSource1.Blazor/Services/IBusquedaApiClient.cs src/OpenSource1.Blazor/Services/BusquedaApiClient.cs src/OpenSource1.Blazor/Program.cs src/OpenSource1.Blazor/Components/Layout/BarraBusqueda.razor src/OpenSource1.Blazor/Components/Layout/MainLayout.razor src/OpenSource1.Blazor/Components/Pages/Buscar.razor tests/OpenSource1.SmokeTests/Blazor/BusquedaApiClientTests.cs tests/OpenSource1.SmokeTests/Blazor/BuscarPaginaTests.cs
git commit -m "feat: barra superior de busqueda y pagina de resultados con modulos y registros"
```

---

### Task A4: Paleta Ctrl+K (mejora progresiva) y `/buscar/sugerencias`

**Grupo de paralelismo:** A-serie (tras A3b).

**Files:**
- Create: `src/OpenSource1.Blazor/wwwroot/app.search.js`
- Modify: `src/OpenSource1.Blazor/Components/App.razor` (script tras `app.interactions.js`)
- Modify: `src/OpenSource1.Blazor/Components/Layout/BarraBusqueda.razor` (atributos ARIA, capa y la isla JSON)
- Modify: `src/OpenSource1.Blazor/Program.cs` (endpoint `GET /buscar/sugerencias` antes de `app.MapStaticAssets();`)
- Test: `tests/OpenSource1.SmokeTests/Blazor/PaletaBusquedaTests.cs`

**Interfaces:**
- Consumes: `IBusquedaApiClient` (A3b), `IRegistroModulos` (A1).
- Produces: `GET /buscar/sugerencias?q=` (host Blazor, `RequireAuthorization(CanConsult)`): 200 `BusquedaGlobalResponse` en camelCase; 400 si `q` recortado fuera de 2–100; 502 si la API falla. Isla `<script type="application/json" id="modulos-data">` con `[{titulo, descripcion, ruta, grupo, palabras}]` de los módulos visibles. IDs DOM: `busqueda-global`, `busqueda-paleta` (`role="dialog"`, `hidden`), `busqueda-paleta-lista` (`role="listbox"`), opciones `busqueda-opcion-{n}`.

- [ ] **Step 1: Tests (fallan)**

`tests/OpenSource1.SmokeTests/Blazor/PaletaBusquedaTests.cs`:

```csharp
extern alias BlazorApp;

using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using BlazorApp::OpenSource1.Blazor.Services;
using Moq;
using OpenSource1.Application.Features.Busqueda.Dtos;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>
/// Fix-Features A4: la paleta es JS progresivo; aquí se prueba lo que la sostiene en el servidor: la isla JSON con SOLO los
/// módulos visibles, el marcado accesible y el endpoint /buscar/sugerencias (el JWT nunca sale del servidor). El
/// comportamiento de teclado se prueba en el E2E (tests/e2e/specs/paleta.spec.ts, Task D3).
/// </summary>
public sealed class PaletaBusquedaTests
{
    [Theory]
    [InlineData("Ejecutor", false)]
    [InlineData("Administrador", true)]
    public async Task Isla_SoloModulosVisibles(string rol, bool veUsuarios)
    {
        using var app = new BlazorSsrFactory();
        var html = await (await app.Cliente(rol).GetAsync("/reporteria")).Content.ReadAsStringAsync();

        var isla = Regex.Match(html, "<script type=\"application/json\" id=\"modulos-data\">(.*?)</script>", RegexOptions.Singleline);
        Assert.True(isla.Success, "Falta la isla #modulos-data.");
        Assert.DoesNotContain("<", isla.Groups[1].Value);
        var rutas = JsonDocument.Parse(isla.Groups[1].Value).RootElement.EnumerateArray().Select(m => m.GetProperty("ruta").GetString()).ToList();

        Assert.Contains("/clientes", rutas);
        Assert.Equal(veUsuarios, rutas.Contains("/admin/users"));
        Assert.Equal(veUsuarios, rutas.Contains("/admin/fechas-registro"));
    }

    [Fact]
    public async Task Marcado_Accesible_YScriptDeLaPaleta()
    {
        using var app = new BlazorSsrFactory();
        var html = await (await app.Cliente().GetAsync("/reporteria")).Content.ReadAsStringAsync();

        Assert.Contains("role=\"combobox\"", html);
        Assert.Contains("aria-controls=\"busqueda-paleta-lista\"", html);
        Assert.Matches(new Regex("<div id=\"busqueda-paleta\" role=\"dialog\"[^>]*hidden"), html);
        Assert.Contains("role=\"listbox\"", html);
        Assert.Matches(new Regex("<script src=\"[^\"]*app\\.search[^\"]*\\.js\"></script>"), html);
    }

    [Fact]
    public async Task Sugerencias_DevuelveElJsonDeLaApi()
    {
        using var app = new BlazorSsrFactory();
        app.Simular<IBusquedaApiClient>()
            .Setup(c => c.BuscarAsync("torn", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConsultaResultado<BusquedaGlobalResponse>(true, string.Empty, new BusquedaGlobalResponse(
                [new GrupoResultadosBusqueda("productos", "Productos", [new ResultadoBusqueda("productos", "1", "Tornillo", "T1", "/productos/1")])])));

        var respuesta = await app.Cliente().GetAsync("/buscar/sugerencias?q=%20torn%20");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var raiz = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("/productos/1", raiz.GetProperty("grupos")[0].GetProperty("items")[0].GetProperty("ruta").GetString());
    }

    [Fact]
    public async Task Sugerencias_QCorta_400_Anonimo_Login_SinCanConsult_Prohibido()
    {
        using var app = new BlazorSsrFactory();
        app.Simular<IBusquedaApiClient>();

        Assert.Equal(HttpStatusCode.BadRequest, (await app.Cliente().GetAsync("/buscar/sugerencias?q=a")).StatusCode);

        var anonimo = await app.Cliente(anonimo: true).GetAsync("/buscar/sugerencias?q=abc");
        Assert.Equal(HttpStatusCode.Redirect, anonimo.StatusCode);
        Assert.StartsWith("/account/login", FormulariosSsr.Destino(anonimo));

        var sinPermiso = await app.Cliente("Supervisor", permisos: "").GetAsync("/buscar/sugerencias?q=abc");
        Assert.Equal(HttpStatusCode.Redirect, sinPermiso.StatusCode);
        Assert.StartsWith("/access-denied", FormulariosSsr.Destino(sinPermiso));
    }

    [Fact]
    public async Task Sugerencias_ApiCaida_502()
    {
        using var app = new BlazorSsrFactory();
        var api = app.Simular<IBusquedaApiClient>();
        api.Setup(c => c.BuscarAsync("caida", 5, It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("sin API"));
        api.Setup(c => c.BuscarAsync("fallo", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConsultaResultado<BusquedaGlobalResponse>(false, "No fue posible cargar los resultados de la búsqueda."));

        Assert.Equal(HttpStatusCode.BadGateway, (await app.Cliente().GetAsync("/buscar/sugerencias?q=caida")).StatusCode);
        Assert.Equal(HttpStatusCode.BadGateway, (await app.Cliente().GetAsync("/buscar/sugerencias?q=fallo")).StatusCode);
    }
}
```

- [ ] **Step 2: Ejecutar y ver que fallan**

Run: `dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~PaletaBusquedaTests"`
Expected: FAIL (no hay isla, ni endpoint).

- [ ] **Step 3: Endpoint `/buscar/sugerencias`**

En `Program.cs`, antes de `app.MapStaticAssets();`:

```csharp
// Paleta Ctrl+K (Fix-Features A4): JSON mínimo del host. El navegador nunca habla con la API: el host la llama con la sesión
// (BearerTokenHandler) y devuelve solo los resultados. 400 = consulta fuera de 2–100; 502 = la API no respondió.
app.MapGet("/buscar/sugerencias", async (string? q, IBusquedaApiClient busqueda, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    var termino = q?.Trim() ?? string.Empty;
    if (termino.Length is < 2 or > 100)
    {
        return Results.BadRequest();
    }

    try
    {
        var resultado = await busqueda.BuscarAsync(termino, 5, cancellationToken);
        return resultado.Succeeded ? Results.Json(resultado.Valor) : Results.StatusCode(StatusCodes.Status502BadGateway);
    }
    catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
    {
        logger.LogWarning(ex, "No fue posible obtener sugerencias de búsqueda.");
        return Results.StatusCode(StatusCodes.Status502BadGateway);
    }
}).RequireAuthorization(ApplicationPolicies.CanConsult);
```

- [ ] **Step 4: Capa, ARIA e isla en `BarraBusqueda.razor`**

Reemplazar el contenido completo de `BarraBusqueda.razor` por:

```razor
@* BarraBusqueda — caja de búsqueda global (Fix-Features A3/A4). Sin JS es un formulario GET a /buscar. Con JS
   (app.search.js), Ctrl/Cmd+K o "/" abren la capa: módulos filtrados en el navegador desde la isla #modulos-data (solo
   los visibles para este usuario) y registros desde /buscar/sugerencias. *@
@using System.Text.Json
@using Microsoft.AspNetCore.WebUtilities
@inject NavigationManager Nav
@inject IRegistroModulos Registro

<form method="get" action="/buscar" role="search" class="relative w-full max-w-xl" data-testid="busqueda-global-form">
    <label for="busqueda-global" class="sr-only">Buscar módulos, clientes, productos y documentos</label>
    <span class="pointer-events-none absolute inset-y-0 left-3 flex items-center text-slate-400" aria-hidden="true">
        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.8" stroke="currentColor" class="size-4">
            <path stroke-linecap="round" stroke-linejoin="round" d="@IconosModulo.Buscar" />
        </svg>
    </span>
    <input id="busqueda-global" name="q" type="search" autocomplete="off" maxlength="100" value="@consultaActual"
           role="combobox" aria-expanded="false" aria-controls="busqueda-paleta-lista" aria-autocomplete="list" aria-haspopup="listbox"
           placeholder="Buscar módulos, clientes, productos, documentos… (Ctrl+K)"
           class="block w-full rounded-lg border border-slate-300 bg-slate-50 py-2 pl-9 pr-3 text-sm text-slate-800 shadow-sm placeholder:text-slate-400 focus:border-brand-600 focus:bg-white focus:outline-none focus:ring-2 focus:ring-brand-600/20 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-100" />
    <div id="busqueda-paleta" role="dialog" aria-label="Resultados de la búsqueda" hidden
         class="absolute left-0 right-0 top-full z-40 mt-2 overflow-hidden rounded-xl border border-slate-200 bg-white shadow-xl dark:border-slate-700 dark:bg-slate-800">
        <ul id="busqueda-paleta-lista" role="listbox" aria-label="Sugerencias" class="max-h-96 overflow-y-auto py-1"></ul>
        <p class="border-t border-slate-100 px-3 py-2 text-[11px] text-slate-400 dark:border-slate-700">↑↓ para moverse · Enter para abrir · Enter sin selección: ver todos · Esc para cerrar</p>
    </div>
</form>
<script type="application/json" id="modulos-data">@((MarkupString)islaJson)</script>

@code {
    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    [CascadingParameter] private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    private string? consultaActual;
    private string islaJson = "[]";

    protected override async Task OnParametersSetAsync()
    {
        var uri = new Uri(Nav.Uri);
        consultaActual = uri.AbsolutePath.Equals("/buscar", StringComparison.OrdinalIgnoreCase)
                         && QueryHelpers.ParseQuery(uri.Query).TryGetValue("q", out var q)
            ? q.ToString()
            : null;

        if (AuthenticationStateTask is null)
        {
            return;
        }

        var visibles = await Registro.VisiblesAsync((await AuthenticationStateTask).User);
        var titulos = Registro.Grupos.ToDictionary(g => g.Clave, g => g.Titulo, StringComparer.Ordinal);
        // El codificador por defecto de System.Text.Json escapa <, >, & y ' (<…): la isla no puede cerrar el <script>.
        islaJson = JsonSerializer.Serialize(
            visibles.Select(m => new { m.Titulo, m.Descripcion, m.Ruta, Grupo = titulos[m.Grupo], Palabras = string.Join(' ', m.PalabrasClave) }),
            OpcionesJson);
    }
}
```

En `App.razor`, después de `<script src="@Assets["app.interactions.js"]"></script>`:

```razor
    <script src="@Assets["app.search.js"]"></script>
```

- [ ] **Step 5: Escribir `app.search.js`**

`src/OpenSource1.Blazor/wwwroot/app.search.js`:

```javascript
// app.search.js — Paleta de búsqueda Ctrl/Cmd+K o "/" (Fix-Features A4). Mejora progresiva: sin este script la caja sigue
// siendo un <form method="get" action="/buscar">. Módulos: isla JSON #modulos-data (solo los visibles para el usuario,
// renderizada en servidor). Registros: GET /buscar/sugerencias?q= (el host llama a la API con la sesión; el JWT no sale).
(() => {
  'use strict';

  const DEBOUNCE_MS = 250;
  const MIN_CARACTERES = 2;
  const MAX_MODULOS = 8;
  const ERROR_REGISTROS = 'No fue posible buscar registros en este momento.';

  let modulos = [];
  let opciones = [];
  let activo = -1;
  let temporizador = 0;
  let controlador = null;
  let ultimaConsulta = '';

  const porId = (id) => document.getElementById(id);
  const normalizar = (texto) => (texto || '').normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().trim();

  function elementos() {
    const input = porId('busqueda-global');
    const panel = porId('busqueda-paleta');
    const lista = porId('busqueda-paleta-lista');
    return input && panel && lista ? { input, panel, lista } : null;
  }

  function leerModulos() {
    const isla = porId('modulos-data');
    if (!isla) return [];
    try {
      const datos = JSON.parse(isla.textContent || '[]');
      return Array.isArray(datos) ? datos : [];
    } catch {
      return [];
    }
  }

  function filtrarModulos(consulta) {
    const q = normalizar(consulta);
    if (!q) return modulos.slice(0, MAX_MODULOS);
    return modulos
      .filter((m) => normalizar(`${m.titulo} ${m.grupo} ${m.palabras}`).includes(q))
      .slice(0, MAX_MODULOS);
  }

  function estaAbierta() {
    const el = elementos();
    return !!el && !el.panel.hidden;
  }

  function abrir() {
    const el = elementos();
    if (!el) return;
    el.panel.hidden = false;
    el.input.setAttribute('aria-expanded', 'true');
    el.input.focus();
    actualizar(el.input.value);
  }

  function cerrar() {
    const el = elementos();
    if (!el) return;
    el.panel.hidden = true;
    el.input.setAttribute('aria-expanded', 'false');
    el.input.removeAttribute('aria-activedescendant');
    activo = -1;
    window.clearTimeout(temporizador);
    if (controlador) controlador.abort();
  }

  function encabezado(lista, texto) {
    const li = document.createElement('li');
    li.setAttribute('role', 'presentation');
    li.className = 'px-3 pt-3 pb-1 text-[10px] font-bold uppercase tracking-widest text-slate-400';
    li.textContent = texto;
    lista.appendChild(li);
  }

  function aviso(lista, texto) {
    const li = document.createElement('li');
    li.setAttribute('role', 'presentation');
    li.className = 'px-3 py-2 text-xs text-slate-500';
    li.textContent = texto;
    lista.appendChild(li);
  }

  function opcion(lista, datos) {
    const indice = opciones.length;
    opciones.push(datos);
    const li = document.createElement('li');
    li.id = `busqueda-opcion-${indice}`;
    li.setAttribute('role', 'option');
    li.setAttribute('aria-selected', 'false');
    li.className = 'mx-1 rounded-lg';
    const a = document.createElement('a');
    a.href = datos.href;
    a.tabIndex = -1;
    a.className = 'flex flex-col rounded-lg px-3 py-2 text-sm text-slate-700 hover:bg-slate-100 dark:text-slate-200 dark:hover:bg-slate-700/60';
    const titulo = document.createElement('span');
    titulo.className = 'font-semibold';
    titulo.textContent = datos.texto;
    a.appendChild(titulo);
    if (datos.detalle) {
      const detalle = document.createElement('span');
      detalle.className = 'text-xs text-slate-400';
      detalle.textContent = datos.detalle;
      a.appendChild(detalle);
    }
    li.appendChild(a);
    lista.appendChild(li);
  }

  function pintar(consulta, grupos, error, cargando) {
    const el = elementos();
    if (!el) return;
    el.lista.replaceChildren();
    opciones = [];
    activo = -1;
    el.input.removeAttribute('aria-activedescendant');

    const encontrados = filtrarModulos(consulta);
    encabezado(el.lista, 'Módulos');
    if (encontrados.length === 0) aviso(el.lista, 'Ningún módulo coincide.');
    encontrados.forEach((m) => opcion(el.lista, { href: m.ruta, texto: m.titulo, detalle: m.grupo }));

    if (consulta.trim().length < MIN_CARACTERES) return;
    if (cargando) { encabezado(el.lista, 'Registros'); aviso(el.lista, 'Buscando…'); return; }
    if (error) { encabezado(el.lista, 'Registros'); aviso(el.lista, error); return; }
    (grupos || []).filter((g) => Array.isArray(g.items) && g.items.length > 0).forEach((g) => {
      encabezado(el.lista, g.titulo);
      g.items.forEach((r) => opcion(el.lista, { href: r.ruta, texto: r.titulo, detalle: r.subtitulo }));
    });
  }

  function marcar(indice) {
    const el = elementos();
    if (!el || opciones.length === 0) return;
    activo = (indice + opciones.length) % opciones.length;
    el.lista.querySelectorAll('[role="option"]').forEach((li, i) => {
      const seleccionada = i === activo;
      li.setAttribute('aria-selected', seleccionada ? 'true' : 'false');
      li.classList.toggle('bg-brand-50', seleccionada);
      if (seleccionada) li.scrollIntoView({ block: 'nearest' });
    });
    el.input.setAttribute('aria-activedescendant', `busqueda-opcion-${activo}`);
  }

  function actualizar(consulta) {
    ultimaConsulta = consulta;
    window.clearTimeout(temporizador);
    if (controlador) controlador.abort();
    if (consulta.trim().length < MIN_CARACTERES) { pintar(consulta, [], null, false); return; }
    pintar(consulta, [], null, true);
    temporizador = window.setTimeout(() => buscarRegistros(consulta), DEBOUNCE_MS);
  }

  async function buscarRegistros(consulta) {
    controlador = new AbortController();
    try {
      const respuesta = await fetch(`/buscar/sugerencias?q=${encodeURIComponent(consulta.trim())}`, {
        headers: { Accept: 'application/json' },
        credentials: 'same-origin',
        signal: controlador.signal,
      });
      if (consulta !== ultimaConsulta) return;
      if (!respuesta.ok) { pintar(consulta, [], ERROR_REGISTROS, false); return; }
      const datos = await respuesta.json();
      pintar(consulta, datos.grupos || [], null, false);
    } catch (error) {
      if (error && error.name === 'AbortError') return;
      if (consulta === ultimaConsulta) pintar(consulta, [], ERROR_REGISTROS, false);
    }
  }

  function esCampoEditable(objetivo) {
    if (!(objetivo instanceof HTMLElement)) return false;
    return objetivo.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(objetivo.tagName);
  }

  function inicializar() {
    modulos = leerModulos();
    const el = elementos();
    if (!el || el.input.dataset.paletaLista === 'true') return;
    el.input.dataset.paletaLista = 'true';

    el.input.addEventListener('focus', () => { if (el.panel.hidden) abrir(); });
    el.input.addEventListener('input', () => { if (el.panel.hidden) abrir(); else actualizar(el.input.value); });
    el.input.addEventListener('keydown', (evento) => {
      if (evento.key === 'ArrowDown') { evento.preventDefault(); if (!estaAbierta()) abrir(); marcar(activo + 1); }
      else if (evento.key === 'ArrowUp') { evento.preventDefault(); marcar(activo - 1); }
      else if (evento.key === 'Escape') { evento.preventDefault(); cerrar(); }
      else if (evento.key === 'Enter' && estaAbierta() && activo >= 0 && opciones[activo]) {
        evento.preventDefault();
        const enlace = el.lista.querySelector(`#busqueda-opcion-${activo} a`);
        cerrar();
        if (enlace) enlace.click();
      }
      // Enter sin selección: el <form method="get" action="/buscar"> se envía con normalidad.
    });
    el.lista.addEventListener('mousedown', (evento) => evento.preventDefault());
    el.lista.addEventListener('click', () => cerrar());
  }

  document.addEventListener('keydown', (evento) => {
    const tecla = (evento.key || '').toLowerCase();
    if ((evento.ctrlKey || evento.metaKey) && tecla === 'k') { evento.preventDefault(); abrir(); }
    else if (tecla === '/' && !evento.ctrlKey && !evento.metaKey && !evento.altKey && !esCampoEditable(evento.target)) {
      evento.preventDefault();
      abrir();
    }
  });

  document.addEventListener('click', (evento) => {
    const el = elementos();
    if (el && estaAbierta() && !el.panel.contains(evento.target) && evento.target !== el.input) cerrar();
  });

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', inicializar);
  else inicializar();

  // Navegación mejorada de Blazor: el DOM se parchea sin recargar; se relee la isla y se cablea un input nuevo si lo hay.
  if (window.Blazor && typeof window.Blazor.addEventListener === 'function') {
    window.Blazor.addEventListener('enhancedload', () => { cerrar(); inicializar(); });
  }
})();
```

- [ ] **Step 6: Comprobar sintaxis del JS y ejecutar los tests**

Run: `node --check src/OpenSource1.Blazor/wwwroot/app.search.js && dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~PaletaBusquedaTests|FullyQualifiedName~BuscarPaginaTests"`
Expected: sin salida de `node --check`; tests PASS.

- [ ] **Step 7: Build sin avisos**

Run: `dotnet build test.slnx -warnaserror`
Expected: 0 avisos.

- [ ] **Step 8: Commit**

```bash
git add src/OpenSource1.Blazor/wwwroot/app.search.js src/OpenSource1.Blazor/Components/App.razor src/OpenSource1.Blazor/Components/Layout/BarraBusqueda.razor src/OpenSource1.Blazor/Program.cs tests/OpenSource1.SmokeTests/Blazor/PaletaBusquedaTests.cs
git commit -m "feat: paleta Ctrl+K progresiva con modulos visibles y sugerencias de registros desde el servidor"
```

---

### Task A5: Inicio compacto y páginas de grupo `/modulos/{grupo}`

**Grupo de paralelismo:** A-serie (tras A4). A6 puede correr a la vez.

**Files:**
- Create: `src/OpenSource1.Blazor/Navigation/IIndicadoresModulos.cs`
- Create: `src/OpenSource1.Blazor/Navigation/IndicadoresModulos.cs`
- Modify: `src/OpenSource1.Blazor/Program.cs` (registro `AddScoped<IIndicadoresModulos, IndicadoresModulos>()` junto al de `IRegistroModulos`)
- Modify (reemplazo completo): `src/OpenSource1.Blazor/Components/Pages/Home.razor`
- Create: `src/OpenSource1.Blazor/Components/Pages/ModuloGrupo.razor`
- Test: `tests/OpenSource1.SmokeTests/Blazor/InicioYGruposTests.cs`

**Interfaces:**
- Consumes: `IRegistroModulos` (A1); clientes tipados existentes `ISocioNegocioApiClient`, `IProductoApiClient`, `ICategoriaProductoApiClient`, `IAlmacenApiClient`, `IFacturaVentaApiClient`, `INotaCreditoVentaApiClient`, `ICuentaContableApiClient`, `ITerminoPagoApiClient`, `IUnidadMedidaApiClient`.
- Produces: `record Indicador(string Titulo, string Valor, string? Ruta)`; `interface IIndicadoresModulos { Task<IReadOnlyList<Indicador>> ObtenerAsync(string grupo, ClaimsPrincipal usuario, CancellationToken cancellationToken = default); }` (nunca lanza; máx. 3). Página `/modulos/{Grupo}` (grupo inexistente → 404 con `NavigationManager.NotFound()`).

- [ ] **Step 1: Tests (fallan)**

`tests/OpenSource1.SmokeTests/Blazor/InicioYGruposTests.cs`:

```csharp
extern alias BlazorApp;

using System.Net;
using BlazorApp::OpenSource1.Blazor.Services;
using Moq;
using OpenSource1.Application.Features.FacturasVenta.Borradores.Dtos;
using OpenSource1.Application.Features.FacturasVenta.Posteadas;
using OpenSource1.Application.Features.NotasCreditoVenta.Borradores.Dtos;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>Fix-Features A5/A6: inicio compacto sin hero azul y páginas de grupo con indicadores tolerantes a fallos.</summary>
public sealed class InicioYGruposTests
{
    [Fact]
    public async Task Inicio_SinHeroNiAnimaciones_ConKpisYTarjetasDeGrupo()
    {
        using var app = new BlazorSsrFactory();
        app.Simular<ISocioNegocioApiClient>().Setup(c => c.ListAllAsync(It.IsAny<SocioNegocioSearchFilter?>(), It.IsAny<CancellationToken>())).ReturnsAsync([new SocioNegocioResponse { NombreComercial = "A" }]);
        app.Simular<IProductoApiClient>().Setup(c => c.ListAllAsync(It.IsAny<ProductoSearchFilter?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ProductoResponse { Nombre = "P", Existencia = 0 }, new ProductoResponse { Nombre = "Q", Existencia = 3 }]);

        var html = await HtmlAsync(app.Cliente("Administrador"), "/");

        Assert.DoesNotContain("bg-gradient-to-br from-brand-700", html);
        Assert.DoesNotMatch("class=\"[^\"]*animate-(fade-in|slide-up|slide-in-left|pop|pulse-glow|float)", html.Split("<main")[1]);
        Assert.Contains("data-testid=\"kpis-inicio\"", html);
        Assert.Contains("href=\"/modulos/facturacion\"", html);
        Assert.Contains("href=\"/modulos/administracion\"", html);
    }

    [Fact]
    public async Task Inicio_Ejecutor_SoloGruposConModulosVisibles()
    {
        using var app = new BlazorSsrFactory();
        app.Simular<ISocioNegocioApiClient>().Setup(c => c.ListAllAsync(It.IsAny<SocioNegocioSearchFilter?>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        app.Simular<IProductoApiClient>().Setup(c => c.ListAllAsync(It.IsAny<ProductoSearchFilter?>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var html = await HtmlAsync(app.Cliente("Ejecutor"), "/");

        Assert.Contains("href=\"/modulos/clientes\"", html);
        Assert.DoesNotContain("href=\"/modulos/administracion\"", html);
    }

    [Fact]
    public async Task Grupo_MuestraSusModulos_YGrupoInexistente404()
    {
        using var app = new BlazorSsrFactory();

        var html = await HtmlAsync(app.Cliente("Administrador"), "/modulos/configuracion");
        var inexistente = await app.Cliente("Administrador").GetAsync("/modulos/noexiste");

        Assert.Contains("href=\"/terminos-pago\"", html);
        Assert.Contains("href=\"/admin/fechas-registro\"", html);
        Assert.Matches("<details data-grupo=\"configuracion\" open", html);
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
    }

    [Fact]
    public async Task Grupo_IndicadorQueFalla_SeOmite()
    {
        using var app = new BlazorSsrFactory();
        var facturas = app.Simular<IFacturaVentaApiClient>();
        facturas.Setup(c => c.ListBorradoresAsync(It.IsAny<FacturaVentaBorradorFiltro?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<FacturaVentaBorradorResponse>([], 1, 1, 7));
        facturas.Setup(c => c.ListFacturasAsync(It.IsAny<FacturaVentaSearchCriteria?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API caída"));
        app.Simular<INotaCreditoVentaApiClient>()
            .Setup(c => c.ListBorradoresAsync(It.IsAny<NotaCreditoVentaBorradorFiltro?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<NotaCreditoVentaBorradorResponse>([], 1, 1, 2));

        var html = await HtmlAsync(app.Cliente("Administrador"), "/modulos/facturacion");

        Assert.Contains("Borradores abiertos", html);
        Assert.Contains(">7<", html);
        Assert.Contains("Borradores de nota de crédito", html);
        Assert.DoesNotContain("Facturas posteadas", html);
        Assert.Contains("href=\"/facturas-venta\"", html);
    }

    [Fact]
    public async Task Grupo_SinModulosVisibles_MensajeSinEnlaces()
    {
        using var app = new BlazorSsrFactory();

        var html = await HtmlAsync(app.Cliente("Ejecutor"), "/modulos/administracion");

        Assert.Contains("No tiene módulos disponibles en este grupo.", html);
        Assert.DoesNotContain("data-testid=\"tarjeta-modulo\"", html);
    }

    private static async Task<string> HtmlAsync(HttpClient cliente, string url)
    {
        var respuesta = await cliente.GetAsync(url);
        var html = await respuesta.Content.ReadAsStringAsync();
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, $"GET {url}: {respuesta.StatusCode}\n{html}");
        return html;
    }
}
```

Comprobar antes de ejecutar que los `using` de los DTOs coinciden con sus namespaces reales (`NotaCreditoVentaBorradorResponse` y `NotaCreditoVentaBorradorFiltro`: `grep -rn "class NotaCreditoVentaBorradorResponse\|record NotaCreditoVentaBorradorFiltro" src/`); ajustar solo los `using`.

- [ ] **Step 2: Ejecutar y ver que fallan**

Run: `dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~InicioYGruposTests"`
Expected: FAIL (el inicio actual tiene el hero y `/modulos/...` no existe).

- [ ] **Step 3: Servicio de indicadores**

`src/OpenSource1.Blazor/Navigation/IIndicadoresModulos.cs`:

```csharp
using System.Security.Claims;

namespace OpenSource1.Blazor.Navigation;

public sealed record Indicador(string Titulo, string Valor, string? Ruta);

public interface IIndicadoresModulos
{
    /// <summary>Hasta 3 indicadores del grupo desde endpoints existentes; el que falla se omite. Nunca lanza. Sin CanConsult: ninguno.</summary>
    Task<IReadOnlyList<Indicador>> ObtenerAsync(string grupo, ClaimsPrincipal usuario, CancellationToken cancellationToken = default);
}
```

`src/OpenSource1.Blazor/Navigation/IndicadoresModulos.cs`:

```csharp
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using OpenSource1.Application.Security;
using OpenSource1.Blazor.Services;
using OpenSource1.Core.Common;

namespace OpenSource1.Blazor.Navigation;

public sealed class IndicadoresModulos(
    IAuthorizationService autorizacion,
    ISocioNegocioApiClient socios,
    IProductoApiClient productos,
    ICategoriaProductoApiClient categorias,
    IAlmacenApiClient almacenes,
    IFacturaVentaApiClient facturas,
    INotaCreditoVentaApiClient notas,
    ICuentaContableApiClient cuentas,
    ITerminoPagoApiClient terminos,
    IUnidadMedidaApiClient unidades,
    ILogger<IndicadoresModulos> logger) : IIndicadoresModulos
{
    // Solo el total: una página de un elemento basta para leer PagedResult.Total.
    private static readonly PageRequest Uno = new(1, 1);

    public async Task<IReadOnlyList<Indicador>> ObtenerAsync(string grupo, ClaimsPrincipal usuario, CancellationToken cancellationToken = default)
    {
        if (!(await autorizacion.AuthorizeAsync(usuario, ApplicationPolicies.CanConsult)).Succeeded)
        {
            return [];
        }

        var resultado = new List<Indicador>();
        foreach (var (titulo, ruta, contar) in Definiciones(grupo, cancellationToken).Take(3))
        {
            try
            {
                resultado.Add(new Indicador(titulo, (await contar()).ToString("N0", CultureInfo.InvariantCulture), ruta));
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "No fue posible calcular el indicador {Indicador} del grupo {Grupo}.", titulo, grupo);
            }
        }

        return resultado;
    }

    private List<(string Titulo, string? Ruta, Func<Task<long>> Contar)> Definiciones(string grupo, CancellationToken ct) => grupo switch
    {
        "clientes" =>
        [
            ("Clientes registrados", "/clientes", async () => (await socios.ListAsync(null, Uno, ct)).Total),
        ],
        "productos" =>
        [
            ("Productos", "/productos", async () => (await productos.ListAsync(null, Uno, ct)).Total),
            ("Sin existencia", "/productos", async () => (await productos.ListAsync(new ProductoSearchFilter(null, null, null, null, null, null, null, null, "without"), Uno, ct)).Total),
            ("Categorías", "/categorias-producto", async () => (await categorias.ListAsync(null, Uno, ct)).Total),
        ],
        "inventario" =>
        [
            ("Almacenes", "/almacenes", async () => (await almacenes.ListAsync(null, Uno, ct)).Total),
        ],
        "facturacion" =>
        [
            ("Borradores abiertos", "/facturas-venta/borradores?estado=1", async () => (await facturas.ListBorradoresAsync(new FacturaVentaBorradorFiltro(null, null, null, 1), Uno, ct)).Total),
            ("Facturas posteadas", "/facturas-venta", async () => (await facturas.ListFacturasAsync(null, Uno, ct)).Total),
            ("Borradores de nota de crédito", "/notas-credito-venta/borradores", async () => (await notas.ListBorradoresAsync(null, Uno, ct)).Total),
        ],
        "contabilidad" =>
        [
            ("Cuentas contables", "/cuentas-contables", async () => (await cuentas.ListAsync(null, Uno, ct)).Total),
        ],
        "configuracion" =>
        [
            ("Términos de pago", "/terminos-pago", async () => (await terminos.ListAsync(null, Uno, ct)).Total),
            ("Unidades de medida", "/unidades-medida", async () => (await unidades.ListAsync(null, Uno, ct)).Total),
        ],
        _ => [],
    };
}
```

En `Program.cs`, tras `builder.Services.AddScoped<IRegistroModulos, RegistroModulos>();`:

```csharp
builder.Services.AddScoped<IIndicadoresModulos, IndicadoresModulos>();
```

- [ ] **Step 4: Página de grupo**

`src/OpenSource1.Blazor/Components/Pages/ModuloGrupo.razor`:

```razor
@page "/modulos/{Grupo}"
@attribute [Authorize]
@using Microsoft.AspNetCore.Authorization
@inject IRegistroModulos Registro
@inject IIndicadoresModulos Indicadores
@inject NavigationManager Nav

<PageTitle>@(grupo?.Titulo ?? "Módulos") – AxionERP</PageTitle>

@if (grupo is not null)
{
    <nav aria-label="Migas de pan" class="mb-2 text-xs text-slate-500 dark:text-slate-400"><a href="/" class="hover:text-brand-600">Inicio</a> <span aria-hidden="true">/</span> <span aria-current="page" class="font-semibold text-slate-700 dark:text-slate-200">@grupo.Titulo</span></nav>
    <div class="mb-6 flex items-center gap-3">
        <div class="flex size-10 items-center justify-center rounded-xl bg-brand-50 text-brand-600 dark:bg-brand-500/10 dark:text-brand-400">
            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.8" stroke="currentColor" class="size-5" aria-hidden="true"><path stroke-linecap="round" stroke-linejoin="round" d="@grupo.Icono" /></svg>
        </div>
        <div>
            <h1 class="text-2xl font-extrabold tracking-tight text-slate-900 dark:text-slate-100">@grupo.Titulo</h1>
            <p class="text-sm text-slate-500 dark:text-slate-400">@grupo.Descripcion</p>
        </div>
    </div>

    @if (indicadores.Count > 0)
    {
        <div class="mb-6 grid grid-cols-1 gap-4 sm:grid-cols-3" data-testid="indicadores-grupo">
            @foreach (var indicador in indicadores)
            {
                <a href="@(indicador.Ruta ?? "#")" class="rounded-2xl border border-slate-200 bg-white px-5 py-4 shadow-sm hover:border-brand-200 dark:border-slate-700 dark:bg-slate-800">
                    <p class="text-xs font-bold uppercase tracking-wide text-slate-400 dark:text-slate-500">@indicador.Titulo</p>
                    <p class="mt-1 text-2xl font-extrabold text-slate-900 dark:text-slate-100">@indicador.Valor</p>
                </a>
            }
        </div>
    }

    @if (modulos.Count == 0)
    {
        <MessageBox Type="info" Message="No tiene módulos disponibles en este grupo." />
    }
    else
    {
        <div class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
            @foreach (var modulo in modulos)
            {
                <a href="@modulo.Ruta" data-testid="tarjeta-modulo" class="flex gap-3 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm hover:border-brand-200 hover:shadow-md dark:border-slate-700 dark:bg-slate-800">
                    <span class="flex size-9 shrink-0 items-center justify-center rounded-lg bg-slate-50 text-slate-500 dark:bg-slate-900/40 dark:text-slate-300">
                        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.8" stroke="currentColor" class="size-4" aria-hidden="true"><path stroke-linecap="round" stroke-linejoin="round" d="@modulo.Icono" /></svg>
                    </span>
                    <span>
                        <span class="block text-sm font-bold text-slate-900 dark:text-slate-100">@modulo.Titulo</span>
                        <span class="block text-xs text-slate-500 dark:text-slate-400">@modulo.Descripcion</span>
                    </span>
                </a>
            }
        </div>
    }
}

@code {
    [Parameter] public string Grupo { get; set; } = string.Empty;
    [CascadingParameter] private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    private GrupoModulo? grupo;
    private IReadOnlyList<Modulo> modulos = [];
    private IReadOnlyList<Indicador> indicadores = [];

    protected override async Task OnInitializedAsync()
    {
        grupo = Registro.BuscarGrupo(Grupo);
        if (grupo is null)
        {
            // .NET 10: responde 404 y renderiza la NotFoundPage del Router.
            Nav.NotFound();
            return;
        }

        if (AuthenticationStateTask is null)
        {
            return;
        }

        var usuario = (await AuthenticationStateTask).User;
        modulos = (await Registro.VisiblesAsync(usuario)).Where(m => m.Grupo == grupo.Clave).ToList();
        if (modulos.Count > 0)
        {
            indicadores = await Indicadores.ObtenerAsync(grupo.Clave, usuario);
        }
    }
}
```

- [ ] **Step 5: Reemplazar `Home.razor`**

Reemplazar el archivo completo por:

```razor
@page "/"
@using Microsoft.AspNetCore.Authorization
@using OpenSource1.Application.Security
@using OpenSource1.Blazor.Services
@inject ISocioNegocioApiClient SocioNegocioApiClient
@inject IProductoApiClient ProductoApiClient
@inject IRegistroModulos Registro
@inject ILogger<Home> Logger
@inject IAuthorizationService AuthorizationService

<PageTitle>Inicio – AxionERP</PageTitle>

<AuthorizeView>
    <Authorized Context="auth">
        <div class="mb-6 flex flex-col gap-1">
            <p class="text-xs font-bold uppercase tracking-widest text-brand-600 dark:text-brand-400">Inicio</p>
            <h1 class="text-2xl font-extrabold tracking-tight text-slate-900 dark:text-slate-100">Bienvenido, @auth.User.Identity?.Name</h1>
            <p class="text-sm text-slate-500 dark:text-slate-400">Rol: @string.Join(", ", auth.User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value))</p>
        </div>

        @if (canConsult)
        {
            @if (dashboardError is not null)
            {
                <div class="mb-6"><MessageBox Type="warning" Message="@dashboardError" /></div>
            }
            else
            {
                <div class="mb-8 grid grid-cols-2 gap-3 lg:grid-cols-4" data-testid="kpis-inicio">
                    @Kpi("Clientes", totalClientes.ToString(System.Globalization.CultureInfo.InvariantCulture), "/dashboard/clientes", null)
                    @Kpi("Productos", totalProductos.ToString(System.Globalization.CultureInfo.InvariantCulture), "/dashboard/productos", null)
                    @Kpi("Sin existencia", productosSinStock.ToString(System.Globalization.CultureInfo.InvariantCulture), "/productos", productosSinStock > 0 ? "text-red-600 dark:text-red-400" : null)
                    @Kpi("Existencia baja (≤5)", productosStockBajo.ToString(System.Globalization.CultureInfo.InvariantCulture), "/productos", productosStockBajo > 0 ? "text-amber-600 dark:text-amber-400" : null)
                </div>
            }
        }

        <h2 class="mb-3 text-sm font-bold uppercase tracking-wide text-slate-700 dark:text-slate-300">Módulos</h2>
        <div class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3" data-testid="grupos-inicio">
            @foreach (var grupo in grupos)
            {
                <a href="@($"/modulos/{grupo.Clave}")" class="flex gap-3 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm hover:border-brand-200 hover:shadow-md dark:border-slate-700 dark:bg-slate-800">
                    <span class="flex size-10 shrink-0 items-center justify-center rounded-xl bg-brand-50 text-brand-600 dark:bg-brand-500/10 dark:text-brand-400">
                        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.8" stroke="currentColor" class="size-5" aria-hidden="true"><path stroke-linecap="round" stroke-linejoin="round" d="@grupo.Icono" /></svg>
                    </span>
                    <span>
                        <span class="block text-sm font-bold text-slate-900 dark:text-slate-100">@grupo.Titulo</span>
                        <span class="block text-xs text-slate-500 dark:text-slate-400">@grupo.Descripcion</span>
                        <span class="mt-1 block text-[11px] font-semibold text-slate-400">@conteos[grupo.Clave] módulo(s)</span>
                    </span>
                </a>
            }
        </div>
    </Authorized>
    <NotAuthorized>
        <div class="rounded-2xl border border-slate-200 bg-white px-8 py-10 shadow-sm dark:border-slate-700 dark:bg-slate-800">
            <h1 class="mb-1 text-lg font-bold text-slate-800 dark:text-slate-200">Bienvenido a AxionERP</h1>
            <p class="mb-6 max-w-md text-sm text-slate-500 dark:text-slate-400">Inicie sesión para acceder a su panel de trabajo según el rol que le fue asignado.</p>
            <div class="flex flex-col gap-3 sm:flex-row">
                <a href="/account/login" class="inline-flex items-center justify-center rounded-xl bg-brand-600 px-5 py-2.5 text-sm font-semibold text-white shadow-sm hover:bg-brand-700">Iniciar Sesión</a>
                <a href="/account/register" class="inline-flex items-center justify-center rounded-xl border border-brand-200 bg-brand-50 px-5 py-2.5 text-sm font-semibold text-brand-700 hover:bg-brand-100 dark:border-brand-500/30 dark:bg-brand-500/10 dark:text-brand-400">Registrarse</a>
            </div>
        </div>
    </NotAuthorized>
</AuthorizeView>

@code {
    private int totalClientes;
    private int totalProductos;
    private int productosSinStock;
    private int productosStockBajo;
    private bool canConsult;
    private string? dashboardError;
    private IReadOnlyList<GrupoModulo> grupos = [];
    private Dictionary<string, int> conteos = [];

    [CascadingParameter] private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    protected override async Task OnInitializedAsync()
    {
        if (AuthenticationStateTask is null)
        {
            return;
        }

        var user = (await AuthenticationStateTask).User;
        if (user.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var visibles = await Registro.VisiblesAsync(user);
        conteos = visibles.GroupBy(m => m.Grupo).ToDictionary(g => g.Key, g => g.Count());
        grupos = Registro.Grupos.Where(g => conteos.ContainsKey(g.Clave)).OrderBy(g => g.Orden).ToList();

        canConsult = (await AuthorizationService.AuthorizeAsync(user, ApplicationPolicies.CanConsult)).Succeeded;
        if (!canConsult)
        {
            return;
        }

        try
        {
            var clientes = await SocioNegocioApiClient.ListAllAsync();
            var productos = await ProductoApiClient.ListAllAsync();
            totalClientes = clientes.Count;
            totalProductos = productos.Count;
            productosSinStock = productos.Count(p => p.Existencia <= 0);
            productosStockBajo = productos.Count(p => p.Existencia is > 0 and <= 5);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not load dashboard data.");
            dashboardError = "No fue posible cargar los indicadores en este momento.";
        }
    }

    private static RenderFragment Kpi(string titulo, string valor, string ruta, string? claseValor) => __builder =>
    {
        <a href="@ruta" class="rounded-xl border border-slate-200 bg-white px-4 py-3 shadow-sm hover:border-brand-200 dark:border-slate-700 dark:bg-slate-800">
            <p class="text-[11px] font-bold uppercase tracking-wide text-slate-400 dark:text-slate-500">@titulo</p>
            <p class="mt-0.5 text-xl font-extrabold @(claseValor ?? "text-slate-900 dark:text-slate-100")">@valor</p>
        </a>
    };
}
```

Decisión registrada (ver "Ambigüedades"): los gráficos, la actividad reciente y las guías por rol del inicio anterior se retiran del inicio; siguen disponibles en `/dashboard/clientes` y `/dashboard/productos`.

- [ ] **Step 6: Ejecutar los tests**

Run: `dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~InicioYGruposTests|FullyQualifiedName~NavMenuTests"`
Expected: PASS. Si `Nav.NotFound()` no compila (API no disponible en la versión instalada), sustituirlo por `HttpContextAccessor.HttpContext!.Response.StatusCode = StatusCodes.Status404NotFound;` + renderizar `<NotFound />` y mantener el test de 404.

- [ ] **Step 7: Build sin avisos**

Run: `dotnet build test.slnx -warnaserror`
Expected: 0 avisos.

- [ ] **Step 8: Commit**

```bash
git add src/OpenSource1.Blazor/Navigation/IIndicadoresModulos.cs src/OpenSource1.Blazor/Navigation/IndicadoresModulos.cs src/OpenSource1.Blazor/Program.cs src/OpenSource1.Blazor/Components/Pages/Home.razor src/OpenSource1.Blazor/Components/Pages/ModuloGrupo.razor tests/OpenSource1.SmokeTests/Blazor/InicioYGruposTests.cs
git commit -m "feat: inicio compacto con tarjetas de grupo y paginas de grupo con indicadores"
```

---

### Task A6: Sin flash azul — barra de progreso tardía y sin animaciones de entrada

**Grupo de paralelismo:** tras A2; en paralelo con A3b, A4 y A5 (no toca `Home.razor`, `MainLayout.razor`, `App.razor` ni `Program.cs`).

**Files:**
- Modify: `src/OpenSource1.Blazor/wwwroot/app.css:24-33` (`.blazor-loading-bar`)
- Modify: `src/OpenSource1.Blazor/wwwroot/app.interactions.js` (retardo 150 ms; overlay solo en envíos de formulario)
- Modify (quitar clases `animate-fade-in|animate-slide-up|animate-slide-up-slow|animate-slide-in-left|animate-pop|animate-pulse-glow|animate-float`): `Components/Pages/Bitacora.razor`, `ChangePassword.razor`, `ClientesDashboard.razor`, `Clientes.razor`, `ForgotPassword.razor`, `Login.razor`, `ProductosDashboard.razor`, `Productos.razor`, `Register.razor`, `Reporteria.razor`, `ResetPassword.razor`, `UserManagement.razor`, `UserProfile.razor` (todas bajo `src/OpenSource1.Blazor/`)
- Test: `tests/OpenSource1.SmokeTests/Blazor/SinFlashTests.cs`

**Interfaces:**
- Consumes: nada de otras tasks.
- Produces: `app.interactions.js` con `const loadingDelayMs = 150;` y la bandera `overlayPermitido`; `.blazor-loading-bar` de 2 px y `rgba(86, 127, 255, 0.55)`.

- [ ] **Step 1: Test por inspección del código fuente (falla)**

`tests/OpenSource1.SmokeTests/Blazor/SinFlashTests.cs`:

```csharp
using System.Text.RegularExpressions;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>
/// Fix-Features A6: el "flash azul" venía del hero con gradiente animado del inicio (A5 lo retira; lo cubre
/// InicioYGruposTests) y de la barra de progreso de cada navegación. Aquí: ninguna página SSR usa animaciones de entrada,
/// la barra mide 2 px con color atenuado y solo aparece si la navegación tarda más de 150 ms. ConfirmDialog (modal) queda
/// fuera: su fundido no es una entrada de página.
/// </summary>
public sealed class SinFlashTests
{
    private static readonly Regex AnimacionEntrada = new("animate-(fade-in|slide-up|slide-up-slow|slide-in-left|pop|pulse-glow|float)\\b");

    [Fact]
    public void NingunaPagina_UsaAnimacionesDeEntrada()
    {
        var paginas = Directory.GetFiles(Path.Combine(Raiz(), "src", "OpenSource1.Blazor", "Components", "Pages"), "*.razor")
            .Where(p => !p.EndsWith("Home.razor", StringComparison.Ordinal));

        var conAnimacion = paginas.Where(p => AnimacionEntrada.IsMatch(File.ReadAllText(p))).Select(Path.GetFileName).ToList();

        Assert.True(conAnimacion.Count == 0, "Páginas con animación de entrada: " + string.Join(", ", conAnimacion));
    }

    [Fact]
    public void BarraDeProgreso_Delgada_Atenuada_YTardia()
    {
        var css = File.ReadAllText(Path.Combine(Raiz(), "src", "OpenSource1.Blazor", "wwwroot", "app.css"));
        var js = File.ReadAllText(Path.Combine(Raiz(), "src", "OpenSource1.Blazor", "wwwroot", "app.interactions.js"));
        var regla = Regex.Match(css, "\\.blazor-loading-bar\\s*\\{(?<cuerpo>[^}]*)\\}").Groups["cuerpo"].Value;

        Assert.Contains("height: 2px;", regla);
        Assert.DoesNotContain("#2155d9", regla);
        Assert.Contains("const loadingDelayMs = 150;", js);
        Assert.Contains("overlayPermitido", js);
    }

    private static string Raiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "test.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio (test.slnx).");
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~SinFlashTests"`
Expected: FAIL (13 páginas con animación; barra de 3 px `#2155d9`; retardo 180).

- [ ] **Step 3: CSS de la barra**

En `app.css`, sustituir la regla `.blazor-loading-bar` por:

```css
/* Barra de progreso de navegación (Fix-Features A6): solo aparece si la navegación tarda > 150 ms (app.interactions.js),
   2 px y color de marca atenuado para no producir un destello azul en navegaciones rápidas. */
.blazor-loading-bar {
    height: 2px;
    background: rgba(86, 127, 255, 0.55);
    position: fixed;
    top: 0;
    left: 0;
    z-index: 9999;
    transition: width 0.2s ease;
    pointer-events: none;
}
```

- [ ] **Step 4: JS de carga**

En `app.interactions.js`:
1. Cambiar `const loadingDelayMs = 180;` por `const loadingDelayMs = 150;`.
2. Tras `let progressBarActive = false;` añadir:

```javascript
  // El velo de pantalla completa solo acompaña envíos de formulario (operaciones que escriben); en la navegación por enlaces
  // únicamente puede aparecer la barra fina, y solo si tarda más de loadingDelayMs (Fix-Features A6).
  let overlayPermitido = false;
```

3. En `showLoading()`, envolver el bloque del overlay:

```javascript
  function showLoading() {
    const el = overlay();
    if (el && overlayPermitido) {
      el.classList.add('is-visible');
      el.setAttribute('aria-hidden', 'false');
    }
```

(el resto de la función, la barra, queda igual).
4. En `hideLoading()`, añadir como primera línea `overlayPermitido = false;`.
5. En el listener de `submit`, justo antes de `if (!form.hasAttribute('data-enhance'))`, añadir `overlayPermitido = true;`.
6. En el listener de `click` de enlaces, justo antes de `hardNavigationPending = true;`, añadir `overlayPermitido = false;`.

- [ ] **Step 5: Quitar las animaciones de entrada de las 13 páginas**

Para cada archivo listado en **Files**, eliminar de los atributos `class` los tokens `animate-fade-in`, `animate-slide-up`, `animate-slide-up-slow`, `animate-slide-in-left`, `animate-pop`, `animate-pulse-glow` y `animate-float` (solo el token; el resto de clases se conserva). Comando de apoyo (revisar el diff después):

```bash
cd /home/ray/test/src/OpenSource1.Blazor/Components/Pages && sed -i -E 's/ ?animate-(fade-in|slide-up-slow|slide-up|slide-in-left|pop|pulse-glow|float)\b//g' Bitacora.razor ChangePassword.razor ClientesDashboard.razor Clientes.razor ForgotPassword.razor Login.razor ProductosDashboard.razor Productos.razor Register.razor Reporteria.razor ResetPassword.razor UserManagement.razor UserProfile.razor && git diff --stat
```

Expected: solo cambian atributos `class`; ninguna línea desaparece.

- [ ] **Step 6: Ejecutar tests y sintaxis del JS**

Run: `node --check src/OpenSource1.Blazor/wwwroot/app.interactions.js && dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~SinFlashTests"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/OpenSource1.Blazor/wwwroot/app.css src/OpenSource1.Blazor/wwwroot/app.interactions.js src/OpenSource1.Blazor/Components/Pages tests/OpenSource1.SmokeTests/Blazor/SinFlashTests.cs
git commit -m "fix: sin destello azul al navegar con barra de progreso tardia y sin animaciones de entrada"
```

---

### Task G-A: Puerta de la Fase A

**Grupo de paralelismo:** serie (tras A1–A6).

**Files:** ninguno nuevo (solo arreglos si algo falla, en el archivo de la task responsable).

- [ ] **Step 1: Build y suite completa**

Run: `dotnet build test.slnx -warnaserror && env DOCKER_CONTEXT=default dotnet test test.slnx`
Expected: 0 avisos; suite verde (1361 + los tests nuevos de A).

- [ ] **Step 2: Runtime sobre Docker**

Run (desde la raíz): `docker compose up -d --build api blazor` y comprobar con el navegador (o `playwright-cli`) en `http://localhost:8080` como `admin` / `ejecutor` (contraseña: `AUTH_SEED_DEFAULT_PASSWORD` de `.env`):
- menú por grupos con el grupo de la página abierto; Ejecutor sin Administración;
- Ctrl+K y `/` abren la paleta; flechas/Enter/Esc; Enter sin selección lleva a `/buscar?q=`;
- `/buscar?q=<texto>` con JS desactivado sigue funcionando;
- inicio sin hero azul; `/modulos/facturacion` con indicadores; navegación entre páginas sin destello azul.
Expected: todo OK; anotar incidencias como arreglos en la task responsable (commit `fix: …`).

- [ ] **Step 3: Sin commit si no hubo arreglos.**

---
## Fase B — Patrón de página

### Task B1: Componentes compartidos (barra de acciones, página-tarjeta, selección)

**Grupo de paralelismo:** serie (tras G-A). Es el único task de la Fase B que toca `App.razor` y los componentes compartidos.

**Files:**
- Create: `src/OpenSource1.Blazor/Components/AccionPagina.cs`
- Create: `src/OpenSource1.Blazor/Components/RetornoLocal.cs`
- Create: `src/OpenSource1.Blazor/Components/PageToolbar.razor`
- Create: `src/OpenSource1.Blazor/Components/EntityFormPage.razor`
- Create: `src/OpenSource1.Blazor/Components/FormularioAcciones.razor`
- Create: `src/OpenSource1.Blazor/Components/SeleccionFila.razor`
- Create: `src/OpenSource1.Blazor/Components/TarjetaAcciones.razor`
- Create: `src/OpenSource1.Blazor/wwwroot/app.menus.js`
- Modify: `src/OpenSource1.Blazor/Components/App.razor` (script tras `app.search.js`)
- Test: `tests/OpenSource1.SmokeTests/Blazor/RetornoLocalTests.cs`
- Test: `tests/OpenSource1.SmokeTests/Blazor/ComponentesPaginaTests.cs`

**Interfaces:**
- Consumes: `IconosModulo` (A1), `ApplicationPolicies`, `MessageBox` (existente), `RenderizadorComponentes` (A1).
- Produces (usadas por B2a–B2d y C1–C3; namespace `OpenSource1.Blazor.Components`):
  - `record AccionPagina(string Etiqueta, string Icono, Func<string?, string> UrlConId, string? Permiso = null, bool RequiereSeleccion = false)`; `record Miga(string Texto, string? Href = null)`; `static class AccionesPermitidas { Task<IReadOnlyList<AccionPagina>> FiltrarAsync(IAuthorizationService, ClaimsPrincipal, IEnumerable<AccionPagina>); Task<bool> PuedeAsync(IAuthorizationService, ClaimsPrincipal, string? permiso); }`.
  - `static class RetornoLocal { string Validar(string? returnUrl, string predeterminada); string ConRetorno(string destino, string returnUrl); string ConParametro(string url, string clave, string valor); }`.
  - `<PageToolbar Titulo Subtitulo Migas NuevoHref NuevoTexto NuevoPermiso(=CanAdd) Crear Ver SeleccionId EditarUrl(Func<string,string>) EditarPermiso(=CanModify) EliminarUrl(Func<string,string>) EliminarPermiso(=CanDelete)>ChildContent</PageToolbar>`; `PageToolbar.SinSeleccion` (texto del `title`); `data-testid`: `page-toolbar`, `accion-nuevo`, `accion-editar`, `accion-eliminar`, `menu-crear`, `menu-ver`.
  - `<EntityFormPage Titulo Subtitulo Migas Mensaje TipoMensaje Errores>ChildContent</EntityFormPage>` (`data-testid="entity-form-page"`).
  - `<FormularioAcciones CancelarHref LimpiarHref TextoGuardar Deshabilitado />` (`data-testid`: `guardar`, `limpiar`, `cancelar`).
  - `<SeleccionFila Href Seleccionada Etiqueta />` y `SeleccionFila.ClaseFilaSeleccionada`.
  - `<TarjetaAcciones Id Rapidas Resto />` (2 rápidas visibles + `⋯` con el resto; `data-testid="tarjeta-acciones"`).

- [ ] **Step 1: Tests de `RetornoLocal` (fallan)**

`tests/OpenSource1.SmokeTests/Blazor/RetornoLocalTests.cs`:

```csharp
extern alias BlazorApp;

using BlazorApp::OpenSource1.Blazor.Components;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>Review Focus 2: el returnUrl de las páginas-tarjeta solo acepta rutas locales; todo lo demás vuelve a la lista.</summary>
public sealed class RetornoLocalTests
{
    [Theory]
    [InlineData("/clientes?nombre=ana&pagina=2")]
    [InlineData("/unidades-medida")]
    public void Validar_RutaLocal_SeConserva(string url) => Assert.Equal(url, RetornoLocal.Validar(url, "/defecto"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://evil.com")]
    [InlineData("//evil.com")]
    [InlineData("/\\evil.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("clientes")]
    [InlineData("/clientes\n")]
    [InlineData("/redir?to=http://evil.com")]
    public void Validar_NoLocal_DevuelveLaPredeterminada(string? url) => Assert.Equal("/defecto", RetornoLocal.Validar(url, "/defecto"));

    [Fact]
    public void ConParametro_AgregaOReemplaza()
    {
        Assert.Equal("/clientes?ok=created", RetornoLocal.ConParametro("/clientes", "ok", "created"));
        Assert.Equal("/clientes?pagina=2&ok=created", RetornoLocal.ConParametro("/clientes?ok=deleted&pagina=2", "ok", "created"));
    }

    [Fact]
    public void ConRetorno_CodificaLaRutaDeVuelta()
    {
        Assert.Equal(
            "/unidades-medida/nuevo?returnUrl=%2Funidades-medida%3Fcodigo%3DK%20G",
            RetornoLocal.ConRetorno("/unidades-medida/nuevo", "/unidades-medida?codigo=K G"));
    }
}
```

- [ ] **Step 2: Tests de componentes (fallan)**

`tests/OpenSource1.SmokeTests/Blazor/ComponentesPaginaTests.cs`:

```csharp
extern alias BlazorApp;

using BlazorApp::OpenSource1.Blazor.Components;
using BlazorApp::OpenSource1.Blazor.Navigation;
using OpenSource1.Application.Security;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>Fix-Features B1: barra de acciones, página-tarjeta, selección y acciones de tarjeta renderizadas con HtmlRenderer.</summary>
public sealed class ComponentesPaginaTests
{
    private static readonly Func<string, string> Editar = id => $"/x/{id}/editar";
    private static readonly Func<string, string> Eliminar = id => $"/x?deleteId={id}";

    [Fact]
    public async Task Toolbar_Admin_SinSeleccion_NuevoVisible_EditarYEliminarDeshabilitados()
    {
        var html = await ToolbarAsync("Administrador", seleccion: null);

        Assert.Contains("data-testid=\"page-toolbar\"", html);
        Assert.Contains("<a href=\"/x/nuevo\" data-testid=\"accion-nuevo\"", html);
        Assert.Contains($"<span data-testid=\"accion-editar\" aria-disabled=\"true\" title=\"{PageToolbar.SinSeleccion}\"", html);
        Assert.Contains("<span data-testid=\"accion-eliminar\" aria-disabled=\"true\"", html);
    }

    [Fact]
    public async Task Toolbar_ConSeleccion_EnlazaConElId()
    {
        var html = await ToolbarAsync("Administrador", seleccion: "abc");

        Assert.Contains("<a data-testid=\"accion-editar\" href=\"/x/abc/editar\"", html);
        Assert.Contains("<a data-testid=\"accion-eliminar\" href=\"/x?deleteId=abc\"", html);
    }

    [Fact]
    public async Task Toolbar_Permisos_EjecutorSinEditarNiEliminar_SupervisorSinNuevo()
    {
        var ejecutor = await ToolbarAsync("Ejecutor", seleccion: "abc");
        var supervisor = await ToolbarAsync("Supervisor", seleccion: "abc");

        Assert.Contains("accion-nuevo", ejecutor);
        Assert.DoesNotContain("accion-editar", ejecutor);
        Assert.DoesNotContain("accion-eliminar", ejecutor);
        Assert.DoesNotContain("accion-nuevo", supervisor);
        Assert.Contains("accion-editar", supervisor);
        Assert.DoesNotContain("accion-eliminar", supervisor);
    }

    [Fact]
    public async Task Toolbar_MenusCrearYVer_FiltranPorPermiso_YDeshabilitanSinSeleccion()
    {
        IReadOnlyList<AccionPagina> crear = [new("Factura", IconosModulo.Documento, id => $"/facturas-venta/nueva?socioId={id}", ApplicationPolicies.CanAdd, RequiereSeleccion: true)];
        IReadOnlyList<AccionPagina> ver =
        [
            new("Ficha", IconosModulo.Persona, id => $"/clientes/{id}", null, RequiereSeleccion: true),
            new("Panel", IconosModulo.Grafico, _ => "/dashboard/clientes"),
        ];

        var adminSinSeleccion = await ToolbarAsync("Administrador", null, crear, ver);
        var adminConSeleccion = await ToolbarAsync("Administrador", "S1", crear, ver);
        var supervisor = await ToolbarAsync("Supervisor", "S1", crear, ver);

        Assert.Contains("data-testid=\"menu-crear\"", adminSinSeleccion);
        Assert.Contains("<span role=\"menuitem\" aria-disabled=\"true\"", adminSinSeleccion);
        Assert.Contains("href=\"/dashboard/clientes\"", adminSinSeleccion);
        Assert.Contains("href=\"/facturas-venta/nueva?socioId=S1\"", adminConSeleccion);
        Assert.Contains("href=\"/clientes/S1\"", adminConSeleccion);
        Assert.DoesNotContain("data-testid=\"menu-crear\"", supervisor);
        Assert.Contains("data-testid=\"menu-ver\"", supervisor);
    }

    [Fact]
    public async Task EntityFormPage_MigasTituloMensaje_YAcciones()
    {
        var html = await RenderizadorComponentes.RenderizarAsync<EntityFormPage>(
            PermisosTestAuthHandler.Principal("Administrador"),
            new Dictionary<string, object?>
            {
                ["Titulo"] = "Nueva unidad de medida",
                ["Migas"] = (IReadOnlyList<Miga>)[new("Configuración", "/modulos/configuracion"), new("Nueva")],
                ["Mensaje"] = "El código ya existe.",
                ["TipoMensaje"] = "warning",
            });
        var acciones = await RenderizadorComponentes.RenderizarAsync<FormularioAcciones>(
            PermisosTestAuthHandler.Principal("Administrador"),
            new Dictionary<string, object?> { ["CancelarHref"] = "/unidades-medida?pagina=2", ["LimpiarHref"] = "/unidades-medida/nuevo" });

        Assert.Contains("data-testid=\"entity-form-page\"", html);
        Assert.Contains("href=\"/modulos/configuracion\"", html);
        Assert.Contains("Nueva unidad de medida", html);
        Assert.Contains("El código ya existe.", html);
        Assert.Contains("<button type=\"submit\" data-testid=\"guardar\"", acciones);
        Assert.Contains("href=\"/unidades-medida?pagina=2\" data-testid=\"cancelar\"", acciones);
        Assert.Contains("href=\"/unidades-medida/nuevo\" data-testid=\"limpiar\"", acciones);
    }

    [Fact]
    public async Task TarjetaAcciones_DosRapidas_ElRestoEnElMenu_SegunPermiso()
    {
        IReadOnlyList<AccionPagina> rapidas =
        [
            new("Ficha", IconosModulo.Persona, id => $"/clientes/{id}"),
            new("Editar", IconosModulo.Lista, id => $"/clientes/{id}/editar", ApplicationPolicies.CanModify),
            new("Factura", IconosModulo.Documento, id => $"/facturas-venta/nueva?socioId={id}", ApplicationPolicies.CanAdd),
        ];
        IReadOnlyList<AccionPagina> resto = [new("Cobros", IconosModulo.Tarjeta, id => $"/cobros?socioId={id}")];

        var admin = await RenderizadorComponentes.RenderizarAsync<TarjetaAcciones>(
            PermisosTestAuthHandler.Principal("Administrador"),
            new Dictionary<string, object?> { ["Id"] = "C1", ["Rapidas"] = rapidas, ["Resto"] = resto });
        var ejecutor = await RenderizadorComponentes.RenderizarAsync<TarjetaAcciones>(
            PermisosTestAuthHandler.Principal("Ejecutor"),
            new Dictionary<string, object?> { ["Id"] = "C1", ["Rapidas"] = rapidas, ["Resto"] = resto });

        var menuAdmin = admin[admin.IndexOf("<details", StringComparison.Ordinal)..];
        Assert.DoesNotContain("href=\"/clientes/C1/editar\"", menuAdmin);
        Assert.Contains("href=\"/facturas-venta/nueva?socioId=C1\"", menuAdmin);
        Assert.Contains("href=\"/cobros?socioId=C1\"", menuAdmin);
        Assert.Contains("aria-label=\"Más acciones\"", admin);
        Assert.DoesNotContain("href=\"/clientes/C1/editar\"", ejecutor);
        Assert.Contains("href=\"/facturas-venta/nueva?socioId=C1\"", ejecutor[..ejecutor.IndexOf("<details", StringComparison.Ordinal)]);
    }

    [Fact]
    public async Task SeleccionFila_Marcada_YNoMarcada()
    {
        var marcada = await RenderizadorComponentes.RenderizarAsync<SeleccionFila>(
            PermisosTestAuthHandler.Principal("Administrador"),
            new Dictionary<string, object?> { ["Href"] = "/x?sel=1", ["Seleccionada"] = true, ["Etiqueta"] = "KG" });
        var libre = await RenderizadorComponentes.RenderizarAsync<SeleccionFila>(
            PermisosTestAuthHandler.Principal("Administrador"),
            new Dictionary<string, object?> { ["Href"] = "/x?sel=2", ["Seleccionada"] = false, ["Etiqueta"] = "UND" });

        Assert.Contains("aria-current=\"true\"", marcada);
        Assert.Contains("aria-label=\"Seleccionar KG\"", marcada);
        Assert.DoesNotContain("aria-current", libre);
    }

    private static Task<string> ToolbarAsync(string rol, string? seleccion, IReadOnlyList<AccionPagina>? crear = null, IReadOnlyList<AccionPagina>? ver = null) =>
        RenderizadorComponentes.RenderizarAsync<PageToolbar>(
            PermisosTestAuthHandler.Principal(rol),
            new Dictionary<string, object?>
            {
                ["Titulo"] = "Unidades de Medida",
                ["NuevoHref"] = "/x/nuevo",
                ["SeleccionId"] = seleccion,
                ["EditarUrl"] = Editar,
                ["EliminarUrl"] = Eliminar,
                ["Crear"] = crear ?? [],
                ["Ver"] = ver ?? [],
            });
}
```

- [ ] **Step 3: Ejecutar y ver que fallan**

Run: `dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~RetornoLocalTests|FullyQualifiedName~ComponentesPaginaTests"`
Expected: FAIL de compilación.

- [ ] **Step 4: Tipos de apoyo**

`src/OpenSource1.Blazor/Components/AccionPagina.cs`:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace OpenSource1.Blazor.Components;

/// <summary>
/// Acción de <see cref="PageToolbar"/> o de <see cref="TarjetaAcciones"/> (Fix-Features B1). <c>UrlConId</c> recibe el id
/// seleccionado (null sin selección). <c>Permiso</c> es una política (ApplicationPolicies.*): sin permiso la acción no se
/// muestra. <c>RequiereSeleccion</c>: sin selección se muestra deshabilitada con <see cref="PageToolbar.SinSeleccion"/>.
/// </summary>
public sealed record AccionPagina(
    string Etiqueta, string Icono, Func<string?, string> UrlConId, string? Permiso = null, bool RequiereSeleccion = false);

public sealed record Miga(string Texto, string? Href = null);

public static class AccionesPermitidas
{
    public static async Task<IReadOnlyList<AccionPagina>> FiltrarAsync(
        IAuthorizationService autorizacion, ClaimsPrincipal usuario, IEnumerable<AccionPagina> acciones)
    {
        var permitidas = new List<AccionPagina>();
        foreach (var accion in acciones)
        {
            if (await PuedeAsync(autorizacion, usuario, accion.Permiso))
            {
                permitidas.Add(accion);
            }
        }

        return permitidas;
    }

    public static async Task<bool> PuedeAsync(IAuthorizationService autorizacion, ClaimsPrincipal usuario, string? permiso) =>
        permiso is null || (await autorizacion.AuthorizeAsync(usuario, permiso)).Succeeded;
}
```

`src/OpenSource1.Blazor/Components/RetornoLocal.cs`:

```csharp
namespace OpenSource1.Blazor.Components;

/// <summary>
/// returnUrl de las páginas-tarjeta (Fix-Features B1, Review Focus 2): solo rutas locales absolutas ("/…"); se rechazan
/// "//host", "/\host", esquemas ("://", "javascript:") y caracteres de control, y se usa la ruta por defecto.
/// </summary>
public static class RetornoLocal
{
    public static string Validar(string? returnUrl, string predeterminada)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return predeterminada;
        }

        var url = returnUrl;
        var esLocal = url.StartsWith('/')
                      && !url.StartsWith("//", StringComparison.Ordinal)
                      && !url.StartsWith("/\\", StringComparison.Ordinal)
                      && !url.Contains("://", StringComparison.Ordinal)
                      && !url.Any(char.IsControl)
                      && url == url.Trim();
        return esLocal ? url : predeterminada;
    }

    public static string ConRetorno(string destino, string returnUrl) => ConParametro(destino, "returnUrl", returnUrl);

    /// <summary>Añade <paramref name="clave"/>=<paramref name="valor"/> a la query, reemplazando un valor previo de esa clave.</summary>
    public static string ConParametro(string url, string clave, string valor)
    {
        var partes = url.Split('?', 2);
        var pares = partes.Length == 2
            ? partes[1].Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Where(p => !p.StartsWith(clave + "=", StringComparison.Ordinal) && p != clave)
                .ToList()
            : [];
        pares.Add($"{clave}={Uri.EscapeDataString(valor)}");
        return $"{partes[0]}?{string.Join('&', pares)}";
    }
}
```

- [ ] **Step 5: Componentes**

`src/OpenSource1.Blazor/Components/PageToolbar.razor`:

```razor
@* PageToolbar — barra común de listados y fichas (Fix-Features B1): título, migas, + Nuevo, Crear ▾, Ver ▾, Editar y
   Eliminar. Una acción sin permiso no se renderiza; una que requiere selección, sin selección, se muestra deshabilitada con
   title explicativo. Menús con <details data-menu> nativos (app.menus.js solo los cierra al pulsar fuera o con Esc). *@
@using System.Security.Claims
@inject IAuthorizationService AuthorizationService

<div class="mb-6 flex flex-col gap-3" data-testid="page-toolbar">
    @if (Migas.Count > 0)
    {
        <nav aria-label="Migas de pan" class="text-xs text-slate-500 dark:text-slate-400">
            <ol class="flex flex-wrap items-center gap-1">
                @for (var i = 0; i < Migas.Count; i++)
                {
                    var miga = Migas[i];
                    <li class="flex items-center gap-1">
                        @if (i > 0)
                        {
                            <span aria-hidden="true">/</span>
                        }
                        @if (miga.Href is null)
                        {
                            <span aria-current="page" class="font-semibold text-slate-700 dark:text-slate-200">@miga.Texto</span>
                        }
                        else
                        {
                            <a href="@miga.Href" class="hover:text-brand-600">@miga.Texto</a>
                        }
                    </li>
                }
            </ol>
        </nav>
    }
    <div class="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
        <div class="min-w-0">
            <h1 class="text-2xl font-extrabold tracking-tight text-slate-900 dark:text-slate-100">@Titulo</h1>
            @if (!string.IsNullOrWhiteSpace(Subtitulo))
            {
                <p class="mt-1 text-sm text-slate-500 dark:text-slate-400">@Subtitulo</p>
            }
        </div>
        <div class="flex flex-wrap items-center gap-2" role="toolbar" aria-label="Acciones de la página">
            @ChildContent
            @if (NuevoHref is not null && puedeNuevo)
            {
                <a href="@NuevoHref" data-testid="accion-nuevo" class="@ClaseNuevo">+ @NuevoTexto</a>
            }
            @Menu("Crear", crear, "menu-crear")
            @Menu("Ver", ver, "menu-ver")
            @if (EditarUrl is not null && puedeEditar)
            {
                @Boton("Editar", SeleccionId is null ? null : EditarUrl(SeleccionId), "accion-editar", ClaseBoton)
            }
            @if (EliminarUrl is not null && puedeEliminar)
            {
                @Boton("Eliminar", SeleccionId is null ? null : EliminarUrl(SeleccionId), "accion-eliminar", ClaseEliminar)
            }
        </div>
    </div>
</div>

@code {
    public const string SinSeleccion = "Seleccione un registro de la lista para usar esta acción.";

    private const string ClaseBoton = "btn-press inline-flex items-center gap-1.5 rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm font-semibold text-slate-700 shadow-sm hover:bg-slate-50 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700/60";
    private const string ClaseNuevo = "btn-press inline-flex items-center gap-1.5 rounded-lg bg-brand-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-brand-700";
    private const string ClaseEliminar = "btn-press inline-flex items-center gap-1.5 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm font-semibold text-red-700 hover:bg-red-100 dark:border-red-500/30 dark:bg-red-500/10 dark:text-red-400";

    [Parameter, EditorRequired] public string Titulo { get; set; } = string.Empty;
    [Parameter] public string? Subtitulo { get; set; }
    [Parameter] public IReadOnlyList<Miga> Migas { get; set; } = [];
    [Parameter] public string? NuevoHref { get; set; }
    [Parameter] public string NuevoTexto { get; set; } = "Nuevo";
    [Parameter] public string? NuevoPermiso { get; set; } = ApplicationPolicies.CanAdd;
    [Parameter] public IReadOnlyList<AccionPagina> Crear { get; set; } = [];
    [Parameter] public IReadOnlyList<AccionPagina> Ver { get; set; } = [];
    [Parameter] public string? SeleccionId { get; set; }
    [Parameter] public Func<string, string>? EditarUrl { get; set; }
    [Parameter] public string? EditarPermiso { get; set; } = ApplicationPolicies.CanModify;
    [Parameter] public Func<string, string>? EliminarUrl { get; set; }
    [Parameter] public string? EliminarPermiso { get; set; } = ApplicationPolicies.CanDelete;
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [CascadingParameter] private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    private IReadOnlyList<AccionPagina> crear = [];
    private IReadOnlyList<AccionPagina> ver = [];
    private bool puedeNuevo;
    private bool puedeEditar;
    private bool puedeEliminar;

    protected override async Task OnParametersSetAsync()
    {
        var usuario = AuthenticationStateTask is null ? new ClaimsPrincipal() : (await AuthenticationStateTask).User;
        puedeNuevo = await AccionesPermitidas.PuedeAsync(AuthorizationService, usuario, NuevoPermiso);
        puedeEditar = await AccionesPermitidas.PuedeAsync(AuthorizationService, usuario, EditarPermiso);
        puedeEliminar = await AccionesPermitidas.PuedeAsync(AuthorizationService, usuario, EliminarPermiso);
        crear = await AccionesPermitidas.FiltrarAsync(AuthorizationService, usuario, Crear);
        ver = await AccionesPermitidas.FiltrarAsync(AuthorizationService, usuario, Ver);
    }

    private RenderFragment Menu(string etiqueta, IReadOnlyList<AccionPagina> acciones, string testId) => __builder =>
    {
        if (acciones.Count == 0)
        {
            return;
        }

        <details class="relative" data-menu data-testid="@testId">
            <summary class="@ClaseBoton cursor-pointer list-none [&::-webkit-details-marker]:hidden">@etiqueta ▾</summary>
            <div role="menu" class="absolute right-0 z-30 mt-1 min-w-56 rounded-xl border border-slate-200 bg-white py-1 shadow-lg dark:border-slate-700 dark:bg-slate-800">
                @foreach (var accion in acciones)
                {
                    @ItemMenu(accion)
                }
            </div>
        </details>
    };

    private RenderFragment ItemMenu(AccionPagina accion) => __builder =>
    {
        if (accion.RequiereSeleccion && SeleccionId is null)
        {
            <span role="menuitem" aria-disabled="true" title="@SinSeleccion" class="flex cursor-not-allowed items-center gap-2 px-3 py-2 text-sm text-slate-400">@Icono(accion.Icono) @accion.Etiqueta</span>
        }
        else
        {
            <a role="menuitem" href="@accion.UrlConId(SeleccionId)" class="flex items-center gap-2 px-3 py-2 text-sm text-slate-700 hover:bg-slate-50 dark:text-slate-200 dark:hover:bg-slate-700/60">@Icono(accion.Icono) @accion.Etiqueta</a>
        }
    };

    private static RenderFragment Boton(string etiqueta, string? href, string testId, string clase) => __builder =>
    {
        if (href is null)
        {
            <span data-testid="@testId" aria-disabled="true" title="@SinSeleccion" class="@clase cursor-not-allowed opacity-50">@etiqueta</span>
        }
        else
        {
            <a data-testid="@testId" href="@href" class="@clase">@etiqueta</a>
        }
    };

    private static RenderFragment Icono(string trazo) => __builder =>
    {
        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.8" stroke="currentColor" class="size-4 shrink-0" aria-hidden="true"><path stroke-linecap="round" stroke-linejoin="round" d="@trazo" /></svg>
    };
}
```

`src/OpenSource1.Blazor/Components/EntityFormPage.razor`:

```razor
@* EntityFormPage — página-tarjeta de alta/edición (Fix-Features B1). La página pone su EditForm en ChildContent, con
   <FormularioAcciones /> dentro del form (Guardar / Limpiar campos / Cancelar → vuelve a la lista con sus filtros). *@

<PageTitle>@Titulo – AxionERP</PageTitle>

<div class="mx-auto w-full max-w-3xl" data-testid="entity-form-page">
    @if (Migas.Count > 0)
    {
        <nav aria-label="Migas de pan" class="mb-2 text-xs text-slate-500 dark:text-slate-400">
            <ol class="flex flex-wrap items-center gap-1">
                @for (var i = 0; i < Migas.Count; i++)
                {
                    var miga = Migas[i];
                    <li class="flex items-center gap-1">
                        @if (i > 0)
                        {
                            <span aria-hidden="true">/</span>
                        }
                        @if (miga.Href is null)
                        {
                            <span aria-current="page" class="font-semibold text-slate-700 dark:text-slate-200">@miga.Texto</span>
                        }
                        else
                        {
                            <a href="@miga.Href" class="hover:text-brand-600">@miga.Texto</a>
                        }
                    </li>
                }
            </ol>
        </nav>
    }
    <div class="mb-6">
        <h1 class="text-2xl font-extrabold tracking-tight text-slate-900 dark:text-slate-100">@Titulo</h1>
        @if (!string.IsNullOrWhiteSpace(Subtitulo))
        {
            <p class="mt-1 text-sm text-slate-500 dark:text-slate-400">@Subtitulo</p>
        }
    </div>
    @if (!string.IsNullOrWhiteSpace(Mensaje) || Errores.Count > 0)
    {
        <div class="mb-6"><MessageBox Type="@TipoMensaje" Message="@Mensaje" Errors="@Errores" /></div>
    }
    <section class="rounded-2xl border border-slate-200 bg-white shadow-sm dark:border-slate-700 dark:bg-slate-800">
        <div class="px-6 py-6">@ChildContent</div>
    </section>
</div>

@code {
    [Parameter, EditorRequired] public string Titulo { get; set; } = string.Empty;
    [Parameter] public string? Subtitulo { get; set; }
    [Parameter] public IReadOnlyList<Miga> Migas { get; set; } = [];
    [Parameter] public string? Mensaje { get; set; }
    [Parameter] public string TipoMensaje { get; set; } = "warning";
    [Parameter] public IReadOnlyList<string> Errores { get; set; } = [];
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
```

`src/OpenSource1.Blazor/Components/FormularioAcciones.razor`:

```razor
@* FormularioAcciones — botones de la página-tarjeta, DENTRO del EditForm (Fix-Features B1). *@
<div class="mt-6 flex flex-wrap items-center gap-3 border-t border-slate-100 pt-5 dark:border-slate-700">
    @if (!Deshabilitado)
    {
        <button type="submit" data-testid="guardar" class="btn-press rounded-lg bg-brand-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-brand-700">@TextoGuardar</button>
    }
    @if (LimpiarHref is not null)
    {
        <a href="@LimpiarHref" data-testid="limpiar" class="btn-press rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-600 shadow-sm hover:bg-slate-50 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-300">Limpiar campos</a>
    }
    <a href="@CancelarHref" data-testid="cancelar" class="btn-press rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-600 shadow-sm hover:bg-slate-50 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-300">Cancelar</a>
</div>

@code {
    [Parameter, EditorRequired] public string CancelarHref { get; set; } = "/";
    [Parameter] public string? LimpiarHref { get; set; }
    [Parameter] public string TextoGuardar { get; set; } = "Guardar";
    [Parameter] public bool Deshabilitado { get; set; }
}
```

Nota: en `FormularioAcciones` el orden de atributos del enlace es `href` → `data-testid` para que el test de B1 los encuentre juntos; mantenerlo.

`src/OpenSource1.Blazor/Components/SeleccionFila.razor`:

```razor
@* SeleccionFila — radio visual de la vista Lista (Fix-Features B1): enlace a la misma lista con ?sel={id} (conserva filtros). *@
<a href="@Href" aria-label="@($"Seleccionar {Etiqueta}")" aria-current="@(Seleccionada ? "true" : null)" data-testid="seleccionar-fila"
   class="inline-flex size-5 items-center justify-center rounded-full border @(Seleccionada ? "border-brand-600" : "border-slate-300 dark:border-slate-600")">
    @if (Seleccionada)
    {
        <span class="size-2.5 rounded-full bg-brand-600" aria-hidden="true"></span>
    }
</a>

@code {
    public const string ClaseFilaSeleccionada = "bg-brand-50/70 dark:bg-brand-500/10";

    [Parameter, EditorRequired] public string Href { get; set; } = string.Empty;
    [Parameter] public bool Seleccionada { get; set; }
    [Parameter] public string Etiqueta { get; set; } = "registro";
}
```

`src/OpenSource1.Blazor/Components/TarjetaAcciones.razor`:

```razor
@* TarjetaAcciones — acciones de una tarjeta en la vista Tarjetas (Fix-Features B1): las 2 primeras rápidas permitidas
   visibles y el resto (rápidas sobrantes + Resto) en un menú ⋯ con <details data-menu>. *@
@using System.Security.Claims
@inject IAuthorizationService AuthorizationService

<div class="flex shrink-0 items-center gap-2" data-testid="tarjeta-acciones">
    @foreach (var accion in visibles)
    {
        <a href="@accion.UrlConId(Id)" title="@accion.Etiqueta" class="btn-press inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-2.5 py-1.5 text-xs font-semibold text-slate-600 hover:bg-slate-50 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-300">@accion.Etiqueta</a>
    }
    @if (enMenu.Count > 0)
    {
        <details class="relative" data-menu>
            <summary aria-label="Más acciones" class="btn-press inline-flex cursor-pointer list-none items-center rounded-lg border border-slate-200 bg-white px-2.5 py-1.5 text-xs font-bold text-slate-600 hover:bg-slate-50 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-300 [&::-webkit-details-marker]:hidden">⋯</summary>
            <div role="menu" class="absolute right-0 z-30 mt-1 min-w-52 rounded-xl border border-slate-200 bg-white py-1 shadow-lg dark:border-slate-700 dark:bg-slate-800">
                @foreach (var accion in enMenu)
                {
                    <a role="menuitem" href="@accion.UrlConId(Id)" class="block px-3 py-2 text-sm text-slate-700 hover:bg-slate-50 dark:text-slate-200 dark:hover:bg-slate-700/60">@accion.Etiqueta</a>
                }
            </div>
        </details>
    }
</div>

@code {
    [Parameter, EditorRequired] public string Id { get; set; } = string.Empty;
    [Parameter] public IReadOnlyList<AccionPagina> Rapidas { get; set; } = [];
    [Parameter] public IReadOnlyList<AccionPagina> Resto { get; set; } = [];
    [CascadingParameter] private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    private IReadOnlyList<AccionPagina> visibles = [];
    private IReadOnlyList<AccionPagina> enMenu = [];

    protected override async Task OnParametersSetAsync()
    {
        var usuario = AuthenticationStateTask is null ? new ClaimsPrincipal() : (await AuthenticationStateTask).User;
        var rapidas = await AccionesPermitidas.FiltrarAsync(AuthorizationService, usuario, Rapidas);
        var resto = await AccionesPermitidas.FiltrarAsync(AuthorizationService, usuario, Resto);
        visibles = rapidas.Take(2).ToList();
        enMenu = rapidas.Skip(2).Concat(resto).ToList();
    }
}
```

`src/OpenSource1.Blazor/wwwroot/app.menus.js`:

```javascript
// app.menus.js — cierra los menús <details data-menu> (PageToolbar, TarjetaAcciones) al pulsar fuera o con Esc
// (Fix-Features B1). Sin este script los menús siguen abriéndose y cerrándose con su propio <summary>.
(() => {
  'use strict';

  function cerrarTodos(excepto) {
    document.querySelectorAll('details[data-menu][open]').forEach((menu) => {
      if (menu !== excepto) menu.removeAttribute('open');
    });
  }

  document.addEventListener('click', (evento) => {
    const actual = evento.target instanceof Element ? evento.target.closest('details[data-menu]') : null;
    cerrarTodos(actual);
  });

  document.addEventListener('keydown', (evento) => {
    if (evento.key === 'Escape') cerrarTodos(null);
  });
})();
```

En `App.razor`, después de `<script src="@Assets["app.search.js"]"></script>`:

```razor
    <script src="@Assets["app.menus.js"]"></script>
```

- [ ] **Step 6: Ejecutar los tests**

Run: `node --check src/OpenSource1.Blazor/wwwroot/app.menus.js && dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~RetornoLocalTests|FullyQualifiedName~ComponentesPaginaTests"`
Expected: PASS. Si una aserción de orden de atributos falla, ajustar el **marcado** del componente al orden que el test documenta (es el contrato que usarán los lotes B2), no el test.

- [ ] **Step 7: Build sin avisos**

Run: `dotnet build test.slnx -warnaserror`
Expected: 0 avisos.

- [ ] **Step 8: Commit**

```bash
git add src/OpenSource1.Blazor/Components/AccionPagina.cs src/OpenSource1.Blazor/Components/RetornoLocal.cs src/OpenSource1.Blazor/Components/PageToolbar.razor src/OpenSource1.Blazor/Components/EntityFormPage.razor src/OpenSource1.Blazor/Components/FormularioAcciones.razor src/OpenSource1.Blazor/Components/SeleccionFila.razor src/OpenSource1.Blazor/Components/TarjetaAcciones.razor src/OpenSource1.Blazor/wwwroot/app.menus.js src/OpenSource1.Blazor/Components/App.razor tests/OpenSource1.SmokeTests/Blazor/RetornoLocalTests.cs tests/OpenSource1.SmokeTests/Blazor/ComponentesPaginaTests.cs
git commit -m "feat: barra de acciones, pagina-tarjeta de alta y edicion y seleccion de filas compartidas"
```

---

### Receta común de conversión de un listado (la aplican B2a–B2d)

Cada lote la aplica a sus páginas con los valores de su tabla. Es la misma transformación en todas; los pasos de cada
lote la repiten con los nombres concretos.

1. **Página editor nueva** `Components/Pages/<Entidad>Editor.razor` con dos `@page`: `"/<ruta>/nuevo"` y
   `"/<ruta>/{Id:guid}/editar"`, `[Authorize(Policy = CanConsult)]`, `[SupplyParameterFromQuery(Name = "returnUrl")]`.
   Conserva **los mismos `FormName`** del listado (`save-…` / `update-…`) y las mismas propiedades `SaveInput` /
   `UpdateInput` (los `*Fields.razor` usan esos prefijos). Muestra el formulario de alta si no hay `Id` y el de
   modificación si lo hay, dentro de `<EntityFormPage>` con `<FormularioAcciones CancelarHref="@Volver" LimpiarHref="@Nav.Uri" />`.
   El formulario que no se muestra (o que el permiso oculta) queda en el árbol como `EditForm` vacío con el mismo
   `FormName` y handler. Tras guardar: `Nav.NavigateTo(RetornoLocal.ConParametro(Volver, "ok", "created"|"updated"))`
   con `Volver = RetornoLocal.Validar(ReturnUrl, "/<ruta>")`. Sin `CanAdd` (alta) o `CanModify` (edición) muestra
   `"No tiene permiso para realizar esta acción."`.
2. **Mover** del listado al editor, sin cambiar su lógica: el bloque de carga de `EditId` de `OnInitializedAsync` (ahora
   con el `Id` de la ruta), `CreateAsync`, `UpdateAsync` (usa el `Id` de la ruta), `ToInput`, los cargadores de opciones
   que solo usan los formularios y los `RenderFragment` de campos que solo usan los formularios. Las clases de formulario
   (`<Entidad>Form`) **se quedan** en el listado como `public sealed class` anidada (los `*Fields.razor` las referencian
   como `<Listado>.<Entidad>Form`).
3. **Listado**: sustituir la cabecera por `<PageToolbar>` (migas `Grupo → Listado`, `NuevoHref`, `SeleccionId`,
   `EditarUrl`, `EliminarUrl`); borrar la sección `<!-- ── Formularios de operación ── -->` completa y los
   `[SupplyParameterFromForm]` de `save-`/`update-` con sus handlers; añadir
   `[SupplyParameterFromQuery(Name = "sel")] private Guid? Sel`, `SeleccionValida` (solo si el id está en la página
   cargada) y la columna `<SeleccionFila>` + resaltado de fila; `BuildListUrl` gana `Guid? sel = null` (por defecto
   conserva `Sel`) y pierde `editId`; los enlaces de edición por fila apuntan a la ruta nueva con `returnUrl`.
4. **Ruta antigua**: `?editId={id}` en el listado redirige (`Nav.NavigateTo(..., replace: true)`) a
   `/<ruta>/{id}/editar`; las páginas `…/new` pasan a ser redirecciones a `…/nuevo` (solo Clientes y Productos).
5. **Diálogo de eliminación**: sin cambios (sigue con `?deleteId=` y su `EditForm` `delete-…`).
6. **Tests**: un archivo por lote con, para cada entidad, alta y edición renderizadas, alta y edición enviadas
   (redirección con `ok=`), ruta antigua, sin formularios en línea, selección válida e inválida, y el POST forzado sin
   permiso (mensaje real de la API).

---

### Task B2a: Lote 1 — Maestros (Unidades de medida, Términos de pago, Categorías de producto, Almacenes)

**Grupo de paralelismo:** B2 — **en paralelo con B2b, B2c y B2d**. Archivos exclusivos de este lote: los de la lista
siguiente. **No tocar** `MainLayout.razor`, `NavMenu.razor`, `BarraBusqueda.razor`, `Navigation/*`, `Program.cs`,
`App.razor`, `_Imports.razor` ni los componentes de B1.

**Files:**
- Create: `src/OpenSource1.Blazor/Components/Pages/UnidadMedidaEditor.razor`
- Create: `src/OpenSource1.Blazor/Components/Pages/TerminoPagoEditor.razor`
- Create: `src/OpenSource1.Blazor/Components/Pages/CategoriaProductoEditor.razor`
- Create: `src/OpenSource1.Blazor/Components/Pages/AlmacenEditor.razor`
- Modify: `src/OpenSource1.Blazor/Components/Pages/UnidadesMedida.razor`
- Modify: `src/OpenSource1.Blazor/Components/Pages/TerminosPago.razor`
- Modify: `src/OpenSource1.Blazor/Components/Pages/CategoriasProducto.razor`
- Modify: `src/OpenSource1.Blazor/Components/Pages/Almacenes.razor`
- Test: `tests/OpenSource1.SmokeTests/Blazor/ConversionMaestrosTests.cs`

**Interfaces:**
- Consumes: B1 (`PageToolbar`, `EntityFormPage`, `FormularioAcciones`, `SeleccionFila`, `RetornoLocal`, `Miga`); clientes `IUnidadMedidaApiClient`, `ITerminoPagoApiClient`, `ICategoriaProductoApiClient`, `IAlmacenApiClient` (métodos `ListAsync`, `ListAllAsync` (categorías), `GetByIdAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`); formularios `UnidadesMedida.UnidadMedidaForm`, `TerminosPago.TerminoPagoForm`, `CategoriasProducto.CategoriaProductoForm`, `Almacenes.AlmacenForm`.
- Produces: rutas `/unidades-medida/nuevo`, `/unidades-medida/{id}/editar`, `/terminos-pago/nuevo`, `/terminos-pago/{id}/editar`, `/categorias-producto/nuevo`, `/categorias-producto/{id}/editar`, `/almacenes/nuevo`, `/almacenes/{id}/editar`; `?sel=` en los cuatro listados.

| Listado | Ruta | Grupo (migas) | FormName alta / edición | Formulario | Campos | Mensajes `ok` |
|---|---|---|---|---|---|---|
| `UnidadesMedida.razor` | `/unidades-medida` | Configuración `/modulos/configuracion` | `save-unidadmedida` / `update-unidadmedida` | `UnidadesMedida.UnidadMedidaForm` | `UnidadMedidaFields` | created/updated/deleted (ya existen) |
| `TerminosPago.razor` | `/terminos-pago` | Configuración | `save-terminopago` / `update-terminopago` | `TerminosPago.TerminoPagoForm` | `TerminoPagoFields` | ídem |
| `CategoriasProducto.razor` | `/categorias-producto` | Productos `/modulos/productos` | `save-categoriaproducto` / `update-categoriaproducto` | `CategoriasProducto.CategoriaProductoForm` | `CategoriaProductoFields` (+ `Opciones`, `ExcluirId`, `PadreActualNombre`) | ídem |
| `Almacenes.razor` | `/almacenes` | Inventario `/modulos/inventario` | `save-almacen` / `update-almacen` | `Almacenes.AlmacenForm` | `AlmacenFields` | ídem |

- [ ] **Step 1: Tests del lote (fallan)**

`tests/OpenSource1.SmokeTests/Blazor/ConversionMaestrosTests.cs`:

```csharp
extern alias BlazorApp;

using System.Net;
using BlazorApp::OpenSource1.Blazor.Services;
using Moq;
using OpenSource1.Application.Features.Almacenes.Dtos;
using OpenSource1.Application.Features.CategoriasProducto.Dtos;
using OpenSource1.Application.Features.TerminosPago.Dtos;
using OpenSource1.Application.Features.UnidadesMedida.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>Fix-Features B2a: alta y edición de maestros en página-tarjeta propia; listados con barra y selección.</summary>
public sealed class ConversionMaestrosTests
{
    private static readonly Guid IdUnidad = Guid.Parse("7a000000-0000-0000-0000-000000000001");

    [Theory]
    [InlineData("/unidades-medida/nuevo", "save-unidadmedida", "/unidades-medida")]
    [InlineData("/terminos-pago/nuevo", "save-terminopago", "/terminos-pago")]
    [InlineData("/categorias-producto/nuevo", "save-categoriaproducto", "/categorias-producto")]
    [InlineData("/almacenes/nuevo", "save-almacen", "/almacenes")]
    public async Task Nuevo_Admin_RenderizaFormularioEnTarjeta(string ruta, string formName, string lista)
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlAsync(app.Cliente(), ruta);

        Assert.Contains("data-testid=\"entity-form-page\"", html);
        Assert.Contains($"name=\"_handler\" value=\"{formName}\"", html);
        Assert.Contains("data-testid=\"guardar\"", html);
        Assert.Contains($"href=\"{lista}\" data-testid=\"cancelar\"", html);
    }

    [Theory]
    [InlineData("/unidades-medida?editId={0}", "/unidades-medida/{0}/editar")]
    [InlineData("/terminos-pago?editId={0}", "/terminos-pago/{0}/editar")]
    [InlineData("/categorias-producto?editId={0}", "/categorias-producto/{0}/editar")]
    [InlineData("/almacenes?editId={0}", "/almacenes/{0}/editar")]
    public async Task EditIdLegado_RedirigeALaRutaNueva(string origen, string destino)
    {
        using var app = Configurar(new BlazorSsrFactory());
        var id = Guid.NewGuid();

        var respuesta = await app.Cliente().GetAsync(string.Format(origen, id));

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal(string.Format(destino, id), FormulariosSsr.Destino(respuesta));
    }

    [Theory]
    [InlineData("/unidades-medida", "save-unidadmedida")]
    [InlineData("/terminos-pago", "save-terminopago")]
    [InlineData("/categorias-producto", "save-categoriaproducto")]
    [InlineData("/almacenes", "save-almacen")]
    public async Task Listado_SinFormulariosEnLinea_ConBarra(string ruta, string formNameAlta)
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlAsync(app.Cliente(), ruta);

        Assert.Contains("data-testid=\"page-toolbar\"", html);
        Assert.Contains($"href=\"{ruta}/nuevo?returnUrl=", html);
        Assert.DoesNotContain("id=\"agregar\"", html);
        Assert.DoesNotContain("id=\"modificar\"", html);
        Assert.DoesNotContain($"value=\"{formNameAlta}\"", html);
    }

    [Fact]
    public async Task Seleccion_ValidaHabilitaEditar_YLaQueNoEstaEnLaPaginaLaDeshabilita()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var valida = await HtmlAsync(app.Cliente(), $"/unidades-medida?sel={IdUnidad}");
        var invalida = await HtmlAsync(app.Cliente(), $"/unidades-medida?sel={Guid.NewGuid()}");

        Assert.Contains($"<a data-testid=\"accion-editar\" href=\"/unidades-medida/{IdUnidad}/editar?returnUrl=", valida);
        Assert.Contains("aria-current=\"true\"", valida);
        Assert.Contains("<span data-testid=\"accion-editar\" aria-disabled=\"true\"", invalida);
    }

    [Fact]
    public async Task Guardar_Alta_RedirigeAlReturnUrlConOk()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var api = app.Simular<IUnidadMedidaApiClient>();
        api.Setup(c => c.CreateAsync(It.Is<UnidadMedidaInput>(i => i.Codigo == "KG" && i.Decimales == 2), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UnidadMedidaOperationResult(true, "ok", Guid.NewGuid()));

        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), "/unidades-medida/nuevo?returnUrl=%2Funidades-medida%3Fcodigo%3DK",
            "save-unidadmedida", new Dictionary<string, string> { ["SaveInput.Codigo"] = " KG ", ["SaveInput.Nombre"] = "Kilogramo", ["SaveInput.Decimales"] = "2" });

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/unidades-medida?codigo=K&ok=created", FormulariosSsr.Destino(respuesta));
    }

    [Fact]
    public async Task Guardar_ConReturnUrlExterno_VuelveALaLista()
    {
        using var app = Configurar(new BlazorSsrFactory());
        app.Simular<IUnidadMedidaApiClient>()
            .Setup(c => c.CreateAsync(It.IsAny<UnidadMedidaInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UnidadMedidaOperationResult(true, "ok"));

        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), "/unidades-medida/nuevo?returnUrl=%2F%2Fevil.com",
            "save-unidadmedida", new Dictionary<string, string> { ["SaveInput.Codigo"] = "KG", ["SaveInput.Nombre"] = "Kilogramo", ["SaveInput.Decimales"] = "2" });

        Assert.Equal("/unidades-medida?ok=created", FormulariosSsr.Destino(respuesta));
    }

    [Fact]
    public async Task Editar_CargaLoGuardado_YGuardaConElIdDeLaRuta()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var api = app.Simular<IUnidadMedidaApiClient>();
        api.Setup(c => c.UpdateAsync(IdUnidad, It.Is<UnidadMedidaInput>(i => i.Codigo == "UNI"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UnidadMedidaOperationResult(true, "ok"));

        var html = await HtmlAsync(app.Cliente(), $"/unidades-medida/{IdUnidad}/editar");
        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), $"/unidades-medida/{IdUnidad}/editar", "update-unidadmedida",
            new Dictionary<string, string> { ["UpdateInput.Id"] = IdUnidad.ToString(), ["UpdateInput.Codigo"] = "UNI", ["UpdateInput.Nombre"] = "Unidad", ["UpdateInput.Decimales"] = "0" });

        Assert.Contains("value=\"UND\"", html);
        Assert.Equal("/unidades-medida?ok=updated", FormulariosSsr.Destino(respuesta));
        api.Verify(c => c.UpdateAsync(IdUnidad, It.IsAny<UnidadMedidaInput>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Editar_NoEncontrado_Mensaje()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlAsync(app.Cliente(), $"/unidades-medida/{Guid.NewGuid()}/editar");

        Assert.Contains("No se encontró la unidad de medida seleccionada", html);
        Assert.DoesNotContain("data-testid=\"guardar\"", html);
    }

    [Fact]
    public async Task Nuevo_SinCanAdd_Mensaje_YPostForzado_DevuelveMensajeDeLaApi()
    {
        using var app = Configurar(new BlazorSsrFactory());
        app.Simular<IUnidadMedidaApiClient>()
            .Setup(c => c.CreateAsync(It.IsAny<UnidadMedidaInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UnidadMedidaOperationResult(false, "No tiene permisos para agregar unidades de medida."));
        var supervisor = app.Cliente("Supervisor");

        var html = await HtmlAsync(supervisor, "/unidades-medida/nuevo");
        var forzado = await FormulariosSsr.EnviarAsync(supervisor, "/unidades-medida/nuevo", "save-unidadmedida",
            new Dictionary<string, string> { ["SaveInput.Codigo"] = "KG", ["SaveInput.Nombre"] = "Kilogramo", ["SaveInput.Decimales"] = "2" });

        Assert.Contains("No tiene permiso para realizar esta acción.", html);
        Assert.Contains("value=\"save-unidadmedida\"", html);
        Assert.DoesNotContain("data-testid=\"guardar\"", html);
        Assert.Equal(HttpStatusCode.OK, forzado.StatusCode);
        Assert.Contains("No tiene permisos para agregar unidades de medida.", await forzado.Content.ReadAsStringAsync());
    }

    private static BlazorSsrFactory Configurar(BlazorSsrFactory app)
    {
        app.Simular<IUnidadMedidaApiClient>().Setup(c => c.ListAsync(It.IsAny<UnidadMedidaSearchFilter?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<UnidadMedidaResponse>([new UnidadMedidaResponse { Id = IdUnidad, Codigo = "UND", Nombre = "Unidad" }], 1, 50, 1));
        app.Simular<IUnidadMedidaApiClient>().Setup(c => c.GetByIdAsync(IdUnidad, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UnidadMedidaResponse { Id = IdUnidad, Codigo = "UND", Nombre = "Unidad" });
        app.Simular<ITerminoPagoApiClient>().Setup(c => c.ListAsync(It.IsAny<TerminoPagoSearchFilter?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<TerminoPagoResponse>([], 1, 50, 0));
        app.Simular<ICategoriaProductoApiClient>().Setup(c => c.ListAsync(It.IsAny<CategoriaProductoSearchFilter?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<CategoriaProductoResponse>([], 1, 50, 0));
        app.Simular<ICategoriaProductoApiClient>().Setup(c => c.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CategoriaProductoResponse { Id = Guid.NewGuid(), Codigo = "GENERAL", Nombre = "General" }]);
        app.Simular<IAlmacenApiClient>().Setup(c => c.ListAsync(It.IsAny<AlmacenSearchFilter?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<AlmacenResponse>([], 1, 50, 0));
        return app;
    }

    private static async Task<string> HtmlAsync(HttpClient cliente, string url)
    {
        var respuesta = await cliente.GetAsync(url);
        var html = await respuesta.Content.ReadAsStringAsync();
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, $"GET {url}: {respuesta.StatusCode}\n{html}");
        return html;
    }
}
```

- [ ] **Step 2: Ejecutar y ver que fallan**

Run: `dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~ConversionMaestrosTests"`
Expected: FAIL (404 en `/…/nuevo`, formularios en línea presentes).

- [ ] **Step 3: `UnidadMedidaEditor.razor` (referencia completa de la receta)**

`src/OpenSource1.Blazor/Components/Pages/UnidadMedidaEditor.razor`:

```razor
@page "/unidades-medida/nuevo"
@page "/unidades-medida/{Id:guid}/editar"
@attribute [Authorize(Policy = ApplicationPolicies.CanConsult)]
@using Microsoft.AspNetCore.Authorization
@using OpenSource1.Application.Security
@using OpenSource1.Blazor.Services
@inject IUnidadMedidaApiClient UnidadMedidaApiClient
@inject IAuthorizationService AuthorizationService
@inject ILogger<UnidadMedidaEditor> Logger
@inject NavigationManager Nav

<EntityFormPage Titulo="@Titulo" Subtitulo="Catálogo de unidades de medida y sus decimales de redondeo." Migas="@Migas" Mensaje="@message" TipoMensaje="@messageType">
    @if (!Permitido)
    {
        <MessageBox Type="warning" Message="@SinPermiso" />
    }
    else if (EsEdicion && !encontrado)
    {
        <MessageBox Type="warning" Message="No se encontró la unidad de medida seleccionada. Puede haber sido eliminada por otro usuario." />
    }
    else if (EsEdicion)
    {
        <EditForm Model="UpdateInput" FormName="update-unidadmedida" OnValidSubmit="UpdateAsync" Enhance="true">
            <DataAnnotationsValidator />
            <input type="hidden" name="UpdateInput.Id" value="@UpdateInput!.Id" />
            <UnidadMedidaFields Model="UpdateInput!" Prefix="UpdateInput" />
            <FormularioAcciones CancelarHref="@Volver" LimpiarHref="@Nav.Uri" />
        </EditForm>
    }
    else
    {
        <EditForm Model="SaveInput" FormName="save-unidadmedida" OnValidSubmit="CreateAsync" Enhance="true">
            <DataAnnotationsValidator />
            <UnidadMedidaFields Model="SaveInput!" Prefix="SaveInput" />
            <FormularioAcciones CancelarHref="@Volver" LimpiarHref="@Nav.Uri" />
        </EditForm>
    }
</EntityFormPage>

@* Formularios siempre en el árbol: sin permiso o sin registro, un POST obsoleto o forzado llega al handler y recibe el
   mensaje real de la API (403/404), no el 400 genérico de Blazor. *@
@if (!MuestraAlta)
{
    <EditForm Model="SaveInput" FormName="save-unidadmedida" OnValidSubmit="CreateAsync"></EditForm>
}
@if (!MuestraEdicion)
{
    <EditForm Model="UpdateInput" FormName="update-unidadmedida" OnValidSubmit="UpdateAsync"></EditForm>
}

@code {
    private const string SinPermiso = "No tiene permiso para realizar esta acción.";

    [Parameter] public Guid? Id { get; set; }
    [SupplyParameterFromQuery(Name = "returnUrl")] private string? ReturnUrl { get; set; }
    [SupplyParameterFromForm(FormName = "save-unidadmedida")] private UnidadesMedida.UnidadMedidaForm? SaveInput { get; set; }
    [SupplyParameterFromForm(FormName = "update-unidadmedida")] private UnidadesMedida.UnidadMedidaForm? UpdateInput { get; set; }
    [CascadingParameter] private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    private bool canAdd;
    private bool canModify;
    private bool encontrado;
    private string? message;
    private string messageType = "info";

    private bool EsEdicion => Id.HasValue;
    private bool Permitido => EsEdicion ? canModify : canAdd;
    private bool MuestraAlta => !EsEdicion && canAdd;
    private bool MuestraEdicion => EsEdicion && canModify && encontrado;
    private string Volver => RetornoLocal.Validar(ReturnUrl, "/unidades-medida");
    private string Titulo => EsEdicion ? "Modificar unidad de medida" : "Nueva unidad de medida";
    private IReadOnlyList<Miga> Migas => [new("Configuración", "/modulos/configuracion"), new("Unidades de medida", Volver), new(EsEdicion ? "Modificar" : "Nueva")];

    protected override async Task OnInitializedAsync()
    {
        SaveInput ??= new();
        UpdateInput ??= new();
        if (AuthenticationStateTask is not null)
        {
            var user = (await AuthenticationStateTask).User;
            canAdd = (await AuthorizationService.AuthorizeAsync(user, ApplicationPolicies.CanAdd)).Succeeded;
            canModify = (await AuthorizationService.AuthorizeAsync(user, ApplicationPolicies.CanModify)).Succeeded;
        }

        if (Id is { } id && canModify)
        {
            try
            {
                var unidad = await UnidadMedidaApiClient.GetByIdAsync(id);
                encontrado = unidad is not null;
                // Solo se rellena con lo guardado cuando NO llegó un formulario de modificación (Id oculto vacío).
                if (unidad is not null && UpdateInput.Id == Guid.Empty)
                {
                    UpdateInput.Id = unidad.Id;
                    UpdateInput.Codigo = unidad.Codigo;
                    UpdateInput.Nombre = unidad.Nombre;
                    UpdateInput.Decimales = unidad.Decimales;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Could not load unidad de medida {Id} from API.", id);
                Show("danger", "No fue posible cargar la unidad de medida a modificar.");
            }
        }
    }

    private async Task CreateAsync() => await ExecuteAsync(() => UnidadMedidaApiClient.CreateAsync(ToInput(SaveInput!)), "created");

    private async Task UpdateAsync()
    {
        if (Id is not { } id)
        {
            Show("warning", "Seleccione primero un registro a modificar desde la tabla.");
            return;
        }

        await ExecuteAsync(() => UnidadMedidaApiClient.UpdateAsync(id, ToInput(UpdateInput!)), "updated");
    }

    private async Task ExecuteAsync(Func<Task<UnidadMedidaOperationResult>> operation, string successCode)
    {
        try
        {
            var result = await operation();
            if (result.Succeeded)
            {
                Nav.NavigateTo(RetornoLocal.ConParametro(Volver, "ok", successCode));
            }
            else
            {
                Show("warning", result.Message);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not complete unidad de medida operation.");
            Show("danger", "No fue posible completar la operación.");
        }
    }

    private void Show(string type, string text) { messageType = type; message = text; }

    private static UnidadMedidaInput ToInput(UnidadesMedida.UnidadMedidaForm form) => new(form.Codigo.Trim(), form.Nombre.Trim(), form.Decimales);
}
```

- [ ] **Step 4: Adaptar `UnidadesMedida.razor`**

1. Sustituir el encabezado (el `<div class="mb-6">` con "Mantenimiento" / `<h1>Unidades de Medida</h1>` / párrafo) por:

```razor
<PageToolbar Titulo="Unidades de Medida" Subtitulo="Catálogo de unidades de medida y sus decimales de redondeo."
             Migas="@Migas"
             NuevoHref="@RetornoLocal.ConRetorno("/unidades-medida/nuevo", BuildListUrl())"
             SeleccionId="@SeleccionValida?.ToString()"
             EditarUrl="@(id => EditarUrl(Guid.Parse(id)))"
             EliminarUrl="@(id => BuildListUrl(deleteId: Guid.Parse(id)))" />
```

2. Tabla: añadir como primera columna `<th class="px-4 py-3"><span class="sr-only">Seleccionar</span></th>`; en cada fila
   `class="hover:bg-slate-50 transition-colors @(unidad.Id == SeleccionValida ? SeleccionFila.ClaseFilaSeleccionada : "")"`
   y primera celda `<td class="px-4 py-3"><SeleccionFila Href="@BuildListUrl(sel: unidad.Id)" Seleccionada="@(unidad.Id == SeleccionValida)" Etiqueta="@unidad.Codigo" /></td>`;
   el `colspan="5"` de la fila vacía pasa a `colspan="6"`; el enlace "Editar unidad de medida" de la fila pasa a
   `href="@EditarUrl(unidad.Id)"`.
3. Borrar la sección completa desde `<!-- ── Formularios de operación ── -->` hasta el `</div>` que cierra su rejilla
   (antes de `@if (DeleteId.HasValue && canDelete)`).
4. En `@code`: borrar `SaveInput`, `UpdateInput` (con sus `[SupplyParameterFromForm]`), `CreateAsync`, `UpdateAsync`,
   `ToInput`, `canAdd`/`canModify` si ya no se usan y el bloque `if (EditId.HasValue && UpdateInput.Id == Guid.Empty) { … }`
   de `OnInitializedAsync`; `UnidadMedidaForm` y `DeleteForm` se quedan. Añadir:

```csharp
    [SupplyParameterFromQuery(Name = "sel")]
    private Guid? Sel { get; set; }

    // Solo cuenta la selección si el registro está en la página cargada (Review Focus 4).
    private Guid? SeleccionValida => Sel is { } s && unidades.Any(u => u.Id == s) ? s : null;
    private static readonly IReadOnlyList<Miga> Migas = [new("Configuración", "/modulos/configuracion"), new("Unidades de medida")];
    private string EditarUrl(Guid id) => RetornoLocal.ConRetorno($"/unidades-medida/{id}/editar", BuildListUrl());
```

   y como **primera** instrucción de `OnInitializedAsync` (antes de `DeleteInput ??= new();`):

```csharp
        if (EditId is { } editarLegado)
        {
            // Ruta antigua ?editId= (Fix-Features B2): la edición vive ahora en su página-tarjeta.
            Nav.NavigateTo($"/unidades-medida/{editarLegado}/editar", replace: true);
            return;
        }
```

5. Reemplazar `BuildListUrl` por:

```csharp
    private string BuildListUrl(int? pagina = null, Guid? deleteId = null, Guid? sel = null)
    {
        var parameters = new List<string>();
        AddParameter(parameters, "codigo", SearchCodigo);
        AddParameter(parameters, "nombre", SearchNombre);
        var paginaEfectiva = pagina ?? Pagina;
        if (paginaEfectiva > 1) parameters.Add($"pagina={paginaEfectiva}");
        if ((sel ?? Sel) is { } seleccion) parameters.Add($"sel={seleccion}");
        if (deleteId.HasValue) parameters.Add($"deleteId={deleteId.Value}");
        return parameters.Count == 0 ? "/unidades-medida" : $"/unidades-medida?{string.Join("&", parameters)}";
    }
```

(las llamadas existentes `BuildListUrl(editId: …)` desaparecen con el paso 2; `BuildListUrl(deleteId: …)` y
`BuildListUrl(pagina: …)` siguen compilando).

- [ ] **Step 5: `TerminoPagoEditor.razor` y adaptación de `TerminosPago.razor`**

`src/OpenSource1.Blazor/Components/Pages/TerminoPagoEditor.razor`: mismo archivo que `UnidadMedidaEditor.razor` del
Step 3 con estas sustituciones exactas (todo lo demás idéntico):

| En `UnidadMedidaEditor.razor` | En `TerminoPagoEditor.razor` |
|---|---|
| `@page "/unidades-medida/nuevo"` / `@page "/unidades-medida/{Id:guid}/editar"` | `@page "/terminos-pago/nuevo"` / `@page "/terminos-pago/{Id:guid}/editar"` |
| `IUnidadMedidaApiClient UnidadMedidaApiClient` | `ITerminoPagoApiClient TerminoPagoApiClient` |
| `ILogger<UnidadMedidaEditor>` | `ILogger<TerminoPagoEditor>` |
| `Subtitulo="Catálogo de unidades de medida y sus decimales de redondeo."` | `Subtitulo="Condiciones de vencimiento y descuento por pronto pago."` |
| `save-unidadmedida` / `update-unidadmedida` | `save-terminopago` / `update-terminopago` |
| `UnidadesMedida.UnidadMedidaForm` | `TerminosPago.TerminoPagoForm` |
| `<UnidadMedidaFields …>` | `<TerminoPagoFields …>` (mismos `Model`/`Prefix`) |
| `"/unidades-medida"` (Volver) | `"/terminos-pago"` |
| Títulos "Modificar unidad de medida" / "Nueva unidad de medida" | "Modificar término de pago" / "Nuevo término de pago" |
| Migas `("Unidades de medida", Volver)`, `"Nueva"` | `("Términos de pago", Volver)`, `"Nuevo"` |
| "No se encontró la unidad de medida seleccionada. Puede haber sido eliminada por otro usuario." | "No se encontró el término de pago seleccionado. Puede haber sido eliminado por otro usuario." |
| Bloque de carga: `var unidad = await UnidadMedidaApiClient.GetByIdAsync(id);` + asignaciones | `var termino = await TerminoPagoApiClient.GetByIdAsync(id);` + `UpdateInput.Id = termino.Id; UpdateInput.Codigo = termino.Codigo; UpdateInput.Descripcion = termino.Descripcion; UpdateInput.DiasVencimiento = termino.DiasVencimiento; UpdateInput.DiasDescuento = termino.DiasDescuento; UpdateInput.PorcentajeDescuento = termino.PorcentajeDescuento;` |
| "No fue posible cargar la unidad de medida a modificar." | "No fue posible cargar el término de pago a modificar." |
| `Func<Task<UnidadMedidaOperationResult>>` | `Func<Task<TerminoPagoOperationResult>>` |
| `ToInput(...) => new(form.Codigo.Trim(), form.Nombre.Trim(), form.Decimales)` con `UnidadMedidaInput` | `private static TerminoPagoInput ToInput(TerminosPago.TerminoPagoForm form) => new(form.Codigo.Trim(), form.Descripcion.Trim(), form.DiasVencimiento, form.DiasDescuento, form.PorcentajeDescuento);` |
| Logs "unidad de medida" | "termino de pago" |

`TerminosPago.razor`: aplicar los pasos 1–5 del Step 4 con `Titulo="Términos de Pago"`,
`Subtitulo="Condiciones de vencimiento y descuento por pronto pago."`, migas
`[new("Configuración", "/modulos/configuracion"), new("Términos de pago")]`, lista `terminos` (variable de iteración `termino`), filtros `codigo`/`descripcion` (`SearchCodigo`, `SearchDescripcion`)
en `BuildListUrl`, rutas `/terminos-pago…` y etiqueta de selección `@termino.Codigo`.

- [ ] **Step 6: `CategoriaProductoEditor.razor` y adaptación de `CategoriasProducto.razor`**

`src/OpenSource1.Blazor/Components/Pages/CategoriaProductoEditor.razor`:

```razor
@page "/categorias-producto/nuevo"
@page "/categorias-producto/{Id:guid}/editar"
@attribute [Authorize(Policy = ApplicationPolicies.CanConsult)]
@using Microsoft.AspNetCore.Authorization
@using OpenSource1.Application.Features.CategoriasProducto.Dtos
@using OpenSource1.Application.Security
@using OpenSource1.Blazor.Services
@inject ICategoriaProductoApiClient CategoriaProductoApiClient
@inject IAuthorizationService AuthorizationService
@inject ILogger<CategoriaProductoEditor> Logger
@inject NavigationManager Nav

<EntityFormPage Titulo="@Titulo" Subtitulo="Jerarquía de categorías de producto." Migas="@Migas" Mensaje="@message" TipoMensaje="@messageType">
    @if (!Permitido)
    {
        <MessageBox Type="warning" Message="@SinPermiso" />
    }
    else if (EsEdicion && !encontrado)
    {
        <MessageBox Type="warning" Message="No se encontró la categoría seleccionada. Puede haber sido eliminada por otro usuario." />
    }
    else if (EsEdicion)
    {
        <EditForm Model="UpdateInput" FormName="update-categoriaproducto" OnValidSubmit="UpdateAsync" Enhance="true">
            <DataAnnotationsValidator />
            <input type="hidden" name="UpdateInput.Id" value="@UpdateInput!.Id" />
            <CategoriaProductoFields Model="UpdateInput!" Prefix="UpdateInput" Opciones="opcionesPadre" ExcluirId="UpdateInput.Id" PadreActualNombre="@padreActualNombre" />
            <FormularioAcciones CancelarHref="@Volver" LimpiarHref="@Nav.Uri" Deshabilitado="@opcionesPadreNoDisponibles" />
        </EditForm>
    }
    else
    {
        <EditForm Model="SaveInput" FormName="save-categoriaproducto" OnValidSubmit="CreateAsync" Enhance="true">
            <DataAnnotationsValidator />
            <CategoriaProductoFields Model="SaveInput!" Prefix="SaveInput" Opciones="opcionesPadre" />
            <FormularioAcciones CancelarHref="@Volver" LimpiarHref="@Nav.Uri" />
        </EditForm>
    }
</EntityFormPage>

@if (!MuestraAlta)
{
    <EditForm Model="SaveInput" FormName="save-categoriaproducto" OnValidSubmit="CreateAsync"></EditForm>
}
@if (!MuestraEdicion)
{
    <EditForm Model="UpdateInput" FormName="update-categoriaproducto" OnValidSubmit="UpdateAsync"></EditForm>
}

@code {
    private const string SinPermiso = "No tiene permiso para realizar esta acción.";
    private const string MensajeOpcionesPadre = "No fue posible cargar las categorías disponibles como padre. Por seguridad no se puede modificar una categoría hasta que carguen; recargue la página.";

    [Parameter] public Guid? Id { get; set; }
    [SupplyParameterFromQuery(Name = "returnUrl")] private string? ReturnUrl { get; set; }
    [SupplyParameterFromForm(FormName = "save-categoriaproducto")] private CategoriasProducto.CategoriaProductoForm? SaveInput { get; set; }
    [SupplyParameterFromForm(FormName = "update-categoriaproducto")] private CategoriasProducto.CategoriaProductoForm? UpdateInput { get; set; }
    [CascadingParameter] private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    private IReadOnlyList<CategoriaProductoResponse> opcionesPadre = [];
    private bool opcionesPadreNoDisponibles;
    private string? padreActualNombre;
    private bool canAdd;
    private bool canModify;
    private bool encontrado;
    private string? message;
    private string messageType = "info";

    private bool EsEdicion => Id.HasValue;
    private bool Permitido => EsEdicion ? canModify : canAdd;
    private bool MuestraAlta => !EsEdicion && canAdd;
    private bool MuestraEdicion => EsEdicion && canModify && encontrado;
    private string Volver => RetornoLocal.Validar(ReturnUrl, "/categorias-producto");
    private string Titulo => EsEdicion ? "Modificar categoría" : "Nueva categoría";
    private IReadOnlyList<Miga> Migas => [new("Productos", "/modulos/productos"), new("Categorías de producto", Volver), new(EsEdicion ? "Modificar" : "Nueva")];

    protected override async Task OnInitializedAsync()
    {
        SaveInput ??= new();
        UpdateInput ??= new();
        if (AuthenticationStateTask is not null)
        {
            var user = (await AuthenticationStateTask).User;
            canAdd = (await AuthorizationService.AuthorizeAsync(user, ApplicationPolicies.CanAdd)).Succeeded;
            canModify = (await AuthorizationService.AuthorizeAsync(user, ApplicationPolicies.CanModify)).Succeeded;
        }

        if (Permitido)
        {
            await LoadOpcionesPadreAsync();
        }

        if (Id is { } id && canModify)
        {
            try
            {
                var categoria = await CategoriaProductoApiClient.GetByIdAsync(id);
                encontrado = categoria is not null;
                if (categoria is not null && UpdateInput.Id == Guid.Empty)
                {
                    UpdateInput.Id = categoria.Id;
                    UpdateInput.Codigo = categoria.Codigo;
                    UpdateInput.Nombre = categoria.Nombre;
                    UpdateInput.CategoriaPadreId = categoria.CategoriaPadreId;
                }

                padreActualNombre = categoria?.CategoriaPadreNombre;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Could not load categoría {Id} from API.", id);
                Show("danger", "No fue posible cargar la categoría a modificar.");
            }
        }
    }

    private async Task LoadOpcionesPadreAsync()
    {
        try
        {
            opcionesPadre = await CategoriaProductoApiClient.ListAllAsync();
            // El catálogo siempre tiene al menos GENERAL: una lista vacía es una respuesta anómala, no un dato.
            opcionesPadreNoDisponibles = opcionesPadre.Count == 0;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not load parent category options from API.");
            opcionesPadreNoDisponibles = true;
        }

        if (opcionesPadreNoDisponibles && EsEdicion)
        {
            Show("danger", MensajeOpcionesPadre);
        }
    }

    private async Task CreateAsync() => await ExecuteAsync(() => CategoriaProductoApiClient.CreateAsync(ToInput(SaveInput!)), "created");

    private async Task UpdateAsync()
    {
        if (Id is not { } id)
        {
            Show("warning", "Seleccione primero un registro a modificar desde la tabla.");
            return;
        }

        // Defensa en profundidad: aunque el botón no se renderice, no se envía un PUT sin opciones de padre fiables.
        if (opcionesPadreNoDisponibles)
        {
            Show("danger", MensajeOpcionesPadre);
            return;
        }

        await ExecuteAsync(() => CategoriaProductoApiClient.UpdateAsync(id, ToInput(UpdateInput!)), "updated");
    }

    private async Task ExecuteAsync(Func<Task<CategoriaProductoOperationResult>> operation, string successCode)
    {
        try
        {
            var result = await operation();
            if (result.Succeeded)
            {
                Nav.NavigateTo(RetornoLocal.ConParametro(Volver, "ok", successCode));
            }
            else
            {
                Show("warning", result.Message);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not complete categoría operation.");
            Show("danger", "No fue posible completar la operación.");
        }
    }

    private void Show(string type, string text) { messageType = type; message = text; }

    private static CategoriaProductoInput ToInput(CategoriasProducto.CategoriaProductoForm form) =>
        new(form.Codigo.Trim(), form.Nombre.Trim(), form.CategoriaPadreId);
}
```

`CategoriasProducto.razor`: pasos 1–5 del Step 4 con `Titulo="Categorías de Producto"`, migas
`[new("Productos", "/modulos/productos"), new("Categorías de producto")]`, lista `categorias`, filtros
`codigo`/`nombre`, rutas `/categorias-producto…`, etiqueta `@categoria.Codigo`. Además borrar del listado
`opcionesPadre`, `opcionesPadreNoDisponibles`, `MensajeOpcionesPadre`, `padreActualNombre`, `LoadOpcionesPadreAsync` y su
llamada (ya solo los usa el editor).

- [ ] **Step 7: `AlmacenEditor.razor` y adaptación de `Almacenes.razor`**

`src/OpenSource1.Blazor/Components/Pages/AlmacenEditor.razor`: el archivo de `UnidadMedidaEditor.razor` (Step 3) con
estas sustituciones exactas:

| En `UnidadMedidaEditor.razor` | En `AlmacenEditor.razor` |
|---|---|
| rutas `/unidades-medida/…` | `@page "/almacenes/nuevo"` / `@page "/almacenes/{Id:guid}/editar"` |
| `IUnidadMedidaApiClient UnidadMedidaApiClient` | `IAlmacenApiClient AlmacenApiClient` |
| `ILogger<UnidadMedidaEditor>` | `ILogger<AlmacenEditor>` |
| Subtítulo | `"Almacenes y almacén predeterminado."` |
| `save-unidadmedida` / `update-unidadmedida` | `save-almacen` / `update-almacen` |
| `UnidadesMedida.UnidadMedidaForm` | `Almacenes.AlmacenForm` |
| `<UnidadMedidaFields …>` | `<AlmacenFields …>` |
| Volver `"/unidades-medida"` | `"/almacenes"` |
| Títulos | "Modificar almacén" / "Nuevo almacén"; migas `[new("Inventario", "/modulos/inventario"), new("Almacenes", Volver), new(EsEdicion ? "Modificar" : "Nuevo")]` |
| Mensaje no encontrado | "No se encontró el almacén seleccionado. Puede haber sido eliminado por otro usuario." |
| Bloque de carga | `var almacen = await AlmacenApiClient.GetByIdAsync(id);` + `UpdateInput.Id = almacen.Id; UpdateInput.Codigo = almacen.Codigo; UpdateInput.Nombre = almacen.Nombre; UpdateInput.DireccionLinea1 = almacen.DireccionLinea1; UpdateInput.DireccionLinea2 = almacen.DireccionLinea2; UpdateInput.Ciudad = almacen.Ciudad; UpdateInput.PaisCodigo = almacen.PaisCodigo; UpdateInput.Bloqueado = almacen.Bloqueado; UpdateInput.EsPredeterminado = almacen.EsPredeterminado;` |
| `Func<Task<UnidadMedidaOperationResult>>` | `Func<Task<AlmacenOperationResult>>` |
| rama `else Show("warning", result.Message);` | `else { errores = result.Errors ?? []; Show("warning", result.Message); }` con `private IReadOnlyList<string> errores = [];` y `Errores="@errores"` en `<EntityFormPage>` |
| `ToInput` | el `ToInput(AlmacenForm)` de `Almacenes.razor` **con su comentario "OJO: …"** (movido literal, con tipo `Almacenes.AlmacenForm`) |

`Almacenes.razor`: pasos 1–5 del Step 4 con `Titulo="Almacenes"`, migas
`[new("Inventario", "/modulos/inventario"), new("Almacenes")]`, lista `almacenes`, filtros `codigo`/`nombre`/`bloqueado`
(los tres que ya añade su `BuildListUrl`), rutas `/almacenes…`, etiqueta `@almacen.Codigo`; el `errores` del listado se
conserva solo si lo sigue usando el diálogo de eliminación.

- [ ] **Step 8: Ejecutar los tests del lote y del host**

Run: `dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~ConversionMaestrosTests|FullyQualifiedName~ComponentesPaginaTests|FullyQualifiedName~RegistroModulosTests"`
Expected: PASS (el registro sigue verde: las rutas `/nuevo` y `/{id}/editar` no son listados).

- [ ] **Step 9: Build sin avisos**

Run: `dotnet build test.slnx -warnaserror`
Expected: 0 avisos (sin variables sin uso en los listados).

- [ ] **Step 10: Commit**

```bash
git add src/OpenSource1.Blazor/Components/Pages/UnidadMedidaEditor.razor src/OpenSource1.Blazor/Components/Pages/TerminoPagoEditor.razor src/OpenSource1.Blazor/Components/Pages/CategoriaProductoEditor.razor src/OpenSource1.Blazor/Components/Pages/AlmacenEditor.razor src/OpenSource1.Blazor/Components/Pages/UnidadesMedida.razor src/OpenSource1.Blazor/Components/Pages/TerminosPago.razor src/OpenSource1.Blazor/Components/Pages/CategoriasProducto.razor src/OpenSource1.Blazor/Components/Pages/Almacenes.razor tests/OpenSource1.SmokeTests/Blazor/ConversionMaestrosTests.cs
git commit -m "feat: alta y edicion de maestros en pagina propia con barra de acciones y seleccion"
```

---
### Task B2b: Lote 2 — Contabilidad (Plan de cuentas, Grupos contables, Grupos de cliente contable, Setups contables)

**Grupo de paralelismo:** B2 — **en paralelo con B2a, B2c y B2d**. Mismas prohibiciones de archivos compartidos que B2a
(no tocar `MainLayout.razor`, `NavMenu.razor`, `BarraBusqueda.razor`, `Navigation/*`, `Program.cs`, `App.razor`,
`_Imports.razor` ni los componentes de B1).

**Files:**
- Create: `src/OpenSource1.Blazor/Components/Pages/CuentaContableEditor.razor`
- Create: `src/OpenSource1.Blazor/Components/Pages/GrupoContableEditor.razor`
- Create: `src/OpenSource1.Blazor/Components/Pages/GrupoClienteContableEditor.razor`
- Modify: `src/OpenSource1.Blazor/Components/Pages/CuentasContables.razor`
- Modify: `src/OpenSource1.Blazor/Components/Pages/GruposContables.razor`
- Modify: `src/OpenSource1.Blazor/Components/Pages/GruposClienteContable.razor`
- Modify: `src/OpenSource1.Blazor/Components/Pages/SetupsContables.razor` (variante "mismo componente", ver Step 6)
- Test: `tests/OpenSource1.SmokeTests/Blazor/ConversionContabilidadTests.cs`

**Interfaces:**
- Consumes: B1; `ICuentaContableApiClient` (`UpdateAsync(Guid id, CuentaContableInput input, long xmin, …)`), `IGrupoContableApiClient` (`GetByIdAsync(TipoGrupoContable tipo, Guid id, …)`, `CreateAsync(tipo, input, …)`, `UpdateAsync(tipo, id, input, xmin, …)`), `IGrupoClienteContableApiClient`, `ISetupContableApiClient`; `TiposGrupoContable.DesdeRuta/De`, `TiposSetupContable.DesdeRuta`.
- Produces: `/cuentas-contables/nuevo|{id}/editar`, `/grupos-contables/nuevo?tipo=…|{id}/editar?tipo=…`, `/grupos-cliente-contable/nuevo|{id}/editar`, `/setups-contables/nuevo?tipo=…|{id}/editar?tipo=…`; `?sel=` en los cuatro listados.

| Listado | Ruta | Migas | FormName alta / edición | Formulario | Notas |
|---|---|---|---|---|---|
| `CuentasContables.razor` | `/cuentas-contables` | Contabilidad `/modulos/contabilidad` | `save-cuenta-contable` / `update-cuenta-contable` | `CuentasContables.CuentaContableForm` | `Xmin` oculto; `errores` |
| `GruposContables.razor` | `/grupos-contables` | Configuración | `save-grupo-contable` / `update-grupo-contable` | `GrupoContableForm` (clase de nivel superior en `Components/GrupoContableFormularios.cs`) | query `tipo` obligatoria en todas las URL |
| `GruposClienteContable.razor` | `/grupos-cliente-contable` | Configuración | `save-grupo-cliente-contable` / `update-grupo-cliente-contable` | `GrupoClienteContableForm` (`Components/GrupoContableFormularios.cs`) | mueve `CargarCuentasAsync`, `CamposCuentas`, `OpcionVigente`, `Cuenta` |
| `SetupsContables.razor` | `/setups-contables` | Configuración | `save-setup-contable` / `update-setup-contable` | `SetupContableForm` (`Components/SetupContableFormulario.cs`) | variante mismo componente |

- [ ] **Step 1: Tests del lote (fallan)**

`tests/OpenSource1.SmokeTests/Blazor/ConversionContabilidadTests.cs`:

```csharp
extern alias BlazorApp;

using System.Net;
using BlazorApp::OpenSource1.Blazor.Services;
using Moq;
using OpenSource1.Application.Features.CuentasContables.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>Fix-Features B2b: maestros contables con alta y edición en página-tarjeta; el tipo (grupos/setups) viaja en la query.</summary>
public sealed class ConversionContabilidadTests
{
    private static readonly Guid IdCuenta = Guid.Parse("7b000000-0000-0000-0000-000000000001");

    [Theory]
    [InlineData("/cuentas-contables/nuevo", "save-cuenta-contable")]
    [InlineData("/grupos-contables/nuevo?tipo=producto", "save-grupo-contable")]
    [InlineData("/grupos-cliente-contable/nuevo", "save-grupo-cliente-contable")]
    [InlineData("/setups-contables/nuevo?tipo=iva", "save-setup-contable")]
    public async Task Nuevo_RenderizaFormularioEnTarjeta(string ruta, string formName)
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlAsync(app.Cliente(), ruta);

        Assert.Contains("data-testid=\"entity-form-page\"", html);
        Assert.Contains($"name=\"_handler\" value=\"{formName}\"", html);
    }

    [Theory]
    [InlineData("/cuentas-contables?editId={0}", "/cuentas-contables/{0}/editar")]
    [InlineData("/grupos-contables?tipo=producto&editId={0}", "/grupos-contables/{0}/editar?tipo=producto")]
    [InlineData("/grupos-cliente-contable?editId={0}", "/grupos-cliente-contable/{0}/editar")]
    [InlineData("/setups-contables?tipo=iva&editId={0}", "/setups-contables/{0}/editar?tipo=iva")]
    public async Task EditIdLegado_RedirigeALaRutaNueva(string origen, string destino)
    {
        using var app = Configurar(new BlazorSsrFactory());
        var id = Guid.NewGuid();

        var respuesta = await app.Cliente().GetAsync(string.Format(origen, id));

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal(string.Format(destino, id), FormulariosSsr.Destino(respuesta));
    }

    [Theory]
    [InlineData("/cuentas-contables", "save-cuenta-contable")]
    [InlineData("/grupos-contables?tipo=producto", "save-grupo-contable")]
    [InlineData("/grupos-cliente-contable", "save-grupo-cliente-contable")]
    [InlineData("/setups-contables?tipo=iva", "save-setup-contable")]
    public async Task Listado_SinFormulariosEnLinea_ConBarra(string ruta, string formNameAlta)
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlAsync(app.Cliente(), ruta);

        Assert.Contains("data-testid=\"page-toolbar\"", html);
        Assert.Contains("data-testid=\"accion-nuevo\"", html);
        Assert.DoesNotContain("id=\"agregar\"", html);
        Assert.DoesNotContain($"value=\"{formNameAlta}\"", html);
    }

    [Fact]
    public async Task CuentaContable_Editar_EnviaElXminLeido()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var api = app.Simular<ICuentaContableApiClient>();
        api.Setup(c => c.UpdateAsync(IdCuenta, It.IsAny<CuentaContableInput>(), 41, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CuentaContableOperationResult(true, "ok"));

        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), $"/cuentas-contables/{IdCuenta}/editar", "update-cuenta-contable",
            new Dictionary<string, string>
            {
                ["UpdateInput.Id"] = IdCuenta.ToString(), ["UpdateInput.Xmin"] = "41",
                ["UpdateInput.Numero"] = "1101", ["UpdateInput.Nombre"] = "Caja general",
            });

        Assert.Equal("/cuentas-contables?ok=updated", FormulariosSsr.Destino(respuesta));
        api.Verify(c => c.UpdateAsync(IdCuenta, It.Is<CuentaContableInput>(i => i.Nombre == "Caja general"), 41, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GrupoContable_GuardarVuelveAlTipo()
    {
        using var app = Configurar(new BlazorSsrFactory());
        app.Simular<IGrupoContableApiClient>()
            .Setup(c => c.CreateAsync(It.IsAny<OpenSource1.Core.Enums.TipoGrupoContable>(), It.IsAny<GrupoContableInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GrupoOperationResult(true, "ok"));

        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), "/grupos-contables/nuevo?tipo=producto&returnUrl=%2Fgrupos-contables%3Ftipo%3Dproducto",
            "save-grupo-contable", new Dictionary<string, string> { ["SaveInput.Codigo"] = "SERV", ["SaveInput.Descripcion"] = "Servicios" });

        Assert.Equal("/grupos-contables?tipo=producto&ok=created", FormulariosSsr.Destino(respuesta));
    }

    private static BlazorSsrFactory Configurar(BlazorSsrFactory app)
    {
        app.Simular<ICuentaContableApiClient>().Setup(c => c.ListAsync(It.IsAny<CuentaContableSearchFilter?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<CuentaContableResponse>([], 1, 50, 0));
        app.Simular<ICuentaContableApiClient>().Setup(c => c.GetByIdAsync(IdCuenta, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CuentaContableResponse { Id = IdCuenta, Numero = "1101", Nombre = "Caja", Xmin = 41 });
        app.Simular<IGrupoContableApiClient>();
        app.Simular<IGrupoClienteContableApiClient>();
        app.Simular<ISetupContableApiClient>();
        app.Simular<IAlmacenApiClient>();
        return app;
    }

    private static async Task<string> HtmlAsync(HttpClient cliente, string url)
    {
        var respuesta = await cliente.GetAsync(url);
        var html = await respuesta.Content.ReadAsStringAsync();
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, $"GET {url}: {respuesta.StatusCode}\n{html}");
        return html;
    }
}
```

(Los mocks sin `Setup` devuelven `null`: las páginas ya capturan el fallo de carga y muestran su aviso, que es lo que se quiere
comprobar en las rutas sin datos. `TipoGrupoContable` está en `OpenSource1.Core.Enums`; si el compilador lo ubica en otro
namespace, ajustar solo ese `using`.)

- [ ] **Step 2: Ejecutar y ver que fallan**

Run: `dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~ConversionContabilidadTests"`
Expected: FAIL (rutas nuevas inexistentes, formularios en línea presentes).

- [ ] **Step 3: `CuentaContableEditor.razor` (editor base del lote)**

`src/OpenSource1.Blazor/Components/Pages/CuentaContableEditor.razor`:

```razor
@page "/cuentas-contables/nuevo"
@page "/cuentas-contables/{Id:guid}/editar"
@attribute [Authorize(Policy = ApplicationPolicies.CanConsult)]
@using Microsoft.AspNetCore.Authorization
@using OpenSource1.Application.Security
@using OpenSource1.Blazor.Services
@inject ICuentaContableApiClient CuentaContableApiClient
@inject IAuthorizationService AuthorizationService
@inject ILogger<CuentaContableEditor> Logger
@inject NavigationManager Nav

<EntityFormPage Titulo="@Titulo" Subtitulo="Catálogo de cuentas contables para el libro contable." Migas="@Migas" Mensaje="@message" TipoMensaje="@messageType" Errores="@errores">
    @if (!Permitido)
    {
        <MessageBox Type="warning" Message="@SinPermiso" />
    }
    else if (EsEdicion && !encontrado)
    {
        <MessageBox Type="warning" Message="No se encontró la cuenta contable seleccionada. Puede haber sido eliminada por otro usuario." />
    }
    else if (EsEdicion)
    {
        <EditForm Model="UpdateInput" FormName="update-cuenta-contable" OnValidSubmit="UpdateAsync" Enhance="true">
            <DataAnnotationsValidator />
            <input type="hidden" name="UpdateInput.Id" value="@UpdateInput!.Id" />
            <input type="hidden" name="UpdateInput.Xmin" value="@UpdateInput.Xmin" />
            <CuentaContableFields Model="UpdateInput!" Prefix="UpdateInput" />
            <FormularioAcciones CancelarHref="@Volver" LimpiarHref="@Nav.Uri" />
        </EditForm>
    }
    else
    {
        <EditForm Model="SaveInput" FormName="save-cuenta-contable" OnValidSubmit="CreateAsync" Enhance="true">
            <DataAnnotationsValidator />
            <CuentaContableFields Model="SaveInput!" Prefix="SaveInput" />
            <FormularioAcciones CancelarHref="@Volver" LimpiarHref="@Nav.Uri" />
        </EditForm>
    }
</EntityFormPage>

@if (!MuestraAlta)
{
    <EditForm Model="SaveInput" FormName="save-cuenta-contable" OnValidSubmit="CreateAsync"></EditForm>
}
@if (!MuestraEdicion)
{
    <EditForm Model="UpdateInput" FormName="update-cuenta-contable" OnValidSubmit="UpdateAsync"></EditForm>
}

@code {
    private const string SinPermiso = "No tiene permiso para realizar esta acción.";

    [Parameter] public Guid? Id { get; set; }
    [SupplyParameterFromQuery(Name = "returnUrl")] private string? ReturnUrl { get; set; }
    [SupplyParameterFromForm(FormName = "save-cuenta-contable")] private CuentasContables.CuentaContableForm? SaveInput { get; set; }
    [SupplyParameterFromForm(FormName = "update-cuenta-contable")] private CuentasContables.CuentaContableForm? UpdateInput { get; set; }
    [CascadingParameter] private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    private bool canAdd;
    private bool canModify;
    private bool encontrado;
    private string? message;
    private string messageType = "info";
    private IReadOnlyList<string> errores = [];

    private bool EsEdicion => Id.HasValue;
    private bool Permitido => EsEdicion ? canModify : canAdd;
    private bool MuestraAlta => !EsEdicion && canAdd;
    private bool MuestraEdicion => EsEdicion && canModify && encontrado;
    private string Volver => RetornoLocal.Validar(ReturnUrl, "/cuentas-contables");
    private string Titulo => EsEdicion ? "Modificar cuenta contable" : "Nueva cuenta contable";
    private IReadOnlyList<Miga> Migas => [new("Contabilidad", "/modulos/contabilidad"), new("Plan de cuentas", Volver), new(EsEdicion ? "Modificar" : "Nueva")];

    protected override async Task OnInitializedAsync()
    {
        SaveInput ??= new();
        UpdateInput ??= new();
        if (AuthenticationStateTask is not null)
        {
            var user = (await AuthenticationStateTask).User;
            canAdd = (await AuthorizationService.AuthorizeAsync(user, ApplicationPolicies.CanAdd)).Succeeded;
            canModify = (await AuthorizationService.AuthorizeAsync(user, ApplicationPolicies.CanModify)).Succeeded;
        }

        if (Id is { } id && canModify)
        {
            try
            {
                var cuenta = await CuentaContableApiClient.GetByIdAsync(id);
                encontrado = cuenta is not null;
                if (cuenta is not null && UpdateInput.Id == Guid.Empty)
                {
                    UpdateInput.Id = cuenta.Id;
                    UpdateInput.Numero = cuenta.Numero;
                    UpdateInput.Nombre = cuenta.Nombre;
                    UpdateInput.TipoCuenta = cuenta.TipoCuenta;
                    UpdateInput.TipoResultado = cuenta.TipoResultado;
                    UpdateInput.PosteoDirecto = cuenta.PosteoDirecto;
                    UpdateInput.Bloqueada = cuenta.Bloqueada;
                    UpdateInput.Sangria = cuenta.Sangria;
                    UpdateInput.Xmin = cuenta.Xmin;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Could not load cuenta contable {Id} from API.", id);
                Show("danger", "No fue posible cargar la cuenta contable a modificar.");
            }
        }
    }

    private async Task CreateAsync() => await ExecuteAsync(() => CuentaContableApiClient.CreateAsync(ToInput(SaveInput!)), "created");

    private async Task UpdateAsync()
    {
        if (Id is not { } id)
        {
            Show("warning", "Seleccione primero un registro a modificar desde la tabla.");
            return;
        }

        await ExecuteAsync(() => CuentaContableApiClient.UpdateAsync(id, ToInput(UpdateInput!), UpdateInput!.Xmin), "updated");
    }

    private async Task ExecuteAsync(Func<Task<CuentaContableOperationResult>> operation, string successCode)
    {
        try
        {
            var result = await operation();
            if (result.Succeeded)
            {
                Nav.NavigateTo(RetornoLocal.ConParametro(Volver, "ok", successCode));
            }
            else
            {
                errores = result.Errors ?? [];
                Show("warning", result.Message);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not complete cuenta contable operation.");
            Show("danger", "No fue posible completar la operación.");
        }
    }

    private void Show(string type, string text) { messageType = type; message = text; }

    private static CuentaContableInput ToInput(CuentasContables.CuentaContableForm form) => new(
        form.Numero.Trim(),
        form.Nombre.Trim(),
        form.TipoCuenta,
        form.TipoResultado,
        form.PosteoDirecto,
        form.Bloqueada,
        form.Sangria);
}
```

`CuentasContables.razor`: sustituir el encabezado (`<div class="mb-6">` con "Contabilidad" / "Plan de Cuentas") por

```razor
<PageToolbar Titulo="Plan de Cuentas" Subtitulo="Catálogo de cuentas contables para el libro contable."
             Migas="@Migas"
             NuevoHref="@RetornoLocal.ConRetorno("/cuentas-contables/nuevo", BuildListUrl())"
             SeleccionId="@SeleccionValida?.ToString()"
             EditarUrl="@(id => EditarUrl(Guid.Parse(id)))"
             EliminarUrl="@(id => BuildListUrl(deleteId: Guid.Parse(id)))" />
```

borrar la sección `<!-- ── Formularios de operación ── -->` completa, borrar `SaveInput`/`UpdateInput`/`CreateAsync`/`UpdateAsync`/`ToInput`
y el bloque `if (EditId.HasValue && UpdateInput.Id == Guid.Empty) { … }`, y añadir al `@code`:

```csharp
    [SupplyParameterFromQuery(Name = "sel")]
    private Guid? Sel { get; set; }

    private Guid? SeleccionValida => Sel is { } s && cuentas.Any(c => c.Id == s) ? s : null;
    private static readonly IReadOnlyList<Miga> Migas = [new("Contabilidad", "/modulos/contabilidad"), new("Plan de cuentas")];
    private string EditarUrl(Guid id) => RetornoLocal.ConRetorno($"/cuentas-contables/{id}/editar", BuildListUrl());
```

con la redirección del `editId` como primera instrucción de `OnInitializedAsync`:

```csharp
        if (EditId is { } editarLegado)
        {
            Nav.NavigateTo($"/cuentas-contables/{editarLegado}/editar", replace: true);
            return;
        }
```

En `BuildListUrl` quitar el parámetro `editId` (y su `parameters.Add`) y añadir `Guid? sel = null` con
`if ((sel ?? Sel) is { } seleccion) parameters.Add($"sel={seleccion}");` antes de `deleteId`; conservar todos los filtros
que ya compone. En la tabla: columna `<SeleccionFila Href="@BuildListUrl(sel: cuenta.Id)" Seleccionada="@(cuenta.Id == SeleccionValida)" Etiqueta="@cuenta.Numero" />`
como primera celda (+ `<th>` con `sr-only`, `colspan` de la fila vacía +1, clase de fila seleccionada) y el enlace de
edición de la fila con `href="@EditarUrl(cuenta.Id)"`.

- [ ] **Step 4: `GrupoContableEditor.razor` y `GruposContables.razor`**

`src/OpenSource1.Blazor/Components/Pages/GrupoContableEditor.razor`:

```razor
@page "/grupos-contables/nuevo"
@page "/grupos-contables/{Id:guid}/editar"
@attribute [Authorize(Policy = ApplicationPolicies.CanConsult)]
@using Microsoft.AspNetCore.Authorization
@using OpenSource1.Application.Features.GruposContables
@using OpenSource1.Application.Security
@using OpenSource1.Blazor.Services
@using OpenSource1.Core.Enums
@inject IGrupoContableApiClient GrupoContableApiClient
@inject IAuthorizationService AuthorizationService
@inject ILogger<GrupoContableEditor> Logger
@inject NavigationManager Nav

<EntityFormPage Titulo="@Titulo" Subtitulo="@Descriptor.NombrePlural" Migas="@Migas" Mensaje="@message" TipoMensaje="@messageType" Errores="@errores">
    @if (!Permitido)
    {
        <MessageBox Type="warning" Message="@SinPermiso" />
    }
    else if (EsEdicion && !encontrado)
    {
        <MessageBox Type="warning" Message="No se encontró el grupo seleccionado. Puede haber sido eliminado por otro usuario." />
    }
    else if (EsEdicion)
    {
        <EditForm Model="UpdateInput" FormName="update-grupo-contable" OnValidSubmit="UpdateAsync" Enhance="true">
            <DataAnnotationsValidator />
            <input type="hidden" name="UpdateInput.Id" value="@UpdateInput!.Id" />
            <input type="hidden" name="UpdateInput.Xmin" value="@UpdateInput.Xmin" />
            <GrupoContableFields Model="UpdateInput!" Prefix="UpdateInput" />
            <FormularioAcciones CancelarHref="@Volver" LimpiarHref="@Nav.Uri" />
        </EditForm>
    }
    else
    {
        <EditForm Model="SaveInput" FormName="save-grupo-contable" OnValidSubmit="CreateAsync" Enhance="true">
            <DataAnnotationsValidator />
            <GrupoContableFields Model="SaveInput!" Prefix="SaveInput" />
            <FormularioAcciones CancelarHref="@Volver" LimpiarHref="@Nav.Uri" />
        </EditForm>
    }
</EntityFormPage>

@if (!MuestraAlta)
{
    <EditForm Model="SaveInput" FormName="save-grupo-contable" OnValidSubmit="CreateAsync"></EditForm>
}
@if (!MuestraEdicion)
{
    <EditForm Model="UpdateInput" FormName="update-grupo-contable" OnValidSubmit="UpdateAsync"></EditForm>
}

@code {
    private const string SinPermiso = "No tiene permiso para realizar esta acción.";

    [Parameter] public Guid? Id { get; set; }
    [SupplyParameterFromQuery(Name = "tipo")] private string? TipoRuta { get; set; }
    [SupplyParameterFromQuery(Name = "returnUrl")] private string? ReturnUrl { get; set; }
    [SupplyParameterFromForm(FormName = "save-grupo-contable")] private GrupoContableForm? SaveInput { get; set; }
    [SupplyParameterFromForm(FormName = "update-grupo-contable")] private GrupoContableForm? UpdateInput { get; set; }
    [CascadingParameter] private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    private bool canAdd;
    private bool canModify;
    private bool encontrado;
    private string? message;
    private string messageType = "info";
    private IReadOnlyList<string> errores = [];

    private TipoGrupoContable Tipo => TiposGrupoContable.DesdeRuta(TipoRuta) ?? TipoGrupoContable.Negocio;
    private TiposGrupoContable.Descriptor Descriptor => TiposGrupoContable.De(Tipo);
    private bool EsEdicion => Id.HasValue;
    private bool Permitido => EsEdicion ? canModify : canAdd;
    private bool MuestraAlta => !EsEdicion && canAdd;
    private bool MuestraEdicion => EsEdicion && canModify && encontrado;
    private string Volver => RetornoLocal.Validar(ReturnUrl, $"/grupos-contables?tipo={Descriptor.Ruta}");
    private string Titulo => EsEdicion ? $"Modificar {Descriptor.Nombre.ToLowerInvariant()}" : $"Nuevo {Descriptor.Nombre.ToLowerInvariant()}";
    private IReadOnlyList<Miga> Migas => [new("Configuración", "/modulos/configuracion"), new(Descriptor.NombrePlural, Volver), new(EsEdicion ? "Modificar" : "Nuevo")];

    protected override async Task OnInitializedAsync()
    {
        SaveInput ??= new();
        UpdateInput ??= new();
        if (AuthenticationStateTask is not null)
        {
            var user = (await AuthenticationStateTask).User;
            canAdd = (await AuthorizationService.AuthorizeAsync(user, ApplicationPolicies.CanAdd)).Succeeded;
            canModify = (await AuthorizationService.AuthorizeAsync(user, ApplicationPolicies.CanModify)).Succeeded;
        }

        if (Id is { } id && canModify)
        {
            try
            {
                var grupo = await GrupoContableApiClient.GetByIdAsync(Tipo, id);
                encontrado = grupo is not null;
                if (grupo is not null && UpdateInput.Id == Guid.Empty)
                {
                    UpdateInput.Id = grupo.Id;
                    UpdateInput.Codigo = grupo.Codigo;
                    UpdateInput.Descripcion = grupo.Descripcion;
                    UpdateInput.Xmin = grupo.Xmin;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Could not load grupo contable {Id} from API.", id);
                Show("danger", "No fue posible cargar el grupo a modificar.");
            }
        }
    }

    private async Task CreateAsync() =>
        await ExecuteAsync(() => GrupoContableApiClient.CreateAsync(Tipo, ToInput(SaveInput!)), "created");

    private async Task UpdateAsync()
    {
        if (Id is not { } id)
        {
            Show("warning", "Seleccione primero un registro a modificar desde la tabla.");
            return;
        }

        await ExecuteAsync(() => GrupoContableApiClient.UpdateAsync(Tipo, id, ToInput(UpdateInput!), UpdateInput!.Xmin), "updated");
    }

    private async Task ExecuteAsync(Func<Task<GrupoOperationResult>> operation, string successCode)
    {
        try
        {
            var result = await operation();
            if (result.Succeeded)
            {
                Nav.NavigateTo(RetornoLocal.ConParametro(Volver, "ok", successCode));
            }
            else
            {
                errores = result.Errors ?? [];
                Show("warning", result.Message);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not complete grupo contable operation.");
            Show("danger", "No fue posible completar la operación.");
        }
    }

    private void Show(string type, string text) { messageType = type; message = text; }

    private static GrupoContableInput ToInput(GrupoContableForm form) => new(form.Codigo.Trim(), form.Descripcion.Trim());
}
```

`GruposContables.razor`: igual que el listado de cuentas (Step 3) con `Titulo="@Descriptor.NombrePlural"`, migas
`[new("Configuración", "/modulos/configuracion"), new(Descriptor.NombrePlural)]` (propiedad no estática), lista `grupos`,
todas las URL con `tipo={Descriptor.Ruta}` (`NuevoHref = RetornoLocal.ConRetorno($"/grupos-contables/nuevo?tipo={Descriptor.Ruta}", BuildListUrl())`,
`EditarUrl(id) = RetornoLocal.ConRetorno($"/grupos-contables/{id}/editar?tipo={Descriptor.Ruta}", BuildListUrl())`) y
redirección legado `Nav.NavigateTo($"/grupos-contables/{editarLegado}/editar?tipo={Descriptor.Ruta}", replace: true)`.
Las pestañas de tipo y el aviso de tipo inválido se conservan tal cual.

- [ ] **Step 5: `GrupoClienteContableEditor.razor` y `GruposClienteContable.razor`**

Crear `src/OpenSource1.Blazor/Components/Pages/GrupoClienteContableEditor.razor` con la estructura exacta de
`GrupoContableEditor.razor` (Step 4) y estas diferencias:
- `@page "/grupos-cliente-contable/nuevo"` y `@page "/grupos-cliente-contable/{Id:guid}/editar"`; sin `tipo`.
- `@inject IGrupoClienteContableApiClient GrupoClienteContableApiClient` y `@inject ICuentaContableApiClient CuentaContableApiClient`; `@using OpenSource1.Application.Features.CuentasContables.Dtos` y `@using OpenSource1.Application.Features.GruposClienteContable.Dtos`.
- Formularios `save-grupo-cliente-contable` / `update-grupo-cliente-contable` con modelo `GrupoClienteContableForm`; dentro de cada `EditForm`, después de `<GrupoContableFields …/>`, la línea `@CamposCuentas(SaveInput!, "SaveInput", null)` (alta) o `@CamposCuentas(UpdateInput, "UpdateInput", grupoEnEdicion)` (edición), y `<FormularioAcciones … Deshabilitado="@cuentasCargaFallida" />`.
- **Mover literalmente** desde `GruposClienteContable.razor`: `CargarCuentasAsync()`, `CamposCuentas(...)`, `OpcionVigente(...)`, `Cuenta(...)`, los campos `cuentas`, `cuentasCargaFallida`, `grupoEnEdicion` y las constantes/CSS que esos métodos usen (`InputCss` u otras), `ToInput(GrupoClienteContableForm)` y el cuerpo de `CreateAsync`/`UpdateAsync` (con `Id` de ruta en vez de `UpdateInput.Id`).
- En `OnInitializedAsync`, tras calcular permisos: `if (Permitido) await CargarCuentasAsync();` y el bloque de carga del grupo en edición copiado del listado (líneas 284-306: `grupoEnEdicion = await GrupoClienteContableApiClient.GetByIdAsync(id)` + asignaciones de `Id`, `Codigo`, `Descripcion`, `CuentaCxCId`, `CuentaDescuentoId`, `CuentaInteresId`, `Xmin`), con `encontrado = grupoEnEdicion is not null;`.
- Títulos "Nuevo grupo contable de cliente" / "Modificar grupo contable de cliente"; migas `[new("Configuración", "/modulos/configuracion"), new("Grupos de cliente contable", Volver), new(EsEdicion ? "Modificar" : "Nuevo")]`; `Volver = RetornoLocal.Validar(ReturnUrl, "/grupos-cliente-contable")`.

`GruposClienteContable.razor`: pasos del listado de cuentas (Step 3) con título "Grupos contables de cliente", lista
`grupos`, rutas `/grupos-cliente-contable…`; borrar del listado lo movido si ya no se usa (si la tabla muestra números de
cuenta con `Cuenta(...)`, dejar `Cuenta` en el listado y **copiarlo** en el editor en lugar de moverlo).

- [ ] **Step 6: `SetupsContables.razor` — variante "mismo componente"**

`SetupsContables` comparte con sus formularios `CargarOpcionesAsync` (también alimenta los filtros), `LeerAsync` (también
lo usa el borrado) y los records `Ranura`/`Vigente`/`Fila`. Duplicarlos en otro componente sería peor que la receta, así que
la página-tarjeta se sirve **desde el mismo componente**:

1. Añadir, debajo de `@page "/setups-contables"`:

```razor
@page "/setups-contables/nuevo"
@page "/setups-contables/{Id:guid}/editar"
```

y al `@code`:

```csharp
    [Parameter] public Guid? Id { get; set; }
    [SupplyParameterFromQuery(Name = "returnUrl")] private string? ReturnUrl { get; set; }
    [SupplyParameterFromQuery(Name = "sel")] private Guid? Sel { get; set; }

    private bool EsPaginaAlta => Nav.ToBaseRelativePath(Nav.Uri).Split('?')[0].TrimEnd('/').EndsWith("setups-contables/nuevo", StringComparison.OrdinalIgnoreCase);
    private bool EsPaginaEdicion => Id.HasValue;
    private bool EsPaginaTarjeta => EsPaginaAlta || EsPaginaEdicion;
    private string Volver => RetornoLocal.Validar(ReturnUrl, $"/setups-contables?tipo={Descriptor.Ruta}");
    private Guid? SeleccionValida => Sel is { } s && filas.Any(f => f.Id == s) ? s : null;
```

(`filas` es el `IReadOnlyList<Fila>` que ya rellena `LoadAsync`.)

2. Envolver TODO el marcado actual (desde la cabecera hasta el diálogo de borrado incluido) en
   `@if (!EsPaginaTarjeta) { … }`, sustituyendo dentro la cabecera por `<PageToolbar>` (migas
   `[new("Configuración", "/modulos/configuracion"), new(Descriptor.NombrePlural)]`, `NuevoHref = RetornoLocal.ConRetorno($"/setups-contables/nuevo?tipo={Descriptor.Ruta}", BuildListUrl())`,
   `EditarUrl = id => RetornoLocal.ConRetorno($"/setups-contables/{id}/editar?tipo={Descriptor.Ruta}", BuildListUrl())`,
   `EliminarUrl = id => BuildListUrl(deleteId: Guid.Parse(id))`, `SeleccionId="@SeleccionValida?.ToString()"`) y quitando la
   sección `<!-- ── Formularios de operación ── -->`.
3. Añadir un bloque `else` con la página-tarjeta que reutiliza los mismos `EditForm` (mismos `FormName`, `@Campos(...)`,
   ocultos `Id`/`Xmin`):

```razor
else
{
    <EntityFormPage Titulo="@(EsPaginaEdicion ? $"Modificar fila del {Descriptor.NombrePlural.ToLowerInvariant()}" : $"Agregar fila al {Descriptor.NombrePlural.ToLowerInvariant()}")"
                    Migas="@([new("Configuración", "/modulos/configuracion"), new(Descriptor.NombrePlural, Volver), new(EsPaginaEdicion ? "Modificar" : "Nueva")])"
                    Mensaje="@message" TipoMensaje="@messageType" Errores="@errores">
        @if (EsPaginaEdicion ? !canModify : !canAdd)
        {
            <MessageBox Type="warning" Message="No tiene permiso para realizar esta acción." />
        }
        else if (EsPaginaEdicion && vigente is null)
        {
            <MessageBox Type="warning" Message="No se encontró el setup seleccionado. Puede haber sido eliminado por otro usuario." />
        }
        else if (EsPaginaEdicion)
        {
            <EditForm Model="UpdateInput" FormName="update-setup-contable" OnValidSubmit="UpdateAsync" Enhance="true">
                <DataAnnotationsValidator />
                <input type="hidden" name="UpdateInput.Id" value="@UpdateInput!.Id" />
                <input type="hidden" name="UpdateInput.Xmin" value="@UpdateInput.Xmin" />
                @Campos(UpdateInput, "UpdateInput", vigente)
                <FormularioAcciones CancelarHref="@Volver" LimpiarHref="@Nav.Uri" Deshabilitado="@opcionesCargaFallida" />
            </EditForm>
        }
        else
        {
            <EditForm Model="SaveInput" FormName="save-setup-contable" OnValidSubmit="CreateAsync" Enhance="true">
                <DataAnnotationsValidator />
                @Campos(SaveInput!, "SaveInput", null)
                <FormularioAcciones CancelarHref="@Volver" LimpiarHref="@Nav.Uri" Deshabilitado="@opcionesCargaFallida" />
            </EditForm>
        }
    </EntityFormPage>
}
```

4. Formularios siempre en el árbol **de la página-tarjeta** (en el listado ya no existen, igual que en los demás lotes): al
   final del archivo, `@if (EsPaginaTarjeta && !(EsPaginaAlta && canAdd)) { <EditForm Model="SaveInput" FormName="save-setup-contable" OnValidSubmit="CreateAsync"></EditForm> }`
   y `@if (EsPaginaTarjeta && !(EsPaginaEdicion && canModify && vigente is not null)) { <EditForm Model="UpdateInput" FormName="update-setup-contable" OnValidSubmit="UpdateAsync"></EditForm> }`.
5. En `OnInitializedAsync`: primero la redirección legado
   `if (EditId is { } editarLegado) { Nav.NavigateTo($"/setups-contables/{editarLegado}/editar?tipo={Descriptor.Ruta}", replace: true); return; }`;
   la carga de edición usa `var editarId = Id ?? (UpdateInput.Id != Guid.Empty ? UpdateInput.Id : null);`; `LoadAsync()`
   solo si `!EsPaginaTarjeta`.
6. `ExecuteAsync`: navegar a `RetornoLocal.ConParametro(Volver, "ok", successCode)` para `created`/`updated` y mantener
   `$"/setups-contables?tipo={Descriptor.Ruta}&ok=deleted"` para el borrado. `UpdateAsync` usa `Id` de ruta.
7. `BuildListUrl`: quitar `editId`, añadir `sel`, conservar `tipo` y filtros.

- [ ] **Step 7: Ejecutar los tests del lote**

Run: `dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~ConversionContabilidadTests|FullyQualifiedName~RegistroModulosTests"`
Expected: PASS.

- [ ] **Step 8: Build sin avisos**

Run: `dotnet build test.slnx -warnaserror`
Expected: 0 avisos.

- [ ] **Step 9: Commit**

```bash
git add src/OpenSource1.Blazor/Components/Pages/CuentaContableEditor.razor src/OpenSource1.Blazor/Components/Pages/GrupoContableEditor.razor src/OpenSource1.Blazor/Components/Pages/GrupoClienteContableEditor.razor src/OpenSource1.Blazor/Components/Pages/CuentasContables.razor src/OpenSource1.Blazor/Components/Pages/GruposContables.razor src/OpenSource1.Blazor/Components/Pages/GruposClienteContable.razor src/OpenSource1.Blazor/Components/Pages/SetupsContables.razor tests/OpenSource1.SmokeTests/Blazor/ConversionContabilidadTests.cs
git commit -m "feat: alta y edicion de maestros contables en pagina propia con barra de acciones"
```

---

### Task B2c: Lote 3 — Clientes y Productos (listado, alta, edición y rutas antiguas)

**Grupo de paralelismo:** B2 — **en paralelo con B2a, B2b y B2d**. Mismas prohibiciones de archivos compartidos.

**Files:**
- Create: `src/OpenSource1.Blazor/Components/Pages/ClienteEditor.razor`
- Create: `src/OpenSource1.Blazor/Components/Pages/ProductoEditor.razor`
- Modify (reemplazo completo, redirección): `src/OpenSource1.Blazor/Components/Pages/ClienteNew.razor`, `src/OpenSource1.Blazor/Components/Pages/ProductoNew.razor`
- Modify: `src/OpenSource1.Blazor/Components/Pages/ClienteDetail.razor` (retira el modo edición)
- Modify: `src/OpenSource1.Blazor/Components/Pages/ProductoDetail.razor` (retira el modo edición)
- Modify: `src/OpenSource1.Blazor/Components/Pages/Clientes.razor`, `src/OpenSource1.Blazor/Components/Pages/Productos.razor` (barra, selección con `?sel=`, tarjetas)
- Test: `tests/OpenSource1.SmokeTests/Blazor/ConversionClientesProductosTests.cs`

**Interfaces:**
- Consumes: B1; `ISocioNegocioApiClient`, `IProductoApiClient`, `ITerminoPagoApiClient`, `IGrupoContableApiClient`, `IGrupoClienteContableApiClient`, `ICategoriaProductoApiClient`, `IUnidadMedidaApiClient`, `IFileStorageService`, `ClienteEditorForm`, `ProductoEditorForm`, `TerminoPagoOpciones`, `GruposSocioOpciones`, `ProductoOpciones`, `GruposProductoOpciones`, `GruposOpciones`.
- Produces (C1 y C2 los amplían): `/clientes/nuevo`, `/clientes/{id}/editar`, `/productos/nuevo`, `/productos/{id}/editar`; `/clientes/new` → `/clientes/nuevo`; `/productos/new` → `/productos/nuevo`; `/clientes/{id}?edit=true` → `/clientes/{id}/editar?returnUrl=%2Fclientes%2F{id}`; ídem productos. En `Clientes.razor` y `Productos.razor`: `?sel={id}` sustituye a `?focusId=` como selección (la ficha lateral "Detalles" se muestra con la selección), `Clientes.SeleccionValida`/`Productos.SeleccionValida`, y el hueco `@TarjetaAccionesDe(cliente)` / `@TarjetaAccionesDe(producto)` en cada tarjeta (C1/C2 añaden las acciones de Crear/Ver).

- [ ] **Step 1: Tests del lote (fallan)**

`tests/OpenSource1.SmokeTests/Blazor/ConversionClientesProductosTests.cs`:

```csharp
extern alias BlazorApp;

using System.Net;
using BlazorApp::OpenSource1.Blazor.Services;
using Moq;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>Fix-Features B2c: clientes y productos con alta/edición en página-tarjeta, selección ?sel= y rutas antiguas redirigidas.</summary>
public sealed class ConversionClientesProductosTests
{
    private static readonly Guid IdCliente = Guid.Parse("7c000000-0000-0000-0000-000000000001");
    private static readonly Guid IdProducto = Guid.Parse("7c000000-0000-0000-0000-000000000002");

    [Theory]
    [InlineData("/clientes/new", "/clientes/nuevo")]
    [InlineData("/productos/new", "/productos/nuevo")]
    public async Task RutaNewAntigua_RedirigeANuevo(string origen, string destino)
    {
        using var app = Configurar(new BlazorSsrFactory());

        var respuesta = await app.Cliente().GetAsync(origen);

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal(destino, FormulariosSsr.Destino(respuesta));
    }

    [Fact]
    public async Task FichaConEditTrue_RedirigeALaPaginaDeEdicion()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var cliente = await app.Cliente().GetAsync($"/clientes/{IdCliente}?edit=true");
        var producto = await app.Cliente().GetAsync($"/productos/{IdProducto}?edit=true");

        Assert.Equal($"/clientes/{IdCliente}/editar?returnUrl=%2Fclientes%2F{IdCliente}", FormulariosSsr.Destino(cliente));
        Assert.Equal($"/productos/{IdProducto}/editar?returnUrl=%2Fproductos%2F{IdProducto}", FormulariosSsr.Destino(producto));
    }

    [Theory]
    [InlineData("/clientes/nuevo", "cliente-new")]
    [InlineData("/productos/nuevo", "producto-new")]
    public async Task Nuevo_TarjetaConSubidaDeImagen(string ruta, string formName)
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlAsync(app.Cliente(), ruta);

        Assert.Contains("data-testid=\"entity-form-page\"", html);
        Assert.Contains($"name=\"_handler\" value=\"{formName}\"", html);
        Assert.Contains("enctype=\"multipart/form-data\"", html);
    }

    [Fact]
    public async Task Editar_Cliente_CargaLoGuardado_YEjecutorNoPuede()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var admin = await HtmlAsync(app.Cliente(), $"/clientes/{IdCliente}/editar");
        var ejecutor = await HtmlAsync(app.Cliente("Ejecutor"), $"/clientes/{IdCliente}/editar");

        Assert.Contains("value=\"Comercial Uno\"", admin);
        Assert.Contains("name=\"_handler\" value=\"cliente-edit\"", admin);
        Assert.Contains("No tiene permiso para realizar esta acción.", ejecutor);
        Assert.Contains("value=\"cliente-edit\"", ejecutor);
    }

    [Fact]
    public async Task Listados_ConBarraSeleccionYTarjetas()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var clientesLista = await HtmlAsync(app.Cliente(), $"/clientes?view=list&sel={IdCliente}");
        var clientesTarjetas = await HtmlAsync(app.Cliente(), "/clientes?view=grid");
        var productosLista = await HtmlAsync(app.Cliente(), $"/productos?view=list&sel={IdProducto}");

        Assert.Contains($"<a data-testid=\"accion-editar\" href=\"/clientes/{IdCliente}/editar?returnUrl=", clientesLista);
        Assert.Contains("href=\"/clientes/nuevo?returnUrl=", clientesLista);
        Assert.Contains("data-testid=\"tarjeta-acciones\"", clientesTarjetas);
        Assert.Contains($"<a data-testid=\"accion-editar\" href=\"/productos/{IdProducto}/editar?returnUrl=", productosLista);
        Assert.DoesNotContain("href=\"/clientes/new\"", clientesLista);
    }

    private static BlazorSsrFactory Configurar(BlazorSsrFactory app)
    {
        var cliente = new SocioNegocioResponse { Id = IdCliente, Codigo = "C0001", NombreComercial = "Comercial Uno", Email = "uno@test.local" };
        var producto = new ProductoResponse { Id = IdProducto, Codigo = "P0001", Nombre = "Tornillo", CategoriaCodigo = "GENERAL", CategoriaNombre = "General" };
        var socios = app.Simular<ISocioNegocioApiClient>();
        socios.Setup(c => c.ListAsync(It.IsAny<SocioNegocioSearchFilter?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<SocioNegocioResponse>([cliente], 1, 50, 1));
        socios.Setup(c => c.GetByIdAsync(IdCliente, It.IsAny<CancellationToken>())).ReturnsAsync(cliente);
        var productos = app.Simular<IProductoApiClient>();
        productos.Setup(c => c.ListAsync(It.IsAny<ProductoSearchFilter?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<ProductoResponse>([producto], 1, 50, 1));
        productos.Setup(c => c.GetByIdAsync(IdProducto, It.IsAny<CancellationToken>())).ReturnsAsync(producto);
        app.Simular<ITerminoPagoApiClient>().Setup(c => c.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        app.Simular<IGrupoContableApiClient>();
        app.Simular<IGrupoClienteContableApiClient>();
        app.Simular<ICategoriaProductoApiClient>().Setup(c => c.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        app.Simular<IUnidadMedidaApiClient>();
        app.Simular<ICobroApiClient>();
        return app;
    }

    private static async Task<string> HtmlAsync(HttpClient cliente, string url)
    {
        var respuesta = await cliente.GetAsync(url);
        var html = await respuesta.Content.ReadAsStringAsync();
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, $"GET {url}: {respuesta.StatusCode}\n{html}");
        return html;
    }
}
```

- [ ] **Step 2: Ejecutar y ver que fallan**

Run: `dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~ConversionClientesProductosTests"`
Expected: FAIL.

- [ ] **Step 3: `ClienteEditor.razor` (alta de `ClienteNew` + edición de `ClienteDetail`)**

`src/OpenSource1.Blazor/Components/Pages/ClienteEditor.razor`:

```razor
@page "/clientes/nuevo"
@page "/clientes/{Id:guid}/editar"
@attribute [Authorize(Policy = ApplicationPolicies.CanConsult)]
@using Microsoft.AspNetCore.Authorization
@using OpenSource1.Application.Features.SociosNegocio.Dtos
@using OpenSource1.Application.Security
@using OpenSource1.Application.Storage
@using OpenSource1.Blazor.Services
@using OpenSource1.Core.Entities.Contabilidad
@inject ISocioNegocioApiClient SocioNegocioApiClient
@inject ITerminoPagoApiClient TerminoPagoApiClient
@inject IGrupoContableApiClient GrupoContableApiClient
@inject IGrupoClienteContableApiClient GrupoClienteContableApiClient
@inject IHttpContextAccessor HttpContextAccessor
@inject IFileStorageService FileStorageService
@inject IAuthorizationService AuthorizationService
@inject ILogger<ClienteEditor> Logger
@inject NavigationManager Nav

<EntityFormPage Titulo="@(EsEdicion ? "Modificar cliente" : "Nuevo cliente")"
                Subtitulo="Datos generales, fiscales y comerciales. El código lo asigna el sistema al guardar."
                Migas="@Migas" Mensaje="@message" TipoMensaje="@messageType" Errores="@errores">
    @if (!Permitido)
    {
        <MessageBox Type="warning" Message="No tiene permiso para realizar esta acción." />
    }
    else if (EsEdicion && cliente is null)
    {
        <MessageBox Type="warning" Message="@(loadError ?? "No se encontró el cliente solicitado.")" />
    }
    else if (EsEdicion)
    {
        @if (edicionBloqueada)
        {
            <div class="mb-4"><MessageBox Type="danger" Message="@TerminoPagoOpciones.MensajeNoDisponibles" /></div>
        }
        @if (grupos.CargaFallida)
        {
            <div class="mb-4"><MessageBox Type="warning" Message="@GruposSocioOpciones.MensajeNoDisponibles" /></div>
        }
        <EditForm Model="EdicionInput" FormName="cliente-edit" OnValidSubmit="SaveAsync" Enhance="true" enctype="multipart/form-data">
            <DataAnnotationsValidator />
            <div class="mb-6 grid grid-cols-1 gap-4 md:grid-cols-2">
                <div>
                    <p class="text-xs font-bold uppercase tracking-wide text-slate-400">Imagen actual</p>
                    @if (!string.IsNullOrWhiteSpace(cliente!.ImagePath))
                    {
                        <img src="@cliente.ImagePath" alt="Imagen del cliente" class="mt-2 h-32 w-32 rounded-xl border border-slate-200 object-cover" />
                    }
                    else
                    {
                        <div class="mt-2 flex h-32 w-32 items-center justify-center rounded-xl border border-dashed border-slate-300 text-xs text-slate-400">Sin imagen</div>
                    }
                </div>
                <div>
                    <label class="mb-1 block text-sm font-medium text-slate-700 dark:text-slate-300">Cambiar imagen</label>
                    <input type="file" name="ClientImage" accept=".jpg,.jpeg,.png,.webp" class="block w-full text-sm text-slate-600" />
                    <p class="mt-1 text-xs text-slate-400">Formatos permitidos: JPG, PNG, WEBP. Máximo 2 MB.</p>
                </div>
            </div>
            <ClienteFields Model="EdicionInput!" Prefix="Input" EsEdicion="true" Codigo="@cliente.Codigo" Terminos="@terminos.Items" TerminoActualId="@cliente.TerminoPagoId" TerminoActualNombre="@terminoActualNombre"
                           Grupos="@grupos" GrupoNegocioVigente="@cliente.GrupoNegocioCodigo" GrupoIvaNegocioVigente="@cliente.GrupoIvaNegocioCodigo" GrupoClienteContableVigente="@cliente.GrupoClienteContableCodigo" />
            <FormularioAcciones CancelarHref="@Volver" LimpiarHref="@Nav.Uri" TextoGuardar="Guardar cambios" Deshabilitado="@edicionBloqueada" />
        </EditForm>
    }
    else
    {
        @if (terminos.CargaFallida)
        {
            <div class="mb-4"><MessageBox Type="warning" Message="@TerminoPagoOpciones.MensajeNoDisponiblesAlta" /></div>
        }
        @if (grupos.CargaFallida)
        {
            <div class="mb-4"><MessageBox Type="warning" Message="@GruposSocioOpciones.MensajeNoDisponiblesAlta" /></div>
        }
        <EditForm Model="AltaInput" FormName="cliente-new" OnValidSubmit="CreateAsync" Enhance="true" enctype="multipart/form-data">
            <DataAnnotationsValidator />
            <div class="mb-6">
                <label class="mb-1 block text-sm font-medium text-slate-700 dark:text-slate-300">Imagen del cliente</label>
                <input type="file" name="ClientImage" accept=".jpg,.jpeg,.png,.webp" class="block w-full text-sm text-slate-600" />
                <p class="mt-1 text-xs text-slate-400">Formatos permitidos: JPG, PNG, WEBP. Máximo 2 MB.</p>
            </div>
            <ClienteFields Model="AltaInput!" Prefix="Input" Terminos="@terminos.Items" Grupos="@grupos" />
            <FormularioAcciones CancelarHref="@Volver" LimpiarHref="@Nav.Uri" TextoGuardar="Guardar cliente" />
        </EditForm>
    }
</EntityFormPage>

@if (!(!EsEdicion && canAdd))
{
    <EditForm Model="AltaInput" FormName="cliente-new" OnValidSubmit="CreateAsync"></EditForm>
}
@if (!(EsEdicion && canModify && cliente is not null))
{
    <EditForm Model="EdicionInput" FormName="cliente-edit" OnValidSubmit="SaveAsync"></EditForm>
}

@code {
    [Parameter] public Guid? Id { get; set; }
    [SupplyParameterFromQuery(Name = "returnUrl")] private string? ReturnUrl { get; set; }
    // Ambos formularios usan el prefijo "Input" (ClienteFields Prefix="Input"), cada uno con su FormName.
    [SupplyParameterFromForm(FormName = "cliente-new", Name = "Input")] private ClienteEditorForm? AltaInput { get; set; }
    [SupplyParameterFromForm(FormName = "cliente-edit", Name = "Input")] private ClienteEditorForm? EdicionInput { get; set; }
    [CascadingParameter] private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    private SocioNegocioResponse? cliente;
    private string? message;
    private string messageType = "danger";
    private IReadOnlyList<string> errores = [];
    private string? loadError;
    private bool canAdd;
    private bool canModify;
    private TerminoPagoOpciones terminos = TerminoPagoOpciones.SinCargar;
    private string? terminoActualNombre;
    private bool edicionBloqueada;
    private GruposSocioOpciones grupos = GruposSocioOpciones.SinCargar;

    private bool EsEdicion => Id.HasValue;
    private bool Permitido => EsEdicion ? canModify : canAdd;
    private string Volver => RetornoLocal.Validar(ReturnUrl, "/clientes");
    private IReadOnlyList<Miga> Migas => [new("Clientes", "/modulos/clientes"), new("Clientes", Volver), new(EsEdicion ? "Modificar" : "Nuevo")];

    protected override async Task OnInitializedAsync()
    {
        if (AuthenticationStateTask is not null)
        {
            var user = (await AuthenticationStateTask).User;
            canAdd = (await AuthorizationService.AuthorizeAsync(user, ApplicationPolicies.CanAdd)).Succeeded;
            canModify = (await AuthorizationService.AuthorizeAsync(user, ApplicationPolicies.CanModify)).Succeeded;
        }

        if (EsEdicion)
        {
            AltaInput ??= new();
            if (canModify)
            {
                await CargarEdicionAsync(Id!.Value);
            }

            EdicionInput ??= new();
            return;
        }

        EdicionInput ??= new();
        var formularioNuevo = AltaInput is null;
        AltaInput ??= new();
        if (!canAdd)
        {
            return;
        }

        // Opciones del <select> de término de pago; si no cargan el alta sigue siendo posible (sin término).
        terminos = await TerminoPagoOpciones.CargarAsync(TerminoPagoApiClient, Logger);
        // Grupos contables (Task 5.3): si no cargan, el alta sigue siendo posible y la API asigna los grupos por defecto.
        grupos = await GruposSocioOpciones.CargarAsync(GrupoContableApiClient, GrupoClienteContableApiClient, Logger);
        if (formularioNuevo)
        {
            AltaInput.GrupoNegocioId = GruposOpciones.PorDefecto(grupos.Negocio, GrupoContableIds.NegocioNacional);
            AltaInput.GrupoIvaNegocioId = GruposOpciones.PorDefecto(grupos.IvaNegocio, GrupoContableIds.IvaNegocioItbis18);
            AltaInput.GrupoClienteContableId = GruposOpciones.PorDefecto(grupos.ClienteContable, GrupoContableIds.ClienteContableGeneral);
        }
    }

    private async Task CargarEdicionAsync(Guid id)
    {
        try
        {
            cliente = await SocioNegocioApiClient.GetByIdAsync(id);
            if (cliente is null)
            {
                return;
            }

            // Solo se rellena con lo guardado cuando NO llegó un formulario.
            EdicionInput ??= new ClienteEditorForm
            {
                NombreComercial = cliente.NombreComercial, Email = cliente.Email, Telefono = cliente.Telefono,
                DireccionLinea1 = cliente.DireccionLinea1, DireccionLinea2 = cliente.DireccionLinea2,
                Sector = cliente.Sector, PaisCodigo = cliente.PaisCodigo, ImagePath = cliente.ImagePath,
                Tipo = cliente.Tipo, RazonSocial = cliente.RazonSocial, TipoDocumentoFiscal = cliente.TipoDocumentoFiscal,
                NumeroDocumentoFiscal = cliente.NumeroDocumentoFiscal, Ciudad = cliente.Ciudad,
                TerminoPagoId = cliente.TerminoPagoId, LimiteCreditoTexto = EntradaDecimal.Formatear(cliente.LimiteCredito),
                Bloqueado = cliente.Bloqueado,
                GrupoNegocioId = cliente.GrupoNegocioId, GrupoIvaNegocioId = cliente.GrupoIvaNegocioId,
                GrupoClienteContableId = cliente.GrupoClienteContableId
            };

            terminos = await TerminoPagoOpciones.CargarAsync(TerminoPagoApiClient, Logger);
            edicionBloqueada = terminos.ModificacionBloqueada(cliente.TerminoPagoId);
            grupos = await GruposSocioOpciones.CargarAsync(GrupoContableApiClient, GrupoClienteContableApiClient, Logger);
            if (grupos.CargaFallida)
            {
                EdicionInput.GrupoNegocioId = cliente.GrupoNegocioId;
                EdicionInput.GrupoIvaNegocioId = cliente.GrupoIvaNegocioId;
                EdicionInput.GrupoClienteContableId = cliente.GrupoClienteContableId;
            }

            if (cliente.TerminoPagoId is { } terminoId && terminos.Items.All(t => t.Id != terminoId))
            {
                if (!terminos.CargaFallida)
                {
                    terminoActualNombre = await TerminoPagoOpciones.NombreAsync(TerminoPagoApiClient, terminoId, Logger);
                }
            }
            else if (cliente.TerminoPagoId is { } enLista)
            {
                var t = terminos.Items.First(x => x.Id == enLista);
                terminoActualNombre = $"{t.Codigo} — {t.Descripcion}";
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not load cliente for edit.");
            loadError = "No fue posible cargar la ficha del cliente.";
        }
    }

    private async Task CreateAsync()
    {
        string? imagenNueva = null;
        try
        {
            var httpContext = HttpContextAccessor.HttpContext ?? throw new InvalidOperationException("No hay HttpContext disponible para cargar la imagen.");
            imagenNueva = await FileStorageService.SaveClientImageAsync(httpContext, "ClientImage", null);
            AltaInput!.ImagePath = imagenNueva;
            var result = await SocioNegocioApiClient.CreateAsync(ToInputAlta(AltaInput));
            if (result.Succeeded)
            {
                Nav.NavigateTo(RetornoLocal.ConParametro(Volver, "ok", "created"));
                return;
            }

            await FileStorageService.DeleteIfExistsAsync(imagenNueva, RutaImagen.CarpetaClientes);
            message = result.Message;
            errores = result.Errors ?? [];
            messageType = "warning";
        }
        catch (Exception ex)
        {
            await FileStorageService.DeleteIfExistsAsync(imagenNueva, RutaImagen.CarpetaClientes);
            Logger.LogWarning(ex, "Could not create cliente.");
            message = ex is InvalidOperationException ? ex.Message : "No fue posible crear el cliente.";
            messageType = "danger";
        }
    }

    private async Task SaveAsync()
    {
        if (cliente is null)
        {
            // Sin permiso o sin registro: el PUT se intenta igual para devolver el mensaje real de la API (403/404).
            if (Id is { } idSinCarga)
            {
                var forzado = await SocioNegocioApiClient.UpdateAsync(idSinCarga, ToInputEdicion(EdicionInput!, gruposModificables: false));
                message = forzado.Succeeded ? null : forzado.Message;
                messageType = "warning";
            }

            return;
        }

        if (edicionBloqueada)
        {
            return;
        }

        var imagenAnterior = cliente.ImagePath;
        string? imagenNueva = null;
        try
        {
            var httpContext = HttpContextAccessor.HttpContext ?? throw new InvalidOperationException("No hay HttpContext disponible para cargar la imagen.");
            imagenNueva = await FileStorageService.SaveClientImageAsync(httpContext, "ClientImage", imagenAnterior);
            EdicionInput!.ImagePath = imagenNueva;
            var result = await SocioNegocioApiClient.UpdateAsync(cliente.Id, ToInputEdicion(EdicionInput, gruposModificables: !grupos.CargaFallida));
            if (result.Succeeded)
            {
                if (imagenNueva != imagenAnterior) await FileStorageService.DeleteIfExistsAsync(imagenAnterior, RutaImagen.CarpetaClientes);
                Nav.NavigateTo(RetornoLocal.ConParametro(Volver, "ok", "updated"));
                return;
            }

            await DescartarImagenNuevaAsync(imagenNueva, imagenAnterior);
            message = result.Message;
            errores = result.Errors ?? [];
            messageType = "warning";
        }
        catch (Exception ex)
        {
            await DescartarImagenNuevaAsync(imagenNueva, imagenAnterior);
            Logger.LogWarning(ex, "Could not update cliente.");
            message = ex is InvalidOperationException ? ex.Message : "No fue posible actualizar el cliente.";
            messageType = "danger";
        }
    }

    private async Task DescartarImagenNuevaAsync(string? imagenNueva, string? imagenAnterior)
    {
        if (imagenNueva is not null && imagenNueva != imagenAnterior)
        {
            await FileStorageService.DeleteIfExistsAsync(imagenNueva, RutaImagen.CarpetaClientes);
        }
    }

    private static SocioNegocioInput ToInputAlta(ClienteEditorForm form) => new(
        form.NombreComercial.Trim(), form.Email?.Trim(), form.Telefono?.Trim(), form.DireccionLinea1?.Trim(), form.DireccionLinea2?.Trim(),
        form.Sector?.Trim(), form.PaisCodigo, form.ImagePath, form.RazonSocial?.Trim(), form.Tipo, form.TipoDocumentoFiscal,
        form.NumeroDocumentoFiscal?.Trim(), form.Ciudad?.Trim(), form.TerminoPagoId is { } id && id != Guid.Empty ? id : null,
        form.LimiteCredito, form.Bloqueado, form.GrupoNegocioId, form.GrupoIvaNegocioId, form.GrupoClienteContableId);

    // Edición: "" y Guid.Empty = limpiar; null = conservar (ver comentario original en ClienteDetail.ToInput).
    private static SocioNegocioInput ToInputEdicion(ClienteEditorForm form, bool gruposModificables) => new(
        form.NombreComercial.Trim(), form.Email?.Trim(), form.Telefono?.Trim(), form.DireccionLinea1?.Trim(), form.DireccionLinea2?.Trim(),
        form.Sector?.Trim(), form.PaisCodigo, form.ImagePath, form.RazonSocial?.Trim() ?? string.Empty, form.Tipo, form.TipoDocumentoFiscal,
        form.NumeroDocumentoFiscal?.Trim() ?? string.Empty, form.Ciudad?.Trim() ?? string.Empty, form.TerminoPagoId ?? Guid.Empty,
        form.LimiteCredito, form.Bloqueado,
        gruposModificables ? form.GrupoNegocioId : null,
        gruposModificables ? form.GrupoIvaNegocioId : null,
        gruposModificables ? form.GrupoClienteContableId : null);
}
```

- [ ] **Step 4: `ProductoEditor.razor`**

`src/OpenSource1.Blazor/Components/Pages/ProductoEditor.razor`: misma estructura que `ClienteEditor.razor` (Step 3), con:
- `@page "/productos/nuevo"` / `@page "/productos/{Id:guid}/editar"`; injects de `ProductoNew.razor` + `IAuthorizationService` + `ILogger<ProductoEditor>`; `@using OpenSource1.Application.Features.Productos.Dtos`, `OpenSource1.Application.Storage`, `OpenSource1.Core.Entities.Contabilidad`.
- Formularios `producto-new` (Model `AltaInput`) y `producto-edit` (Model `EdicionInput`), ambos `[SupplyParameterFromForm(…, Name = "Input")] ProductoEditorForm`; input de archivo `name="ProductImage"`.
- Alta = cuerpo de `ProductoNew.OnInitializedAsync` (opciones `ProductoOpciones.CargarAsync`, `GruposProductoOpciones.CargarAsync`, preselección `GrupoContableIds.ProductoBienes/IvaProductoItbis18/InventarioGeneral`), `CreateAsync` y `ToInput` de `ProductoNew` (renombrado `ToInputAlta`), con avisos `ProductoOpciones.MensajeNoDisponiblesAlta` / `GruposProductoOpciones.MensajeNoDisponiblesAlta`; `<ProductoFields Model="AltaInput!" Prefix="Input" Categorias="@opciones.Categorias" Unidades="@opciones.Unidades" Grupos="@grupos" />`.
- Edición = de `ProductoDetail`: el inicializador `Input ??= new ProductoEditorForm { … }` (a `EdicionInput`), la carga de `opciones`/`edicionBloqueada = opciones.ModificacionBloqueada`/`grupos` (sin la condición `EditMode`), `SaveAsync` y `ToInput(form, gruposModificables)` (renombrado `ToInputEdicion`), con avisos `ProductoOpciones.MensajeNoDisponibles` / `GruposProductoOpciones.MensajeNoDisponibles` y el componente de campos de la ficha actual: `<ProductoFields Model="EdicionInput!" Prefix="Input" EsEdicion="true" Categorias="@opciones.Categorias" Unidades="@opciones.Unidades" CategoriaActualId="@producto.CategoriaId" CategoriaActualNombre="@($"{producto.CategoriaCodigo} — {producto.CategoriaNombre}")" UnidadActualId="@producto.UnidadMedidaBaseId" UnidadActualNombre="@($"{producto.UnidadMedidaNombre} ({producto.UnidadMedidaCodigo})")" Grupos="@grupos" GrupoProductoVigente="@producto.GrupoProductoCodigo" GrupoIvaProductoVigente="@producto.GrupoIvaProductoCodigo" GrupoInventarioVigente="@producto.GrupoInventarioCodigo" />` (el campo `producto` es el `ProductoResponse` cargado, equivalente a `cliente` en `ClienteEditor`), más la imagen actual y el input `ProductImage` como en el Step 3.
- Tras guardar: `RetornoLocal.ConParametro(Volver, "ok", "created"|"updated")` con `Volver = RetornoLocal.Validar(ReturnUrl, "/productos")`; migas `[new("Productos", "/modulos/productos"), new("Productos", Volver), new(EsEdicion ? "Modificar" : "Nuevo")]`; títulos "Nuevo producto" / "Modificar producto".
- La tabla de existencia por almacén **no** se mueve (se queda en la ficha).

- [ ] **Step 5: Redirecciones `…/new` y fichas sin modo edición**

`ClienteNew.razor` (reemplazo completo):

```razor
@page "/clientes/new"
@attribute [Authorize(Policy = ApplicationPolicies.CanConsult)]
@using Microsoft.AspNetCore.Authorization
@inject NavigationManager Nav

@code {
    // Ruta antigua (Fix-Features B2c): el alta vive en /clientes/nuevo.
    protected override void OnInitialized() => Nav.NavigateTo("/clientes/nuevo", replace: true);
}
```

`ProductoNew.razor` (reemplazo completo): igual con `@page "/productos/new"` y destino `"/productos/nuevo"`.

`ClienteDetail.razor`:
1. Primera instrucción de `OnInitializedAsync`: `if (EditMode) { Nav.NavigateTo(RetornoLocal.ConRetorno($"/clientes/{Id}/editar", $"/clientes/{Id}"), replace: true); return; }`.
2. Los dos enlaces "Editar" (`BuildDetailUrl(edit: true)`) pasan a `href="@RetornoLocal.ConRetorno($"/clientes/{Id}/editar", $"/clientes/{Id}")"`.
3. Borrar el bloque `@if (EditMode && canModify) { <EditForm … FormName="cliente-edit" …> … </EditForm> } else {` dejando solo el contenido de la rama `else` (la vista de la ficha); borrar los avisos `edicionBloqueada`/`grupos.CargaFallida` de la cabecera; borrar `Input`, `SaveAsync`, `DescartarImagenNuevaAsync`, `ToInput`, `edicionBloqueada`, `grupos`, la carga `if (EditMode && canModify) { … }` de `LoadAsync` y los `@inject` que queden sin uso (`IHttpContextAccessor`, `IFileStorageService`, `IGrupoContableApiClient`, `IGrupoClienteContableApiClient`). La lógica de `terminoActualNombre` para mostrar el término se conserva.
4. `BuildDetailUrl` pierde el parámetro `edit` (el diálogo de borrado usa `BuildDetailUrl()`).

`ProductoDetail.razor`: los mismos cuatro cambios con rutas `/productos/{Id}` (queda la tabla de existencias).

- [ ] **Step 6: Listados `Clientes.razor` y `Productos.razor`**

En ambos:
1. `[SupplyParameterFromQuery(Name = "focusId")]` pasa a `Name = "sel"` (propiedad renombrada a `Sel`); la ficha lateral
   "Detalles" se muestra cuando hay selección válida (`SeleccionValida`), y el `data-auto-nav`/FAB de `EntityFabOnChange`
   compone `sel=` en lugar de `focusId=` (cambiar el literal `'focusId='` del JS en línea por `'sel='`).
2. Sustituir la cabecera (`<div …>` con "Mantenimiento"/`<h1>Clientes</h1>` y los botones) por `<PageToolbar>` con
   `Titulo="Clientes"`, migas `[new("Clientes", "/modulos/clientes"), new("Clientes")]`,
   `NuevoHref="@RetornoLocal.ConRetorno("/clientes/nuevo", BuildListUrl())"`, `SeleccionId="@SeleccionValida?.ToString()"`,
   `EditarUrl="@(id => RetornoLocal.ConRetorno($"/clientes/{id}/editar", BuildListUrl()))"`,
   `EliminarUrl="@(id => BuildListUrl(deleteId: Guid.Parse(id)))"`, y como `ChildContent` los botones que ya existían
   (vista cuadrícula/lista, Reporte, Panel, Excel) **sin** el antiguo "Nuevo". En Productos, lo mismo con `/productos…` y
   migas `[new("Productos", "/modulos/productos"), new("Productos")]`.
3. Vista lista: primera celda `<SeleccionFila Href="@BuildListUrl(sel: cliente.Id)" Seleccionada="@(cliente.Id == SeleccionValida)" Etiqueta="@cliente.Codigo" />`
   **junto** al checkbox de reporte existente (el checkbox se conserva: sirve a "Reporte de seleccionados"); resaltado de fila;
   el enlace ✎ de la fila apunta a `RetornoLocal.ConRetorno($"/clientes/{cliente.Id}/editar", BuildListUrl())`.
4. Vista tarjetas: sustituir el grupo de iconos 👁 ✎ 🗑 de cada tarjeta por `@TarjetaAccionesDe(cliente)` (el checkbox de
   reporte se conserva) con:

```csharp
    private RenderFragment TarjetaAccionesDe(SocioNegocioResponse c) => __builder =>
    {
        <TarjetaAcciones Id="@c.Id.ToString()"
                         Rapidas="@([new AccionPagina("Ficha", IconosModulo.Persona, id => $"/clientes/{id}"),
                                    new AccionPagina("Editar", IconosModulo.Lista, id => RetornoLocal.ConRetorno($"/clientes/{id}/editar", BuildListUrl()), ApplicationPolicies.CanModify)])"
                         Resto="@([new AccionPagina("Eliminar", IconosModulo.Lista, id => BuildListUrl(deleteId: Guid.Parse(id!)), ApplicationPolicies.CanDelete)])" />
    };
```

   (en Productos: `ProductoResponse`, `IconosModulo.Cubo`, rutas `/productos/…`).
5. `BuildListUrl`: el parámetro `focusId` pasa a `sel` (`Guid? sel = null`, por defecto `Sel`).
6. Añadir `private Guid? SeleccionValida => Sel is { } s && clientes.Any(c => c.Id == s) ? s : null;` (Productos: `productos`).

- [ ] **Step 7: Ejecutar los tests del lote**

Run: `dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~ConversionClientesProductosTests|FullyQualifiedName~ProductoFormularioTests|FullyQualifiedName~RegistroModulosTests"`
Expected: PASS.

- [ ] **Step 8: Build sin avisos**

Run: `dotnet build test.slnx -warnaserror`
Expected: 0 avisos.

- [ ] **Step 9: Commit**

```bash
git add src/OpenSource1.Blazor/Components/Pages/ClienteEditor.razor src/OpenSource1.Blazor/Components/Pages/ProductoEditor.razor src/OpenSource1.Blazor/Components/Pages/ClienteNew.razor src/OpenSource1.Blazor/Components/Pages/ProductoNew.razor src/OpenSource1.Blazor/Components/Pages/ClienteDetail.razor src/OpenSource1.Blazor/Components/Pages/ProductoDetail.razor src/OpenSource1.Blazor/Components/Pages/Clientes.razor src/OpenSource1.Blazor/Components/Pages/Productos.razor tests/OpenSource1.SmokeTests/Blazor/ConversionClientesProductosTests.cs
git commit -m "feat: clientes y productos con alta y edicion en pagina propia, seleccion y rutas antiguas redirigidas"
```

---

### Task B2d: Lote 4 — Documentos y usuarios (Diarios, Borradores de factura, Borradores de nota de crédito, Usuarios)

**Grupo de paralelismo:** B2 — **en paralelo con B2a, B2b y B2c**. Mismas prohibiciones de archivos compartidos.

**Files:**
- Create: `src/OpenSource1.Blazor/Components/Pages/LoteDiarioEditor.razor`
- Create: `src/OpenSource1.Blazor/Components/Pages/FacturaVentaNueva.razor`
- Create: `src/OpenSource1.Blazor/Components/Pages/FacturaVentaBorradorCabecera.razor`
- Create: `src/OpenSource1.Blazor/Components/Pages/NotaCreditoBorradorCabecera.razor`
- Create: `src/OpenSource1.Blazor/Components/Pages/UsuarioNuevo.razor`
- Modify: `src/OpenSource1.Blazor/Components/Pages/DiariosInventario.razor`
- Modify: `src/OpenSource1.Blazor/Components/Pages/FacturasVentaBorradores.razor`
- Modify: `src/OpenSource1.Blazor/Components/Pages/NotasCreditoVentaBorradores.razor`
- Modify: `src/OpenSource1.Blazor/Components/Pages/UserManagement.razor`
- Modify: `src/OpenSource1.Blazor/Components/Pages/UserProfile.razor` (segunda ruta `/admin/users/{UserId}/editar`)
- Test: `tests/OpenSource1.SmokeTests/Blazor/ConversionDocumentosUsuariosTests.cs`

**Interfaces:**
- Consumes: B1; `IDiarioInventarioApiClient` (`ListPlantillasAsync`, `GetLoteByIdAsync`, `CreateLoteAsync` → `LoteDiarioOperationResult(Succeeded, Message, LoteDiarioResponse? Valor, Errors)`, `UpdateLoteAsync(id, input, xmin)`), `IFacturaVentaApiClient` (`CreateBorradorAsync`, `GetBorradorAsync`, `UpdateBorradorAsync(id, input, xmin)`), `ISocioNegocioApiClient`, `IAlmacenApiClient`, `VentasOpciones.SociosAsync/AlmacenesAsync`, `INotaCreditoVentaApiClient` (`GetBorradorAsync`, `UpdateBorradorAsync(id, fechaRegistro, fechaDocumento, descripcion, xmin)`), `IUserAdminApiClient.CreateAsync(email, fullName, password)`.
- Produces (C2 y C3 los amplían):
  - `/diarios-inventario/nuevo` y `/diarios-inventario/{id}/editar` (`LoteDiarioEditor`; tras crear navega a `RetornoLocal.ConParametro(Volver, "ok", "created")`; C2 añade `?productoId=`).
  - `/facturas-venta/nueva` (`FacturaVentaNueva`; formulario `add-borrador`, modelo `FacturasVentaBorradores.BorradorForm`, búsqueda de socio con `socioQuery`; tras crear navega a `/facturas-venta/borradores/{id}?ok=creado`; C3 añade `?socioId=`).
  - `/facturas-venta/borradores/{BorradorId:guid}/editar` (`FacturaVentaBorradorCabecera`; formulario `update-borrador`).
  - `/notas-credito-venta/borradores/{BorradorId:guid}/editar` (`NotaCreditoBorradorCabecera`; formulario `update-borrador-nota`, modelo `NotasCreditoVentaBorradores.CabeceraNotaForm`).
  - `/admin/users/nuevo` (`UsuarioNuevo`; formulario `create-user` con inputs `name="CreateInput.*"`, sin `@bind`) y `/admin/users/{UserId}/editar` (alias de la ficha de usuario).
  - Rutas antiguas: `/diarios-inventario?editId=` → `/diarios-inventario/{id}/editar`; `/facturas-venta/borradores?editId=` → `/facturas-venta/borradores/{id}/editar`; `/notas-credito-venta/borradores?editId=` → `/notas-credito-venta/borradores/{id}/editar`; `/admin/users?new=true` → `/admin/users/nuevo`.

- [ ] **Step 1: Tests del lote (fallan)**

`tests/OpenSource1.SmokeTests/Blazor/ConversionDocumentosUsuariosTests.cs`:

```csharp
extern alias BlazorApp;

using System.Net;
using BlazorApp::OpenSource1.Blazor.Services;
using Moq;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Dtos;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>Fix-Features B2d: lotes de diario, cabeceras de borradores y alta de usuarios en página-tarjeta propia.</summary>
public sealed class ConversionDocumentosUsuariosTests
{
    [Theory]
    [InlineData("/diarios-inventario/nuevo", "save-lote-diario")]
    [InlineData("/facturas-venta/nueva", "add-borrador")]
    [InlineData("/admin/users/nuevo", "create-user")]
    public async Task Nuevo_RenderizaFormularioEnTarjeta(string ruta, string formName)
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlAsync(app.Cliente(), ruta);

        Assert.Contains("data-testid=\"entity-form-page\"", html);
        Assert.Contains($"name=\"_handler\" value=\"{formName}\"", html);
    }

    [Theory]
    [InlineData("/diarios-inventario?editId={0}", "/diarios-inventario/{0}/editar")]
    [InlineData("/facturas-venta/borradores?editId={0}", "/facturas-venta/borradores/{0}/editar")]
    [InlineData("/notas-credito-venta/borradores?editId={0}", "/notas-credito-venta/borradores/{0}/editar")]
    public async Task EditIdLegado_RedirigeALaRutaNueva(string origen, string destino)
    {
        using var app = Configurar(new BlazorSsrFactory());
        var id = Guid.NewGuid();

        var respuesta = await app.Cliente().GetAsync(string.Format(origen, id));

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal(string.Format(destino, id), FormulariosSsr.Destino(respuesta));
    }

    [Fact]
    public async Task UsuariosNewTrue_RedirigeANuevo_YEditarEsAliasDeLaFicha()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var legado = await app.Cliente().GetAsync("/admin/users?new=true");
        var editar = await app.Cliente().GetAsync("/admin/users/u-1/editar");
        var nuevo = await HtmlAsync(app.Cliente(), "/admin/users/nuevo");

        Assert.Equal("/admin/users/nuevo", FormulariosSsr.Destino(legado));
        Assert.Equal(HttpStatusCode.OK, editar.StatusCode);
        Assert.Contains("name=\"CreateInput.Email\"", nuevo);
        Assert.DoesNotContain("@bind", nuevo);
    }

    [Fact]
    public async Task LoteDiario_AltaRedirigeConOk()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var plantilla = Guid.NewGuid();
        app.Simular<IDiarioInventarioApiClient>()
            .Setup(c => c.CreateLoteAsync(It.Is<LoteDiarioInput>(i => i.Codigo == "AJ-01" && i.PlantillaDiarioId == plantilla), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoteDiarioOperationResult(true, "ok", new LoteDiarioResponse { Id = Guid.NewGuid(), Codigo = "AJ-01" }));

        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), "/diarios-inventario/nuevo", "save-lote-diario",
            new Dictionary<string, string> { ["SaveInput.PlantillaDiarioId"] = plantilla.ToString(), ["SaveInput.Codigo"] = "AJ-01", ["SaveInput.Nombre"] = "Ajustes" });

        Assert.Equal("/diarios-inventario?ok=created", FormulariosSsr.Destino(respuesta));
    }

    [Theory]
    [InlineData("/diarios-inventario", "save-lote-diario")]
    [InlineData("/facturas-venta/borradores", "add-borrador")]
    [InlineData("/notas-credito-venta/borradores", "update-borrador-nota")]
    [InlineData("/admin/users", "create-user")]
    public async Task Listado_SinFormulariosEnLinea(string ruta, string formName)
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlAsync(app.Cliente(), ruta);

        Assert.Contains("data-testid=\"page-toolbar\"", html);
        Assert.DoesNotContain($"value=\"{formName}\"", html);
    }

    private static BlazorSsrFactory Configurar(BlazorSsrFactory app)
    {
        app.Simular<IDiarioInventarioApiClient>().Setup(c => c.ListPlantillasAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        app.Simular<IFacturaVentaApiClient>();
        app.Simular<INotaCreditoVentaApiClient>();
        app.Simular<ISocioNegocioApiClient>();
        app.Simular<IAlmacenApiClient>();
        app.Simular<IUserAdminApiClient>();
        return app;
    }

    private static async Task<string> HtmlAsync(HttpClient cliente, string url)
    {
        var respuesta = await cliente.GetAsync(url);
        var html = await respuesta.Content.ReadAsStringAsync();
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, $"GET {url}: {respuesta.StatusCode}\n{html}");
        return html;
    }
}
```

(`IUserAdminApiClient.ListUsersAsync` sin `Setup` devuelve `default` (tupla con nulos): la página muestra su estado vacío.)

- [ ] **Step 2: Ejecutar y ver que fallan**

Run: `dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~ConversionDocumentosUsuariosTests"`
Expected: FAIL.

- [ ] **Step 3: `LoteDiarioEditor.razor`**

`src/OpenSource1.Blazor/Components/Pages/LoteDiarioEditor.razor`:

```razor
@page "/diarios-inventario/nuevo"
@page "/diarios-inventario/{Id:guid}/editar"
@attribute [Authorize(Policy = ApplicationPolicies.CanConsult)]
@using Microsoft.AspNetCore.Authorization
@using OpenSource1.Application.Features.DiariosInventario.Plantillas.Dtos
@using OpenSource1.Application.Security
@using OpenSource1.Blazor.Services
@inject IDiarioInventarioApiClient DiarioApiClient
@inject IAuthorizationService AuthorizationService
@inject ILogger<LoteDiarioEditor> Logger
@inject NavigationManager Nav

<EntityFormPage Titulo="@(EsEdicion ? "Modificar lote" : "Nuevo lote de diario")" Subtitulo="Lotes de ajustes y transferencias de inventario."
                Migas="@Migas" Mensaje="@message" TipoMensaje="@messageType" Errores="@errores">
    @if (!Permitido)
    {
        <MessageBox Type="warning" Message="No tiene permiso para realizar esta acción." />
    }
    else if (plantillasCargaFallida)
    {
        <MessageBox Type="danger" Message="No se puede agregar ni modificar un lote hasta que las plantillas carguen; recargue la página." />
    }
    else if (EsEdicion && !encontrado)
    {
        <MessageBox Type="warning" Message="No se encontró el lote seleccionado. Puede haber sido eliminado por otro usuario." />
    }
    else if (EsEdicion)
    {
        <EditForm Model="UpdateInput" FormName="update-lote-diario" OnValidSubmit="UpdateAsync" Enhance="true">
            <DataAnnotationsValidator />
            <input type="hidden" name="UpdateInput.Id" value="@UpdateInput!.Id" />
            <input type="hidden" name="UpdateInput.PlantillaDiarioId" value="@UpdateInput.PlantillaDiarioId" />
            <input type="hidden" name="UpdateInput.Xmin" value="@UpdateInput.Xmin" />
            <LoteDiarioFields Model="UpdateInput!" Prefix="UpdateInput" Plantillas="@plantillas" EsEdicion="true" />
            <FormularioAcciones CancelarHref="@Volver" LimpiarHref="@Nav.Uri" />
        </EditForm>
    }
    else
    {
        <EditForm Model="SaveInput" FormName="save-lote-diario" OnValidSubmit="CreateAsync" Enhance="true">
            <DataAnnotationsValidator />
            <LoteDiarioFields Model="SaveInput!" Prefix="SaveInput" Plantillas="@plantillas" EsEdicion="false" />
            <FormularioAcciones CancelarHref="@Volver" LimpiarHref="@Nav.Uri" />
        </EditForm>
    }
</EntityFormPage>

@if (!(!EsEdicion && canAdd && !plantillasCargaFallida))
{
    <EditForm Model="SaveInput" FormName="save-lote-diario" OnValidSubmit="CreateAsync"></EditForm>
}
@if (!(EsEdicion && canModify && encontrado && !plantillasCargaFallida))
{
    <EditForm Model="UpdateInput" FormName="update-lote-diario" OnValidSubmit="UpdateAsync"></EditForm>
}

@code {
    [Parameter] public Guid? Id { get; set; }
    [SupplyParameterFromQuery(Name = "returnUrl")] private string? ReturnUrl { get; set; }
    [SupplyParameterFromForm(FormName = "save-lote-diario")] private DiariosInventario.LoteForm? SaveInput { get; set; }
    [SupplyParameterFromForm(FormName = "update-lote-diario")] private DiariosInventario.LoteForm? UpdateInput { get; set; }
    [CascadingParameter] private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    private IReadOnlyList<PlantillaDiarioResponse> plantillas = [];
    private bool plantillasCargaFallida;
    private bool canAdd;
    private bool canModify;
    private bool encontrado;
    private string? message;
    private string messageType = "info";
    private IReadOnlyList<string> errores = [];

    private bool EsEdicion => Id.HasValue;
    private bool Permitido => EsEdicion ? canModify : canAdd;
    private string Volver => RetornoLocal.Validar(ReturnUrl, "/diarios-inventario");
    private IReadOnlyList<Miga> Migas => [new("Inventario", "/modulos/inventario"), new("Diarios de inventario", Volver), new(EsEdicion ? "Modificar" : "Nuevo")];

    protected override async Task OnInitializedAsync()
    {
        SaveInput ??= new();
        UpdateInput ??= new();
        if (AuthenticationStateTask is not null)
        {
            var user = (await AuthenticationStateTask).User;
            canAdd = (await AuthorizationService.AuthorizeAsync(user, ApplicationPolicies.CanAdd)).Succeeded;
            canModify = (await AuthorizationService.AuthorizeAsync(user, ApplicationPolicies.CanModify)).Succeeded;
        }

        if (!Permitido)
        {
            return;
        }

        try
        {
            plantillas = await DiarioApiClient.ListPlantillasAsync();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not load plantillas de diario from API.");
            plantillasCargaFallida = true;
        }

        if (Id is { } id)
        {
            try
            {
                var lote = await DiarioApiClient.GetLoteByIdAsync(id);
                encontrado = lote is not null;
                if (lote is not null && UpdateInput.Id == Guid.Empty)
                {
                    UpdateInput.Id = lote.Id;
                    UpdateInput.PlantillaDiarioId = lote.PlantillaDiarioId;
                    UpdateInput.Codigo = lote.Codigo;
                    UpdateInput.Nombre = lote.Nombre;
                    UpdateInput.Bloqueado = lote.Bloqueado;
                    UpdateInput.Xmin = lote.Xmin;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Could not load lote de diario {Id} from API.", id);
                Show("danger", "No fue posible cargar el lote a modificar.");
            }
        }
    }

    private async Task CreateAsync()
    {
        if (SaveInput!.PlantillaDiarioId == Guid.Empty)
        {
            Show("warning", "Seleccione una plantilla.");
            return;
        }

        await ExecuteAsync(() => DiarioApiClient.CreateLoteAsync(new LoteDiarioInput(SaveInput.PlantillaDiarioId, SaveInput.Codigo.Trim(), SaveInput.Nombre.Trim(), SaveInput.Bloqueado)), "created");
    }

    private async Task UpdateAsync()
    {
        if (Id is not { } id)
        {
            Show("warning", "Seleccione primero un registro a modificar desde la tabla.");
            return;
        }

        await ExecuteAsync(() => DiarioApiClient.UpdateLoteAsync(
            id, new LoteDiarioInput(UpdateInput!.PlantillaDiarioId, UpdateInput.Codigo.Trim(), UpdateInput.Nombre.Trim(), UpdateInput.Bloqueado), UpdateInput.Xmin), "updated");
    }

    private async Task ExecuteAsync(Func<Task<LoteDiarioOperationResult>> operation, string successCode)
    {
        try
        {
            var result = await operation();
            if (result.Succeeded)
            {
                Nav.NavigateTo(DestinoTrasGuardar(result, successCode));
            }
            else
            {
                errores = result.Errors ?? [];
                Show("warning", result.Message);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not complete lote de diario operation.");
            Show("danger", "No fue posible completar la operación.");
        }
    }

    // C2 amplía este método para abrir el lote con la línea del producto (?productoId=).
    private string DestinoTrasGuardar(LoteDiarioOperationResult result, string successCode) =>
        RetornoLocal.ConParametro(Volver, "ok", successCode);

    private void Show(string type, string text) { messageType = type; message = text; }
}
```

`DiariosInventario.razor`: aplicar la receta (cabecera → `<PageToolbar Titulo="Diarios de inventario" Subtitulo="Lotes de ajustes y transferencias de inventario." Migas="@([new("Inventario", "/modulos/inventario"), new("Diarios de inventario")])" NuevoHref="@RetornoLocal.ConRetorno("/diarios-inventario/nuevo", BuildListUrl())" SeleccionId="@SeleccionValida?.ToString()" EditarUrl="@(id => RetornoLocal.ConRetorno($"/diarios-inventario/{id}/editar", BuildListUrl()))" EliminarUrl="@(id => BuildListUrl(deleteId: Guid.Parse(id)))" />`, borrar la sección de formularios,
`SaveInput`/`UpdateInput`/`CreateAsync`/`UpdateAsync` y la carga de `EditId`; `plantillas` se queda si el filtro por
plantilla la usa; `?sel=` con `lotes`; redirección legado a `/diarios-inventario/{id}/editar`; en la tabla, el enlace de
edición de la fila a `RetornoLocal.ConRetorno($"/diarios-inventario/{lote.Id}/editar", BuildListUrl())`; `DeleteAsync` y su
navegación se conservan).

- [ ] **Step 4: `FacturaVentaNueva.razor` y `FacturaVentaBorradorCabecera.razor`**

`src/OpenSource1.Blazor/Components/Pages/FacturaVentaNueva.razor`:

```razor
@page "/facturas-venta/nueva"
@attribute [Authorize(Policy = ApplicationPolicies.CanConsult)]
@using Microsoft.AspNetCore.Authorization
@using OpenSource1.Application.Features.Almacenes.Dtos
@using OpenSource1.Application.Features.SociosNegocio.Dtos
@using OpenSource1.Application.Security
@using OpenSource1.Blazor.Services
@inject IFacturaVentaApiClient FacturaApiClient
@inject ISocioNegocioApiClient SocioApiClient
@inject IAlmacenApiClient AlmacenApiClient
@inject IAuthorizationService AuthorizationService
@inject ILogger<FacturaVentaNueva> Logger
@inject NavigationManager Nav

<EntityFormPage Titulo="Nueva factura" Subtitulo="Crea el borrador de factura; las líneas se agregan en el editor del borrador."
                Migas="@Migas" Mensaje="@message" TipoMensaje="@messageType" Errores="@errores">
    @if (!canAdd)
    {
        <MessageBox Type="warning" Message="No tiene permiso para realizar esta acción." />
    }
    else
    {
        @if (OpcionesFallidas)
        {
            <div class="mb-4"><MessageBox Type="danger" Message="No fue posible cargar los clientes o los almacenes. Por seguridad no se puede crear el borrador hasta que carguen; recargue la página." /></div>
        }
        <form method="get" action="/facturas-venta/nueva" data-enhance class="mb-6 flex flex-wrap items-end gap-2" data-testid="buscar-socio">
            <input type="hidden" name="returnUrl" value="@ReturnUrl" />
            <div class="min-w-60 flex-1">
                <label class="mb-1 block text-xs font-semibold text-slate-500" for="socioQuery">Buscar cliente por nombre</label>
                <input id="socioQuery" type="text" name="socioQuery" value="@SocioQuery" placeholder="ej. Comercial" class="@VentaCss.Filtro" />
            </div>
            <button type="submit" class="btn-press rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-600 shadow-sm hover:bg-slate-50">Buscar</button>
            <p class="w-full text-xs text-slate-400">Se muestran los primeros @VentasOpciones.MaximoSocios clientes por nombre@(string.IsNullOrWhiteSpace(SocioQuery) ? "" : $" que contienen «{SocioQuery}»").</p>
        </form>
        <EditForm Model="AddInput" FormName="add-borrador" OnValidSubmit="CreateAsync" Enhance="true">
            <DataAnnotationsValidator />
            @CamposBorrador(AddInput!)
            <FormularioAcciones CancelarHref="@Volver" LimpiarHref="@Nav.Uri" TextoGuardar="Crear borrador" Deshabilitado="@OpcionesFallidas" />
        </EditForm>
    }
</EntityFormPage>

@if (!canAdd)
{
    <EditForm Model="AddInput" FormName="add-borrador" OnValidSubmit="CreateAsync"></EditForm>
}

@code {
    [SupplyParameterFromQuery(Name = "returnUrl")] private string? ReturnUrl { get; set; }
    [SupplyParameterFromQuery(Name = "socioQuery")] private string? SocioQuery { get; set; }
    [SupplyParameterFromForm(FormName = "add-borrador")] private FacturasVentaBorradores.BorradorForm? AddInput { get; set; }
    [CascadingParameter] private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    private OpcionesCargadas<SocioNegocioResponse> socios = OpcionesCargadas<SocioNegocioResponse>.SinCargar;
    private OpcionesCargadas<AlmacenResponse> almacenes = OpcionesCargadas<AlmacenResponse>.SinCargar;
    private bool canAdd;
    private string? message;
    private string messageType = "info";
    private IReadOnlyList<string> errores = [];

    private bool OpcionesFallidas => socios.CargaFallida || almacenes.CargaFallida;
    private string Volver => RetornoLocal.Validar(ReturnUrl, "/facturas-venta/borradores");
    private IReadOnlyList<Miga> Migas => [new("Facturación", "/modulos/facturacion"), new("Borradores de factura", Volver), new("Nueva factura")];

    protected override async Task OnInitializedAsync()
    {
        AddInput ??= new() { FechaRegistroTexto = EntradaFecha.Formatear(DateOnly.FromDateTime(DateTime.Today)) };
        if (AuthenticationStateTask is not null)
        {
            canAdd = (await AuthorizationService.AuthorizeAsync((await AuthenticationStateTask).User, ApplicationPolicies.CanAdd)).Succeeded;
        }

        if (canAdd)
        {
            socios = await VentasOpciones.SociosAsync(SocioApiClient, SocioQuery, Logger);
            almacenes = await VentasOpciones.AlmacenesAsync(AlmacenApiClient, Logger);
        }
    }

    private async Task CreateAsync()
    {
        if (OpcionesFallidas)
        {
            Show("danger", "No fue posible cargar los clientes o los almacenes; recargue la página.");
            return;
        }

        var f = AddInput!;
        var input = new BorradorCabeceraInput(
            f.SocioNegocioId ?? Guid.Empty, f.SocioNegocioFacturarAId, f.FechaRegistro, f.FechaDocumento, f.FechaVencimiento, f.AlmacenId,
            string.IsNullOrWhiteSpace(f.Descripcion) ? null : f.Descripcion.Trim());
        try
        {
            var result = await FacturaApiClient.CreateBorradorAsync(input);
            if (result.Succeeded)
            {
                Nav.NavigateTo($"/facturas-venta/borradores/{result.Valor!.Id}?ok=creado");
                return;
            }

            errores = result.Errors ?? [];
            Show("warning", result.Message);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not create borrador de factura.");
            Show("danger", "No fue posible crear el borrador.");
        }
    }

    private void Show(string type, string text) { messageType = type; message = text; }
}
```

- `@CamposBorrador(AddInput!)`: **mover** a este archivo el `RenderFragment Campos(BorradorForm model, string prefix, FacturaVentaBorradorResponse? vigente)`
  de `FacturasVentaBorradores.razor` (líneas 354-422) y `OpcionSocioVigente` (líneas 425-431), renombrando `Campos` →
  `CamposBorrador(BorradorForm model)` con `prefix` fijo `"AddInput"` y `vigente` fijo `null` (se eliminan sus ramas de
  edición), junto con la constante `InputCss`. Para los filtros de búsqueda se crea en el mismo archivo
  `private static class VentaCss { public const string Filtro = "<valor literal de FiltroCss de FacturasVentaBorradores.razor>"; }`
  copiando el literal de `FiltroCss` (línea 244).

`src/OpenSource1.Blazor/Components/Pages/FacturaVentaBorradorCabecera.razor`: página `@page "/facturas-venta/borradores/{BorradorId:guid}/editar"`
con la misma estructura (injects `IFacturaVentaApiClient`, `ISocioNegocioApiClient`, `IAlmacenApiClient`, `IAuthorizationService`,
`ILogger<FacturaVentaBorradorCabecera>`, `NavigationManager`), `[SupplyParameterFromForm(FormName = "update-borrador")] FacturasVentaBorradores.BorradorForm? UpdateInput`,
el formulario de búsqueda de socio (con `returnUrl` oculto), el aviso de borrador liberado, `<input type="hidden" name="UpdateInput.Xmin" value="@UpdateInput!.Xmin" />`,
un `RenderFragment CamposCabecera(BorradorForm model, FacturaVentaBorradorResponse vigente)` = el `Campos` original con
`prefix` fijo `"UpdateInput"`, y **movidos** de `FacturasVentaBorradores.razor`: el bloque de carga `if (EditId.HasValue) { … }`
(líneas 305-328, con `BorradorId` en lugar de `EditId`) y `UpdateAsync` (líneas 456-497). `CanModify` requerido
(sin él, mensaje de permiso + `EditForm` vacío `update-borrador`); `Volver = RetornoLocal.Validar(ReturnUrl, "/facturas-venta/borradores")`;
tras guardar navega (como hoy) a `/facturas-venta/borradores/{BorradorId}?ok=modificado`.

`FacturasVentaBorradores.razor`: cabecera → `<PageToolbar Titulo="Borradores de factura" Subtitulo="Facturas de venta en preparación: cabecera, líneas, liberación y posteo." Migas="@([new("Facturación", "/modulos/facturacion"), new("Borradores de factura")])" NuevoHref="@RetornoLocal.ConRetorno("/facturas-venta/nueva", BuildListUrl())" NuevoTexto="Nueva factura" SeleccionId="@SeleccionValida?.ToString()" EditarUrl="@(id => RetornoLocal.ConRetorno($"/facturas-venta/borradores/{id}/editar", BuildListUrl()))" EliminarUrl="@(id => BuildListUrl(deleteId: Guid.Parse(id)))"><a href="/facturas-venta" class="btn-press rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm font-semibold text-slate-600 hover:bg-slate-50">Facturas posteadas</a></PageToolbar>`;
borrar el formulario de búsqueda de socios para selectores, la rejilla Agregar/Modificar, `AddInput`, `UpdateInput`,
`CreateAsync`, `UpdateAsync`, `Campos`, `OpcionSocioVigente`, los `EditForm` vacíos de `add-borrador`/`update-borrador`,
la carga de `socios`/`almacenes` y de `EditId`; conservar el diálogo y el `EditForm` vacío de `delete-borrador`; la
selección válida solo para borradores `Abierta` (`SeleccionValida => Sel is { } s && borradores.Any(b => b.Id == s && b.Estado == EstadoFacturaBorrador.Abierta) ? s : null`);
el enlace "Modificar" de la fila a la ruta nueva con `returnUrl`; `BorradorForm` y `DeleteForm` se quedan como clases
públicas anidadas. Redirección legado del `editId`.

- [ ] **Step 5: `NotaCreditoBorradorCabecera.razor` y `NotasCreditoVentaBorradores.razor`**

`src/OpenSource1.Blazor/Components/Pages/NotaCreditoBorradorCabecera.razor`: `@page "/notas-credito-venta/borradores/{BorradorId:guid}/editar"`,
estructura de `LoteDiarioEditor` (Step 3) sin alta: solo el formulario `update-borrador-nota` con modelo
`NotasCreditoVentaBorradores.CabeceraNotaForm`, **movidos** de `NotasCreditoVentaBorradores.razor` su marcado (la tarjeta
"Modificar" con los campos de fechas/descripción y el `Xmin` oculto), el bloque de carga del `EditId` (con `BorradorId`),
y `UpdateAsync` (líneas 315-340; navega como hoy a `/notas-credito-venta/borradores/{BorradorId}?ok=modificado`); migas
`[new("Facturación", "/modulos/facturacion"), new("Borradores de nota de crédito", Volver), new("Modificar cabecera")]`;
`Volver = RetornoLocal.Validar(ReturnUrl, "/notas-credito-venta/borradores")`; sin `CanModify`, mensaje de permiso +
`EditForm` vacío con el mismo `FormName`.

`NotasCreditoVentaBorradores.razor`: cabecera → `<PageToolbar Titulo="Borradores de nota de crédito" Migas="@([new("Facturación", "/modulos/facturacion"), new("Borradores de nota de crédito")])" SeleccionId="@SeleccionValida?.ToString()" EditarUrl="@(id => RetornoLocal.ConRetorno($"/notas-credito-venta/borradores/{id}/editar", BuildListUrl()))" EliminarUrl="@(id => BuildListUrl(deleteId: Guid.Parse(id)))" />`
(sin `NuevoHref`: el alta de notas se hace desde una factura; C3 añade `+ Nueva nota de crédito`); borrar el formulario de
modificación en línea, el `EditForm` vacío `update-borrador-nota` del final del archivo (línea 202) y lo movido; `[SupplyParameterFromQuery(Name = "sel")] private Guid? Sel` con `private Guid? SeleccionValida => Sel is { } s && borradores.Any(b => b.Id == s) ? s : null;`; `BuildListUrl` sin `editId` y con `sel`; redirección legado del `editId`.

- [ ] **Step 6: `UsuarioNuevo.razor`, `UserManagement.razor` y alias de edición**

`src/OpenSource1.Blazor/Components/Pages/UsuarioNuevo.razor`:

```razor
@page "/admin/users/nuevo"
@attribute [Authorize(Roles = ApplicationRoles.Administrator)]
@using System.ComponentModel.DataAnnotations
@using Microsoft.AspNetCore.Authorization
@using OpenSource1.Application.Security
@using OpenSource1.Blazor.Services
@inject IUserAdminApiClient UserAdminApi
@inject NavigationManager Nav

<EntityFormPage Titulo="Nuevo usuario" Subtitulo="Complete los datos para crear una cuenta. Podrá asignarle un rol luego desde su perfil."
                Migas="@Migas" Mensaje="@createError" TipoMensaje="danger">
    <EditForm Model="CreateInput" FormName="create-user" OnValidSubmit="CreateUserAsync" Enhance="true" class="space-y-4">
        <DataAnnotationsValidator />
        <div>
            <label class="mb-1 block text-sm font-medium text-slate-700 dark:text-slate-300" for="CreateInput_FullName">Nombre completo</label>
            <input id="CreateInput_FullName" name="CreateInput.FullName" value="@CreateInput!.FullName" class="@InputCss" />
            <ValidationMessage For="() => CreateInput!.FullName" />
        </div>
        <div>
            <label class="mb-1 block text-sm font-medium text-slate-700 dark:text-slate-300" for="CreateInput_Email">Correo electrónico</label>
            <input id="CreateInput_Email" type="email" name="CreateInput.Email" value="@CreateInput.Email" class="@InputCss" />
            <ValidationMessage For="() => CreateInput!.Email" />
        </div>
        <div>
            <label class="mb-1 block text-sm font-medium text-slate-700 dark:text-slate-300" for="CreateInput_Password">Contraseña</label>
            <input id="CreateInput_Password" type="password" name="CreateInput.Password" autocomplete="new-password" class="@InputCss" />
            <ValidationMessage For="() => CreateInput!.Password" />
        </div>
        <FormularioAcciones CancelarHref="/admin/users" LimpiarHref="/admin/users/nuevo" TextoGuardar="Crear usuario" />
    </EditForm>
</EntityFormPage>

@code {
    private const string InputCss = "block w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-800 shadow-sm focus:border-brand-600 focus:outline-none focus:ring-2 focus:ring-brand-600/20 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-200";
    private static readonly IReadOnlyList<Miga> Migas = [new("Administración", "/modulos/administracion"), new("Usuarios", "/admin/users"), new("Nuevo")];

    [SupplyParameterFromForm(FormName = "create-user")] private CreateUserModel? CreateInput { get; set; }

    private string? createError;

    protected override void OnInitialized() => CreateInput ??= new();

    private async Task CreateUserAsync()
    {
        var input = CreateInput!;
        var (success, error) = await UserAdminApi.CreateAsync(input.Email, input.FullName, input.Password);
        if (success)
        {
            Nav.NavigateTo($"/admin/users?ok={Uri.EscapeDataString("Usuario creado satisfactoriamente.")}");
        }
        else
        {
            createError = error ?? "No se pudo crear el usuario.";
        }
    }

    public sealed class CreateUserModel
    {
        [Required(ErrorMessage = "El nombre completo es requerido.")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo es requerido.")]
        [EmailAddress(ErrorMessage = "Ingrese un correo válido.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es requerida.")]
        [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
        public string Password { get; set; } = string.Empty;
    }
}
```

`UserManagement.razor`: sustituir el encabezado y el enlace `/admin/users?new=true` por
`<PageToolbar Titulo="Gestión de Usuarios" Subtitulo="Busque, filtre y administre roles y estado de los usuarios." Migas="@([new("Administración", "/modulos/administracion"), new("Usuarios")])" NuevoHref="/admin/users/nuevo" NuevoTexto="Nuevo usuario" NuevoPermiso="@null" />`;
borrar el `ConfirmDialog` de alta, `CreateInput`, `createError`, `CreateUserAsync`, `CreateUserModel`, `OnInitialized` y
la propiedad `IsNewUser` pasa a usarse solo para redirigir: primera instrucción de `OnInitializedAsync`
`if (IsNewUser) { Nav.NavigateTo("/admin/users/nuevo", replace: true); return; }`. En cada tarjeta, junto a "Ver Perfil",
añadir `<a href="/admin/users/@user.Id/editar" class="btn-press mt-2 flex w-full items-center justify-center rounded-lg border border-slate-300 bg-white px-4 py-2 text-xs font-semibold text-slate-600 hover:bg-slate-50">Editar</a>`.

`UserProfile.razor`: añadir debajo de `@page "/admin/users/{UserId}"` la línea `@page "/admin/users/{UserId}/editar"`
(la ficha del usuario ya es su página de edición: roles, estado y datos).

- [ ] **Step 7: Ejecutar los tests del lote y los de ventas existentes**

Run: `dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~ConversionDocumentosUsuariosTests|FullyQualifiedName~FacturaVentaApiClientTests|FullyQualifiedName~NotaCreditoVentaApiClientTests|FullyQualifiedName~LineaDiarioFormularioTests|FullyQualifiedName~RegistroModulosTests"`
Expected: PASS.

- [ ] **Step 8: Build sin avisos**

Run: `dotnet build test.slnx -warnaserror`
Expected: 0 avisos.

- [ ] **Step 9: Commit**

```bash
git add src/OpenSource1.Blazor/Components/Pages/LoteDiarioEditor.razor src/OpenSource1.Blazor/Components/Pages/FacturaVentaNueva.razor src/OpenSource1.Blazor/Components/Pages/FacturaVentaBorradorCabecera.razor src/OpenSource1.Blazor/Components/Pages/NotaCreditoBorradorCabecera.razor src/OpenSource1.Blazor/Components/Pages/UsuarioNuevo.razor src/OpenSource1.Blazor/Components/Pages/DiariosInventario.razor src/OpenSource1.Blazor/Components/Pages/FacturasVentaBorradores.razor src/OpenSource1.Blazor/Components/Pages/NotasCreditoVentaBorradores.razor src/OpenSource1.Blazor/Components/Pages/UserManagement.razor src/OpenSource1.Blazor/Components/Pages/UserProfile.razor tests/OpenSource1.SmokeTests/Blazor/ConversionDocumentosUsuariosTests.cs
git commit -m "feat: lotes, cabeceras de borradores y alta de usuarios en pagina propia con barra de acciones"
```

---

### Task G-B: Puerta de la Fase B (integración de lotes)

**Grupo de paralelismo:** serie (tras B2a–B2d).

- [ ] **Step 1: Integrar los lotes** (si se hicieron en worktrees: `git merge --no-ff` local de cada rama de lote en `Fix-Features`, o cherry-pick de sus commits; los conjuntos de archivos son disjuntos, no debe haber conflictos. Si los hay, es señal de que un lote tocó un archivo prohibido: revertir ese cambio en el lote).
- [ ] **Step 2: Suite completa y build**

Run: `dotnet build test.slnx -warnaserror && env DOCKER_CONTEXT=default dotnet test test.slnx`
Expected: 0 avisos; suite verde.

- [ ] **Step 3: Runtime** — `docker compose up -d --build api blazor`; recorrer como `admin` y `ejecutor`: cada listado de B2 → `+ Nuevo` abre la tarjeta, Cancelar vuelve con los filtros, Guardar vuelve con el aviso; selección de fila habilita Editar/Eliminar; `?editId=`, `/clientes/new` y `?edit=true` redirigen; el Ejecutor no ve Editar/Eliminar.
- [ ] **Step 4: Sin commit si no hubo arreglos.**

---
## Fase C — Acciones contextuales y documentos

Las tres tasks son **paralelas** entre sí (archivos disjuntos, indicados en cada una). Ninguna toca `MainLayout.razor`,
`NavMenu.razor`, `BarraBusqueda.razor`, `Navigation/*`, `Program.cs`, `App.razor`, `_Imports.razor` ni los componentes
de B1. No hacen falta filtros nuevos en la API: `GET api/facturas-venta` ya filtra por `socioId`
(`FacturaVentaSearchCriteria.SocioNegocioId`, ver `FacturasVentaConsultasApiTests.Listado_Paginado_Filtros_Orden_…`), y
movimientos de cliente, estado de cuenta, cobros, existencias y movimientos de producto/valor ya aceptan `socioId` /
`productoId` en sus páginas. El único filtro que falta está en la **UI** de Facturas (`?socioId=`), y lo añade C3.

### Task C1: Clientes — Crear ▾ / Ver ▾ en lista, tarjeta y ficha, y `/cobros/nuevo`

**Grupo de paralelismo:** C — en paralelo con C2 y C3.

**Files:**
- Create: `src/OpenSource1.Blazor/Components/AccionesCliente.cs`
- Create: `src/OpenSource1.Blazor/Components/Pages/CobroNuevo.razor`
- Modify: `src/OpenSource1.Blazor/Components/Pages/Clientes.razor`
- Modify: `src/OpenSource1.Blazor/Components/Pages/ClienteDetail.razor`
- Modify: `src/OpenSource1.Blazor/Components/Pages/Cobros.razor`
- Test: `tests/OpenSource1.SmokeTests/Blazor/AccionesClientesTests.cs`

**Interfaces:**
- Consumes: `PageToolbar`, `TarjetaAcciones`, `AccionPagina`, `RetornoLocal` (B1); `Clientes.SeleccionValida`, `Clientes.TarjetaAccionesDe` (B2c); `Cobros.PagoForm`, `VentasOpciones.SociosAsync/CuentasCapturaDirectaAsync`, `ICobroApiClient.RegistrarPagoAsync(RegistrarPagoInput)`.
- Produces: `static class AccionesCliente { IReadOnlyList<AccionPagina> Crear; IReadOnlyList<AccionPagina> Ver; }`; página `/cobros/nuevo?socioId=` (formulario `registrar-pago`, modelo `Cobros.PagoForm`); rutas de destino que C3 implementa: `/facturas-venta/nueva?socioId=`, `/notas-credito-venta/nueva?socioId=`, `/facturas-venta?socioId=`.

- [ ] **Step 1: Tests (fallan)**

`tests/OpenSource1.SmokeTests/Blazor/AccionesClientesTests.cs`:

```csharp
extern alias BlazorApp;

using System.Net;
using BlazorApp::OpenSource1.Blazor.Services;
using Moq;
using OpenSource1.Application.Features.Cobros;
using OpenSource1.Application.Features.CuentasContables.Dtos;
using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>Fix-Features C1: acciones contextuales de clientes (lista, tarjeta y ficha) y alta de cobro en página propia.</summary>
public sealed class AccionesClientesTests
{
    private static readonly Guid IdCliente = Guid.Parse("7d000000-0000-0000-0000-000000000001");
    private static readonly Guid IdCaja = Guid.Parse("7d000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task Lista_ConSeleccion_CrearYVerApuntanAlCliente()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlAsync(app.Cliente(), $"/clientes?view=list&sel={IdCliente}");

        foreach (var destino in new[]
                 {
                     $"/facturas-venta/nueva?socioId={IdCliente}", $"/notas-credito-venta/nueva?socioId={IdCliente}", $"/cobros/nuevo?socioId={IdCliente}",
                     $"/clientes/{IdCliente}", $"/ventas/movimientos-cliente?socioId={IdCliente}", $"/ventas/estado-cuenta?socioId={IdCliente}",
                     $"/cobros?socioId={IdCliente}", $"/facturas-venta?socioId={IdCliente}",
                 })
        {
            Assert.Contains($"href=\"{destino}\"", html);
        }
    }

    [Fact]
    public async Task Supervisor_NoVeFacturaNiNota_PeroSiCobro()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlAsync(app.Cliente("Supervisor"), $"/clientes?view=list&sel={IdCliente}");

        Assert.DoesNotContain($"href=\"/facturas-venta/nueva?socioId={IdCliente}\"", html);
        Assert.DoesNotContain($"href=\"/notas-credito-venta/nueva?socioId={IdCliente}\"", html);
        Assert.Contains($"href=\"/cobros/nuevo?socioId={IdCliente}\"", html);
    }

    [Fact]
    public async Task Tarjeta_YFicha_TienenLasMismasAcciones()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var tarjetas = await HtmlAsync(app.Cliente(), "/clientes?view=grid");
        var ficha = await HtmlAsync(app.Cliente(), $"/clientes/{IdCliente}");

        Assert.Contains($"href=\"/facturas-venta/nueva?socioId={IdCliente}\"", tarjetas);
        Assert.Contains("data-testid=\"page-toolbar\"", ficha);
        Assert.Contains($"href=\"/ventas/estado-cuenta?socioId={IdCliente}\"", ficha);
        Assert.Contains($"<a data-testid=\"accion-editar\" href=\"/clientes/{IdCliente}/editar?returnUrl=", ficha);
    }

    [Fact]
    public async Task CobroNuevo_PrecargaCliente_YAlGuardarVuelveACobros()
    {
        using var app = Configurar(new BlazorSsrFactory());
        app.Simular<ICobroApiClient>()
            .Setup(c => c.RegistrarPagoAsync(It.Is<RegistrarPagoInput>(i => i.SocioNegocioId == IdCliente && i.Importe == 500m), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VentaOperationResult<ResultadoPagoCliente>(true, "ok", new ResultadoPagoCliente("PAG-0001", 9, "CONTAB-1")));

        var html = await HtmlAsync(app.Cliente(), $"/cobros/nuevo?socioId={IdCliente}");
        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), $"/cobros/nuevo?socioId={IdCliente}", "registrar-pago",
            new Dictionary<string, string>
            {
                ["PagoInput.SocioNegocioId"] = IdCliente.ToString(), ["PagoInput.ImporteTexto"] = "500.00",
                ["PagoInput.FechaRegistroTexto"] = "2026-09-28", ["PagoInput.CuentaCajaId"] = IdCaja.ToString(),
            });

        Assert.Contains($"<option value=\"{IdCliente}\" selected", html);
        Assert.Equal($"/cobros?socioId={IdCliente}&ok=pago&numero=PAG-0001", FormulariosSsr.Destino(respuesta));
    }

    [Fact]
    public async Task Cobros_SinFormularioDeAltaEnLinea_ConNuevoCobro()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlAsync(app.Cliente(), $"/cobros?socioId={IdCliente}");

        Assert.DoesNotContain("id=\"registrar-pago\"", html);
        Assert.Contains($"href=\"/cobros/nuevo?socioId={IdCliente}\"", html);
        Assert.Contains("id=\"aplicar-pago\"", html);
    }

    private static BlazorSsrFactory Configurar(BlazorSsrFactory app)
    {
        var cliente = new SocioNegocioResponse { Id = IdCliente, Codigo = "C0001", NombreComercial = "Comercial Uno" };
        var socios = app.Simular<ISocioNegocioApiClient>();
        socios.Setup(c => c.ListAsync(It.IsAny<SocioNegocioSearchFilter?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<SocioNegocioResponse>([cliente], 1, 50, 1));
        socios.Setup(c => c.GetByIdAsync(IdCliente, It.IsAny<CancellationToken>())).ReturnsAsync(cliente);
        app.Simular<ICuentaContableApiClient>()
            .Setup(c => c.ListAsync(It.IsAny<CuentaContableSearchFilter?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<CuentaContableResponse>([new CuentaContableResponse { Id = IdCaja, Numero = "1101", Nombre = "Caja" }], 1, 200, 1));
        var cobros = app.Simular<ICobroApiClient>();
        cobros.Setup(c => c.ListMovimientosAbiertosAsync(IdCliente, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        app.Simular<ITerminoPagoApiClient>();
        return app;
    }

    private static async Task<string> HtmlAsync(HttpClient cliente, string url)
    {
        var respuesta = await cliente.GetAsync(url);
        var html = await respuesta.Content.ReadAsStringAsync();
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, $"GET {url}: {respuesta.StatusCode}\n{html}");
        return html;
    }
}
```

- [ ] **Step 2: Ejecutar y ver que fallan**

Run: `dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~AccionesClientesTests"`
Expected: FAIL.

- [ ] **Step 3: `AccionesCliente.cs`**

```csharp
using OpenSource1.Application.Security;
using OpenSource1.Blazor.Navigation;

namespace OpenSource1.Blazor.Components;

/// <summary>Acciones contextuales de un cliente (Fix-Features C1): las mismas en la lista, la tarjeta y la ficha.</summary>
public static class AccionesCliente
{
    public static IReadOnlyList<AccionPagina> Crear { get; } =
    [
        new("Factura", IconosModulo.Documento, id => $"/facturas-venta/nueva?socioId={id}", ApplicationPolicies.CanAdd, RequiereSeleccion: true),
        new("Nota de crédito", IconosModulo.Documento, id => $"/notas-credito-venta/nueva?socioId={id}", ApplicationPolicies.CanAdd, RequiereSeleccion: true),
        // Registrar pagos exige CanModify en la API (CobrosController).
        new("Cobro", IconosModulo.Tarjeta, id => $"/cobros/nuevo?socioId={id}", ApplicationPolicies.CanModify, RequiereSeleccion: true),
    ];

    public static IReadOnlyList<AccionPagina> Ver { get; } =
    [
        new("Ficha", IconosModulo.Persona, id => $"/clientes/{id}", null, RequiereSeleccion: true),
        new("Movimientos de cliente", IconosModulo.Lista, id => $"/ventas/movimientos-cliente?socioId={id}", ApplicationPolicies.CanConsult, RequiereSeleccion: true),
        new("Estado de cuenta", IconosModulo.Documento, id => $"/ventas/estado-cuenta?socioId={id}", ApplicationPolicies.CanConsult, RequiereSeleccion: true),
        new("Cobros", IconosModulo.Tarjeta, id => $"/cobros?socioId={id}", ApplicationPolicies.CanConsult, RequiereSeleccion: true),
        new("Facturas del cliente", IconosModulo.Documento, id => $"/facturas-venta?socioId={id}", ApplicationPolicies.CanConsult, RequiereSeleccion: true),
    ];
}
```

- [ ] **Step 4: Lista, tarjeta y ficha**

`Clientes.razor`: en el `<PageToolbar>` añadir `Crear="@AccionesCliente.Crear" Ver="@AccionesCliente.Ver"`; en
`TarjetaAccionesDe` cambiar `Resto` por `Resto="@([.. AccionesCliente.Crear, .. AccionesCliente.Ver.Skip(1), new AccionPagina("Eliminar", IconosModulo.Lista, id => BuildListUrl(deleteId: Guid.Parse(id!)), ApplicationPolicies.CanDelete)])"`
(la "Ficha" ya es acción rápida).

`ClienteDetail.razor`: sustituir la cabecera (`<div …>` con "Ficha" / `<h1>Cliente</h1>` / "Volver al listado" / "Editar")
por:

```razor
<PageToolbar Titulo="@(cliente?.NombreComercial ?? "Cliente")" Subtitulo="@(cliente is null ? null : $"Código {cliente.Codigo}")"
             Migas="@([new("Clientes", "/modulos/clientes"), new("Clientes", "/clientes"), new("Ficha")])"
             SeleccionId="@(cliente?.Id.ToString())"
             Crear="@AccionesCliente.Crear" Ver="@AccionesCliente.Ver.Skip(1).ToList()"
             EditarUrl="@(id => RetornoLocal.ConRetorno($"/clientes/{id}/editar", $"/clientes/{id}"))"
             EliminarUrl="@(_ => BuildDetailUrl(delete: true))" />
```

y borrar los iconos ✎/🗑 duplicados de la tarjeta de la ficha (la barra ya los ofrece).

- [ ] **Step 5: `/cobros/nuevo`**

`src/OpenSource1.Blazor/Components/Pages/CobroNuevo.razor`:

```razor
@page "/cobros/nuevo"
@attribute [Authorize(Policy = ApplicationPolicies.CanConsult)]
@using Microsoft.AspNetCore.Authorization
@using OpenSource1.Application.Features.CuentasContables.Dtos
@using OpenSource1.Application.Features.SociosNegocio.Dtos
@using OpenSource1.Application.Security
@using OpenSource1.Blazor.Services
@inject ICobroApiClient CobroApiClient
@inject ISocioNegocioApiClient SocioApiClient
@inject ICuentaContableApiClient CuentaApiClient
@inject IAuthorizationService AuthorizationService
@inject ILogger<CobroNuevo> Logger
@inject NavigationManager Nav

<EntityFormPage Titulo="Nuevo cobro" Subtitulo="Registra el pago del cliente; después podrá aplicarlo a sus facturas pendientes."
                Migas="@Migas" Mensaje="@message" TipoMensaje="@messageType" Errores="@errores">
    @if (!canModify)
    {
        <MessageBox Type="warning" Message="No tiene permiso para realizar esta acción." />
    }
    else
    {
        <form method="get" action="/cobros/nuevo" data-enhance class="mb-6 flex flex-wrap items-end gap-2" data-testid="buscar-socio">
            <div class="min-w-60 flex-1">
                <label class="mb-1 block text-xs font-semibold text-slate-500" for="socioQuery">Buscar cliente por nombre</label>
                <input id="socioQuery" type="text" name="socioQuery" value="@SocioQuery" placeholder="ej. Comercial" class="@InputCss" />
            </div>
            <button type="submit" class="btn-press rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-600 shadow-sm hover:bg-slate-50">Buscar</button>
        </form>
        <EditForm Model="PagoInput" FormName="registrar-pago" OnValidSubmit="RegistrarPagoAsync" Enhance="true">
            <DataAnnotationsValidator />
            <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <div class="sm:col-span-2">
                    <label class="mb-1 block text-sm font-medium text-slate-700" for="PagoInput_SocioNegocioId">Cliente <span class="text-red-500">*</span></label>
                    <select id="PagoInput_SocioNegocioId" name="PagoInput.SocioNegocioId" class="@InputCss">
                        <option value="" selected="@(PagoInput!.SocioNegocioId is null)">— Seleccione —</option>
                        @if (PagoInput.SocioNegocioId is { } elegido && socios.Items.All(s => s.Id != elegido))
                        {
                            <option value="@elegido" selected="selected">@(socioActual?.Id == elegido ? $"{socioActual.Codigo} — {socioActual.NombreComercial}" : $"Cliente seleccionado ({elegido})")</option>
                        }
                        @foreach (var s in socios.Items)
                        {
                            <option value="@s.Id" selected="@(s.Id == PagoInput.SocioNegocioId)">@s.Codigo — @s.NombreComercial</option>
                        }
                    </select>
                    <ValidationMessage For="() => PagoInput.SocioNegocioId" />
                </div>
                <div>
                    <label class="mb-1 block text-sm font-medium text-slate-700" for="PagoInput_ImporteTexto">Importe <span class="text-red-500">*</span></label>
                    <input id="PagoInput_ImporteTexto" type="text" inputmode="decimal" name="PagoInput.ImporteTexto" value="@PagoInput.ImporteTexto" placeholder="ej. 500.00" class="@InputCss" />
                    <ValidationMessage For="() => PagoInput.ImporteTexto" />
                </div>
                <div>
                    <label class="mb-1 block text-sm font-medium text-slate-700" for="PagoInput_FechaRegistroTexto">Fecha <span class="text-red-500">*</span></label>
                    <input id="PagoInput_FechaRegistroTexto" type="date" name="PagoInput.FechaRegistroTexto" value="@PagoInput.FechaRegistroTexto" class="@InputCss" />
                    <ValidationMessage For="() => PagoInput.FechaRegistroTexto" />
                </div>
                <div class="sm:col-span-2">
                    <label class="mb-1 block text-sm font-medium text-slate-700" for="PagoInput_CuentaCajaId">Cuenta de caja / banco <span class="text-red-500">*</span></label>
                    <select id="PagoInput_CuentaCajaId" name="PagoInput.CuentaCajaId" class="@InputCss">
                        <option value="" selected="@(PagoInput.CuentaCajaId is null)">— Seleccione —</option>
                        @foreach (var c in cuentas.Items)
                        {
                            <option value="@c.Id" selected="@(c.Id == PagoInput.CuentaCajaId)">@c.Numero — @c.Nombre</option>
                        }
                    </select>
                    <ValidationMessage For="() => PagoInput.CuentaCajaId" />
                </div>
                <div class="sm:col-span-2">
                    <label class="mb-1 block text-sm font-medium text-slate-700" for="PagoInput_Descripcion">Descripción</label>
                    <input id="PagoInput_Descripcion" type="text" name="PagoInput.Descripcion" value="@PagoInput.Descripcion" maxlength="200" class="@InputCss" />
                    <ValidationMessage For="() => PagoInput.Descripcion" />
                </div>
            </div>
            <FormularioAcciones CancelarHref="@Volver" LimpiarHref="@Nav.Uri" TextoGuardar="Registrar pago" Deshabilitado="@(socios.CargaFallida || cuentas.CargaFallida)" />
        </EditForm>
    }
</EntityFormPage>

@if (!canModify)
{
    <EditForm Model="PagoInput" FormName="registrar-pago" OnValidSubmit="RegistrarPagoAsync"></EditForm>
}

@code {
    private const string InputCss = "block w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-800 shadow-sm focus:border-brand-600 focus:outline-none focus:ring-2 focus:ring-brand-600/20";

    [SupplyParameterFromQuery(Name = "socioId")] private Guid? SocioId { get; set; }
    [SupplyParameterFromQuery(Name = "socioQuery")] private string? SocioQuery { get; set; }
    [SupplyParameterFromQuery(Name = "returnUrl")] private string? ReturnUrl { get; set; }
    [SupplyParameterFromForm(FormName = "registrar-pago")] private Cobros.PagoForm? PagoInput { get; set; }
    [CascadingParameter] private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    private OpcionesCargadas<SocioNegocioResponse> socios = OpcionesCargadas<SocioNegocioResponse>.SinCargar;
    private OpcionesCargadas<CuentaContableResponse> cuentas = OpcionesCargadas<CuentaContableResponse>.SinCargar;
    private SocioNegocioResponse? socioActual;
    private bool canModify;
    private string? message;
    private string messageType = "info";
    private IReadOnlyList<string> errores = [];

    private string Volver => RetornoLocal.Validar(ReturnUrl, SocioId is { } id ? $"/cobros?socioId={id}" : "/cobros");
    private IReadOnlyList<Miga> Migas => [new("Ventas", "/modulos/ventas"), new("Cobros", Volver), new("Nuevo cobro")];

    protected override async Task OnInitializedAsync()
    {
        PagoInput ??= new() { SocioNegocioId = SocioId, FechaRegistroTexto = EntradaFecha.Formatear(DateOnly.FromDateTime(DateTime.Today)) };
        if (AuthenticationStateTask is not null)
        {
            canModify = (await AuthorizationService.AuthorizeAsync((await AuthenticationStateTask).User, ApplicationPolicies.CanModify)).Succeeded;
        }

        if (!canModify)
        {
            return;
        }

        socios = await VentasOpciones.SociosAsync(SocioApiClient, SocioQuery, Logger);
        cuentas = await VentasOpciones.CuentasCapturaDirectaAsync(CuentaApiClient, Logger);
        if (PagoInput.CuentaCajaId is null && string.IsNullOrEmpty(PagoInput.ImporteTexto))
        {
            PagoInput.CuentaCajaId = cuentas.Items.FirstOrDefault(c => c.Numero == VentasOpciones.NumeroCuentaCajaPorDefecto)?.Id;
        }

        if (PagoInput.SocioNegocioId is { } socioId && socios.Items.All(s => s.Id != socioId))
        {
            try { socioActual = await SocioApiClient.GetByIdAsync(socioId); }
            catch (Exception ex) { Logger.LogWarning(ex, "Could not load socio {Id}.", socioId); }
        }
    }

    private async Task RegistrarPagoAsync()
    {
        if (canModify && (socios.CargaFallida || cuentas.CargaFallida)) return;

        var f = PagoInput!;
        var input = new RegistrarPagoInput(f.SocioNegocioId ?? Guid.Empty, f.Importe, f.FechaRegistro ?? default, f.CuentaCajaId,
            string.IsNullOrWhiteSpace(f.Descripcion) ? null : f.Descripcion.Trim());
        try
        {
            var result = await CobroApiClient.RegistrarPagoAsync(input);
            if (result.Succeeded)
            {
                // Como antes: a Cobros del cliente, para aplicar el pago a sus facturas.
                Nav.NavigateTo($"/cobros?socioId={input.SocioNegocioId}&ok=pago&numero={Uri.EscapeDataString(result.Valor!.Numero)}");
                return;
            }

            errores = result.Errors ?? [];
            messageType = "warning";
            message = result.Message;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not registrar pago.");
            messageType = "danger";
            message = "No fue posible registrar el pago.";
        }
    }
}
```

`Cobros.razor`:
1. Sustituir la cabecera (`<div class="mb-6">` "Ventas" / "Cobros de clientes") por
   `<PageToolbar Titulo="Cobros de clientes" Subtitulo="Consultar los movimientos abiertos de un cliente y aplicar pagos a sus facturas." Migas="@([new("Ventas", "/modulos/ventas"), new("Cobros")])" NuevoHref="@(SocioId is { } s ? $"/cobros/nuevo?socioId={s}" : "/cobros/nuevo")" NuevoTexto="Nuevo cobro" NuevoPermiso="@ApplicationPolicies.CanModify" />`.
2. Borrar la tarjeta `<div id="registrar-pago" …>…</div>` (el `aplicar-pago` se queda; la rejilla de dos columnas pasa a una).
3. Borrar `PagoInput` (con su `[SupplyParameterFromForm]`), `RegistrarPagoAsync`, la carga de `cuentas` y la inicialización de
   `PagoInput` de `OnInitializedAsync`, y el `EditForm` vacío `registrar-pago` del final; `PagoForm` **se queda** como clase
   pública anidada (la usa `CobroNuevo`). `OpcionSocioVigente` se queda solo si lo usa aún el selector de cliente de la
   página; si no, se borra.

- [ ] **Step 6: Ejecutar tests**

Run: `dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~AccionesClientesTests|FullyQualifiedName~ConversionClientesProductosTests"`
Expected: PASS.

- [ ] **Step 7: Build sin avisos**

Run: `dotnet build test.slnx -warnaserror`
Expected: 0 avisos.

- [ ] **Step 8: Commit**

```bash
git add src/OpenSource1.Blazor/Components/AccionesCliente.cs src/OpenSource1.Blazor/Components/Pages/CobroNuevo.razor src/OpenSource1.Blazor/Components/Pages/Clientes.razor src/OpenSource1.Blazor/Components/Pages/ClienteDetail.razor src/OpenSource1.Blazor/Components/Pages/Cobros.razor tests/OpenSource1.SmokeTests/Blazor/AccionesClientesTests.cs
git commit -m "feat: acciones contextuales de clientes y alta de cobro en pagina propia"
```

---

### Task C2: Productos — Crear ▾ / Ver ▾, filtro de estado de existencia y ajuste en diario precargado

**Grupo de paralelismo:** C — en paralelo con C1 y C3.

**Files:**
- Create: `src/OpenSource1.Blazor/Components/AccionesProducto.cs`
- Modify: `src/OpenSource1.Blazor/Components/Pages/Productos.razor`
- Modify: `src/OpenSource1.Blazor/Components/Pages/ProductoDetail.razor`
- Modify: `src/OpenSource1.Blazor/Components/Pages/LoteDiarioEditor.razor`
- Test: `tests/OpenSource1.SmokeTests/Blazor/AccionesProductosTests.cs`

**Interfaces:**
- Consumes: B1; `Productos.SeleccionValida`, `Productos.TarjetaAccionesDe` (B2c); `LoteDiarioEditor.DestinoTrasGuardar` (B2d); `IProductoApiClient.GetByIdAsync`; `ProductoSearchFilter.StockState` ("with" | "without"); la página `DiarioInventarioLote` ya precarga la línea con `?addProductoId=`.
- Produces: `static class AccionesProducto { Crear; Ver; }`; filtro `estado` (`with`/`without`) en `/productos`; `/diarios-inventario/nuevo?productoId=` → tras crear el lote navega a `/diarios-inventario/{loteId}?addProductoId={productoId}`.

- [ ] **Step 1: Tests (fallan)**

`tests/OpenSource1.SmokeTests/Blazor/AccionesProductosTests.cs`:

```csharp
extern alias BlazorApp;

using System.Net;
using BlazorApp::OpenSource1.Blazor.Services;
using Moq;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Dtos;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>Fix-Features C2: acciones contextuales de productos, filtro por estado de existencia y ajuste en diario precargado.</summary>
public sealed class AccionesProductosTests
{
    private static readonly Guid IdProducto = Guid.Parse("7e000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task Lista_Tarjeta_YFicha_ConCrearYVer()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var lista = await HtmlAsync(app.Cliente(), $"/productos?view=list&sel={IdProducto}");
        var tarjetas = await HtmlAsync(app.Cliente(), "/productos?view=grid");
        var ficha = await HtmlAsync(app.Cliente(), $"/productos/{IdProducto}");

        foreach (var destino in new[]
                 {
                     $"/diarios-inventario/nuevo?productoId={IdProducto}", $"/inventario/existencias?productoId={IdProducto}",
                     $"/inventario/movimientos-producto?productoId={IdProducto}", $"/inventario/movimientos-valor?productoId={IdProducto}",
                 })
        {
            Assert.Contains($"href=\"{destino}\"", lista);
            Assert.Contains($"href=\"{destino}\"", ficha);
        }

        Assert.Contains($"href=\"/diarios-inventario/nuevo?productoId={IdProducto}\"", tarjetas);
    }

    [Fact]
    public async Task FiltroEstado_EnviaStockStateALaApi()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var api = app.Simular<IProductoApiClient>();

        await HtmlAsync(app.Cliente(), "/productos?filters=estado&estado=without");

        api.Verify(c => c.ListAsync(It.Is<ProductoSearchFilter?>(f => f!.StockState == "without"), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task AjusteEnDiario_TrasCrearElLote_AbreElLoteConElProducto()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var plantilla = Guid.NewGuid();
        var lote = Guid.NewGuid();
        app.Simular<IDiarioInventarioApiClient>()
            .Setup(c => c.CreateLoteAsync(It.IsAny<LoteDiarioInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoteDiarioOperationResult(true, "ok", new LoteDiarioResponse { Id = lote }));

        var html = await HtmlAsync(app.Cliente(), $"/diarios-inventario/nuevo?productoId={IdProducto}");
        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), $"/diarios-inventario/nuevo?productoId={IdProducto}", "save-lote-diario",
            new Dictionary<string, string> { ["SaveInput.PlantillaDiarioId"] = plantilla.ToString(), ["SaveInput.Codigo"] = "AJ-02", ["SaveInput.Nombre"] = "Ajuste" });

        Assert.Contains("Tornillo", html);
        Assert.Equal($"/diarios-inventario/{lote}?addProductoId={IdProducto}", FormulariosSsr.Destino(respuesta));
    }

    private static BlazorSsrFactory Configurar(BlazorSsrFactory app)
    {
        var producto = new ProductoResponse { Id = IdProducto, Codigo = "P0001", Nombre = "Tornillo", CategoriaCodigo = "GENERAL", CategoriaNombre = "General" };
        var productos = app.Simular<IProductoApiClient>();
        productos.Setup(c => c.ListAsync(It.IsAny<ProductoSearchFilter?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<ProductoResponse>([producto], 1, 50, 1));
        productos.Setup(c => c.GetByIdAsync(IdProducto, It.IsAny<CancellationToken>())).ReturnsAsync(producto);
        app.Simular<IDiarioInventarioApiClient>().Setup(c => c.ListPlantillasAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        app.Simular<ICategoriaProductoApiClient>();
        app.Simular<IUnidadMedidaApiClient>();
        return app;
    }

    private static async Task<string> HtmlAsync(HttpClient cliente, string url)
    {
        var respuesta = await cliente.GetAsync(url);
        var html = await respuesta.Content.ReadAsStringAsync();
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, $"GET {url}: {respuesta.StatusCode}\n{html}");
        return html;
    }
}
```

- [ ] **Step 2: Ejecutar y ver que fallan**

Run: `dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~AccionesProductosTests"`
Expected: FAIL.

- [ ] **Step 3: `AccionesProducto.cs`**

```csharp
using OpenSource1.Application.Security;
using OpenSource1.Blazor.Navigation;

namespace OpenSource1.Blazor.Components;

/// <summary>Acciones contextuales de un producto (Fix-Features C2): lista, tarjeta y ficha.</summary>
public static class AccionesProducto
{
    public static IReadOnlyList<AccionPagina> Crear { get; } =
    [
        new("Ajuste en diario de inventario", IconosModulo.Lista, id => $"/diarios-inventario/nuevo?productoId={id}", ApplicationPolicies.CanAdd, RequiereSeleccion: true),
    ];

    public static IReadOnlyList<AccionPagina> Ver { get; } =
    [
        new("Ficha", IconosModulo.Cubo, id => $"/productos/{id}", null, RequiereSeleccion: true),
        new("Existencias", IconosModulo.Tabla, id => $"/inventario/existencias?productoId={id}", ApplicationPolicies.CanConsult, RequiereSeleccion: true),
        new("Movimientos de producto", IconosModulo.Lista, id => $"/inventario/movimientos-producto?productoId={id}", ApplicationPolicies.CanConsult, RequiereSeleccion: true),
        new("Movimientos de valor", IconosModulo.Lista, id => $"/inventario/movimientos-valor?productoId={id}", ApplicationPolicies.CanConsult, RequiereSeleccion: true),
    ];
}
```

- [ ] **Step 4: `Productos.razor` (menús, tarjetas y filtro de estado)**

1. Al `<PageToolbar>` que puso B2c añadir los atributos `Crear="@AccionesProducto.Crear" Ver="@AccionesProducto.Ver"`; en `TarjetaAccionesDe` el `Resto` pasa a
   `[.. AccionesProducto.Crear, .. AccionesProducto.Ver.Skip(1), new AccionPagina("Eliminar", IconosModulo.Lista, id => BuildListUrl(deleteId: Guid.Parse(id!)), ApplicationPolicies.CanDelete)]`.
2. `FilterableFields`: añadir `("estado", "Estado de existencia"),` al final.
3. `[SupplyParameterFromQuery(Name = "estado")] private string? SearchEstado { get; set; }` y

```csharp
    // "with" = con existencia, "without" = sin existencia (ProductoSearchFilter.StockState); cualquier otro valor se ignora.
    private string? EstadoExistencia() => ActiveFilterFields.Contains("estado") && SearchEstado is "with" or "without" ? SearchEstado : null;
```

4. En `LoadAsync` (y en la carga del reporte si construye su propio `ProductoSearchFilter`), añadir `StockState: EstadoExistencia()`
   como último argumento del `new ProductoSearchFilter(...)`.
5. En los dos lugares que renderizan `<input type="text" name="@field" value="@GetFieldValue(field)" …>` (panel de filtros y
   diálogo de reporte), envolver en:

```razor
@if (field == "estado")
{
    <select name="estado" class="block w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-800 shadow-sm dark:border-slate-600 dark:bg-slate-800 dark:text-slate-200">
        <option value="" selected="@(SearchEstado is null)">Todos</option>
        <option value="with" selected="@(SearchEstado == "with")">Con existencia</option>
        <option value="without" selected="@(SearchEstado == "without")">Sin existencia</option>
    </select>
}
else
{
    @* el <input type="text" …> existente, sin cambios *@
}
```

   (el comentario Razor marca dónde queda el `<input>` actual, que se conserva literal).
6. `GetFieldValue`: añadir `"estado" => SearchEstado,`; `BuildListUrl`: `if (activeFields.Contains("estado")) AddParameter(parameters,"estado",SearchEstado);`.

- [ ] **Step 5: Ficha de producto**

`ProductoDetail.razor`: sustituir la cabecera por

```razor
<PageToolbar Titulo="@(producto?.Nombre ?? "Producto")" Subtitulo="@(producto is null ? null : $"Código {producto.Codigo}")"
             Migas="@([new("Productos", "/modulos/productos"), new("Productos", "/productos"), new("Ficha")])"
             SeleccionId="@(producto?.Id.ToString())"
             Crear="@AccionesProducto.Crear" Ver="@AccionesProducto.Ver.Skip(1).ToList()"
             EditarUrl="@(id => RetornoLocal.ConRetorno($"/productos/{id}/editar", $"/productos/{id}"))"
             EliminarUrl="@(_ => BuildDetailUrl(delete: true))" />
```

y borrar los iconos ✎/🗑 duplicados de la tarjeta de la ficha.

- [ ] **Step 6: `LoteDiarioEditor.razor` con `?productoId=`**

Añadir `@inject IProductoApiClient ProductoApiClient` y `@using OpenSource1.Application.Features.Productos.Dtos`, y en `@code`:

```csharp
    [SupplyParameterFromQuery(Name = "productoId")] private Guid? ProductoId { get; set; }
    private ProductoResponse? productoAjuste;
```

al final de `OnInitializedAsync` (solo en alta):

```csharp
        if (!EsEdicion && ProductoId is { } productoId)
        {
            try { productoAjuste = await ProductoApiClient.GetByIdAsync(productoId); }
            catch (Exception ex) { Logger.LogWarning(ex, "Could not load producto {Id} for ajuste.", productoId); }
        }
```

dentro de la rama de alta, antes del `EditForm`:

```razor
@if (productoAjuste is not null)
{
    <div class="mb-4"><MessageBox Type="info" Message="@($"Al crear el lote se abrirá con una línea para {productoAjuste.Codigo} — {productoAjuste.Nombre}.")" /></div>
}
```

y reemplazar `DestinoTrasGuardar` por:

```csharp
    private string DestinoTrasGuardar(LoteDiarioOperationResult result, string successCode) =>
        successCode == "created" && ProductoId is { } productoId && result.Valor is { } lote
            ? $"/diarios-inventario/{lote.Id}?addProductoId={productoId}"
            : RetornoLocal.ConParametro(Volver, "ok", successCode);
```

(El `EditForm` publica en la misma URL, con su query: `ProductoId` también llega en el POST.)

- [ ] **Step 7: Ejecutar tests**

Run: `dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~AccionesProductosTests|FullyQualifiedName~ConversionClientesProductosTests|FullyQualifiedName~ConversionDocumentosUsuariosTests"`
Expected: PASS.

- [ ] **Step 8: Build sin avisos**

Run: `dotnet build test.slnx -warnaserror`
Expected: 0 avisos.

- [ ] **Step 9: Commit**

```bash
git add src/OpenSource1.Blazor/Components/AccionesProducto.cs src/OpenSource1.Blazor/Components/Pages/Productos.razor src/OpenSource1.Blazor/Components/Pages/ProductoDetail.razor src/OpenSource1.Blazor/Components/Pages/LoteDiarioEditor.razor tests/OpenSource1.SmokeTests/Blazor/AccionesProductosTests.cs
git commit -m "feat: acciones contextuales de productos, filtro por estado de existencia y ajuste en diario precargado"
```

---

### Task C3: Facturas y documentos — `/facturas-venta/nueva` con precarga, `/notas-credito-venta/nueva`, filtro por cliente

**Grupo de paralelismo:** C — en paralelo con C1 y C2.

**Files:**
- Modify: `src/OpenSource1.Blazor/Components/Pages/FacturasVenta.razor`
- Modify: `src/OpenSource1.Blazor/Components/Pages/FacturaVentaNueva.razor`
- Modify: `src/OpenSource1.Blazor/Components/Pages/FacturasVentaBorradores.razor`
- Modify: `src/OpenSource1.Blazor/Components/Pages/NotasCreditoVentaBorradores.razor`
- Create: `src/OpenSource1.Blazor/Components/Pages/NotaCreditoNueva.razor`
- Test: `tests/OpenSource1.SmokeTests/Blazor/AccionesFacturasTests.cs`

**Interfaces:**
- Consumes: B1; `FacturaVentaNueva` (B2d); `IFacturaVentaApiClient.ListFacturasAsync(FacturaVentaSearchCriteria?, PageRequest?, …)`; `FacturaVentaResponse.SocioNegocioId`; `ICobroApiClient.ListMovimientosAbiertosAsync(socioId)` (`MovimientoClienteResponse.TipoDocumento`, `NumeroDocumento`, `ImporteRestante`); `INotaCreditoVentaApiClient.CreateBorradorAsync(NotaCreditoBorradorInput)`; `ITerminoPagoApiClient` + `TerminoPagoOpciones.NombreAsync`.
- Produces: `/facturas-venta?socioId=` (filtro de UI → `SocioNegocioId` de la API), `?sel={numero}` en Facturas, `/facturas-venta/nueva?socioId=` con cliente precargado y bloqueado, `/notas-credito-venta/nueva?socioId=`.

- [ ] **Step 1: Tests (fallan)**

`tests/OpenSource1.SmokeTests/Blazor/AccionesFacturasTests.cs`:

```csharp
extern alias BlazorApp;

using System.Net;
using BlazorApp::OpenSource1.Blazor.Services;
using Moq;
using OpenSource1.Application.Features.FacturasVenta.Borradores.Dtos;
using OpenSource1.Application.Features.FacturasVenta.Posteadas;
using OpenSource1.Application.Features.FacturasVenta.Posteadas.Dtos;
using OpenSource1.Application.Features.MovimientosCliente.Dtos;
using OpenSource1.Application.Features.NotasCreditoVenta.Borradores.Dtos;
using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>Fix-Features C3: facturas filtradas por cliente, nueva factura con cliente precargado y nota de crédito por cliente.</summary>
public sealed class AccionesFacturasTests
{
    private static readonly Guid IdCliente = Guid.Parse("7f000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task Facturas_FiltranPorCliente_YBarraConNuevaFacturaYAccionesDeLaSeleccion()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var api = app.Simular<IFacturaVentaApiClient>();

        var html = await HtmlAsync(app.Cliente(), $"/facturas-venta?socioId={IdCliente}&sel=FV0001");

        api.Verify(c => c.ListFacturasAsync(It.Is<FacturaVentaSearchCriteria?>(f => f!.SocioNegocioId == IdCliente), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        Assert.Contains("href=\"/facturas-venta/nueva", html);
        Assert.Contains("href=\"/facturas-venta/FV0001?crearNota=true\"", html);
        Assert.Contains($"href=\"/ventas/movimientos-cliente?socioId={IdCliente}\"", html);
        Assert.Contains("Comercial Uno", html);
    }

    [Fact]
    public async Task NuevaFactura_ConSocio_PrecargaYBloqueaElCliente()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlAsync(app.Cliente(), $"/facturas-venta/nueva?socioId={IdCliente}");

        Assert.Contains("data-testid=\"cliente-precargado\"", html);
        Assert.Contains($"<input type=\"hidden\" name=\"AddInput.SocioNegocioId\" value=\"{IdCliente}\"", html);
        Assert.Contains("CONTADO — Contado", html);
        Assert.Contains("GENERAL", html);
        Assert.Contains("href=\"/facturas-venta/nueva\"", html);
    }

    [Fact]
    public async Task NuevaFactura_SocioInexistente_AvisoYSelectorNormal()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlAsync(app.Cliente(), $"/facturas-venta/nueva?socioId={Guid.NewGuid()}");

        Assert.Contains("No se encontró el cliente indicado", html);
        Assert.DoesNotContain("data-testid=\"cliente-precargado\"", html);
        Assert.Contains("name=\"AddInput.SocioNegocioId\"", html);
    }

    [Fact]
    public async Task NuevaFactura_Guardar_CreaElBorradorConElCliente()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var borrador = Guid.NewGuid();
        app.Simular<IFacturaVentaApiClient>()
            .Setup(c => c.CreateBorradorAsync(It.Is<BorradorCabeceraInput>(i => i.SocioNegocioId == IdCliente), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VentaOperationResult<FacturaVentaBorradorResponse>(true, "ok", new FacturaVentaBorradorResponse { Id = borrador }));

        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), $"/facturas-venta/nueva?socioId={IdCliente}", "add-borrador",
            new Dictionary<string, string> { ["AddInput.SocioNegocioId"] = IdCliente.ToString(), ["AddInput.FechaRegistroTexto"] = "2026-09-28" });

        Assert.Equal($"/facturas-venta/borradores/{borrador}?ok=creado", FormulariosSsr.Destino(respuesta));
    }

    [Fact]
    public async Task NotaCreditoNueva_ListaSoloFacturasConPendiente_YCreaElBorrador()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var nota = Guid.NewGuid();
        app.Simular<INotaCreditoVentaApiClient>()
            .Setup(c => c.CreateBorradorAsync(It.Is<NotaCreditoBorradorInput>(i => i.FacturaVentaNumero == "FV0001" && i.CopiarLineas), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VentaOperationResult<NotaCreditoVentaBorradorResponse>(true, "ok", new NotaCreditoVentaBorradorResponse { Id = nota }));

        var html = await HtmlAsync(app.Cliente(), $"/notas-credito-venta/nueva?socioId={IdCliente}");
        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), $"/notas-credito-venta/nueva?socioId={IdCliente}", "nueva-nota-socio",
            new Dictionary<string, string> { ["NotaInput.FacturaVentaNumero"] = "FV0001", ["NotaInput.CopiarLineas"] = "true" });

        Assert.Contains("value=\"FV0001\"", html);
        Assert.DoesNotContain("value=\"PAG-0001\"", html);
        Assert.DoesNotContain("value=\"FV0002\"", html);
        Assert.Equal($"/notas-credito-venta/borradores/{nota}?ok=creado", FormulariosSsr.Destino(respuesta));
    }

    [Fact]
    public async Task Borradores_ConNuevaFacturaYVerCliente()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var facturas = await HtmlAsync(app.Cliente(), "/facturas-venta/borradores");
        var notas = await HtmlAsync(app.Cliente(), "/notas-credito-venta/borradores");

        Assert.Contains("href=\"/facturas-venta/nueva?returnUrl=", facturas);
        Assert.Contains("data-testid=\"menu-ver\"", facturas);
        Assert.Contains("href=\"/notas-credito-venta/nueva", notas);
        Assert.Contains("data-testid=\"menu-ver\"", notas);
    }

    private static BlazorSsrFactory Configurar(BlazorSsrFactory app)
    {
        var terminoId = Guid.NewGuid();
        var cliente = new SocioNegocioResponse { Id = IdCliente, Codigo = "C0001", NombreComercial = "Comercial Uno", TerminoPagoId = terminoId, GrupoClienteContableCodigo = "GENERAL" };
        var socios = app.Simular<ISocioNegocioApiClient>();
        socios.Setup(c => c.GetByIdAsync(IdCliente, It.IsAny<CancellationToken>())).ReturnsAsync(cliente);
        socios.Setup(c => c.ListAsync(It.IsAny<SocioNegocioSearchFilter?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<SocioNegocioResponse>([cliente], 1, 50, 1));
        app.Simular<ITerminoPagoApiClient>().Setup(c => c.GetByIdAsync(terminoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpenSource1.Application.Features.TerminosPago.Dtos.TerminoPagoResponse { Id = terminoId, Codigo = "CONTADO", Descripcion = "Contado" });
        var facturas = app.Simular<IFacturaVentaApiClient>();
        facturas.Setup(c => c.ListFacturasAsync(It.IsAny<FacturaVentaSearchCriteria?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<FacturaVentaResponse>([new FacturaVentaResponse { Numero = "FV0001", SocioNegocioId = IdCliente, NombreFacturacion = "Comercial Uno" }], 1, 50, 1));
        facturas.Setup(c => c.ListBorradoresAsync(It.IsAny<FacturaVentaBorradorFiltro?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<FacturaVentaBorradorResponse>([], 1, 50, 0));
        app.Simular<INotaCreditoVentaApiClient>()
            .Setup(c => c.ListBorradoresAsync(It.IsAny<NotaCreditoVentaBorradorFiltro?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<NotaCreditoVentaBorradorResponse>([], 1, 50, 0));
        app.Simular<ICobroApiClient>().Setup(c => c.ListMovimientosAbiertosAsync(IdCliente, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new MovimientoClienteResponse { Id = 1, TipoDocumento = TipoDocumentoCliente.Factura, NumeroDocumento = "FV0001", ImporteOriginal = 100m, ImporteRestante = 60m, Abierta = true },
                new MovimientoClienteResponse { Id = 2, TipoDocumento = TipoDocumentoCliente.Pago, NumeroDocumento = "PAG-0001", ImporteOriginal = -40m, ImporteRestante = -40m, Abierta = true },
            ]);
        app.Simular<IAlmacenApiClient>();
        return app;
    }

    private static async Task<string> HtmlAsync(HttpClient cliente, string url)
    {
        var respuesta = await cliente.GetAsync(url);
        var html = await respuesta.Content.ReadAsStringAsync();
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, $"GET {url}: {respuesta.StatusCode}\n{html}");
        return html;
    }
}
```

- [ ] **Step 2: Ejecutar y ver que fallan**

Run: `dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~AccionesFacturasTests"`
Expected: FAIL.

- [ ] **Step 3: `FacturasVenta.razor` (filtro por cliente, selección y barra)**

1. `[SupplyParameterFromQuery(Name = "socioId")] private Guid? SocioId { get; set; }` y
   `[SupplyParameterFromQuery(Name = "sel")] private string? Sel { get; set; }`; `@inject ISocioNegocioApiClient SocioApiClient`.
2. En la construcción del filtro (línea 559) cambiar el tercer argumento `null` por `SocioId`:
   `var filtro = new FacturaVentaSearchCriteria(EmptyAsNull(SearchNumero), EmptyAsNull(SearchNombre), SocioId, desde, hasta);`
   y, si `SocioId` tiene valor, cargar `socioFiltro = await SocioApiClient.GetByIdAsync(SocioId.Value)` (try/catch con log)
   para mostrar el chip `Cliente: {Codigo} — {NombreComercial}` con enlace "Quitar" a `BuildListUrl(1)` sin `socioId`
   (añadir el parámetro `bool conSocio = true` a `BuildListUrl`).
3. `BuildListUrl(int pagina, bool conSocio = true, string? sel = null)`: añade `socioId={SocioId}` si `conSocio` y
   `sel={Uri.EscapeDataString(sel ?? Sel)}` si hay selección; el formulario GET de filtros incluye
   `<input type="hidden" name="socioId" value="@SocioId" />` cuando hay cliente.
4. Vista de listado (`Numero is null`): cabecera → 

```razor
<PageToolbar Titulo="Facturas" Subtitulo="Facturas de venta posteadas."
             Migas="@([new("Facturación", "/modulos/facturacion"), new("Facturas")])"
             NuevoHref="/facturas-venta/nueva" NuevoTexto="Nueva factura"
             SeleccionId="@FacturaSeleccionada?.Numero"
             Crear="@CrearFactura" Ver="@VerFactura">
    <a href="/facturas-venta/borradores" class="btn-press rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm font-semibold text-slate-600 hover:bg-slate-50">Borradores</a>
</PageToolbar>
```

   con

```csharp
    private FacturaVentaResponse? FacturaSeleccionada => Sel is null ? null : facturas.FirstOrDefault(f => f.Numero == Sel);

    private IReadOnlyList<AccionPagina> CrearFactura =>
    [
        new("Nota de crédito de la factura", IconosModulo.Documento, id => $"/facturas-venta/{Uri.EscapeDataString(id!)}?crearNota=true", ApplicationPolicies.CanAdd, RequiereSeleccion: true),
    ];

    private IReadOnlyList<AccionPagina> VerFactura =>
    [
        new("Detalle", IconosModulo.Documento, id => $"/facturas-venta/{Uri.EscapeDataString(id!)}", null, RequiereSeleccion: true),
        new("Movimientos del cliente", IconosModulo.Lista, _ => $"/ventas/movimientos-cliente?socioId={FacturaSeleccionada?.SocioNegocioId}", ApplicationPolicies.CanConsult, RequiereSeleccion: true),
    ];
```

   (`facturas` es la lista que ya carga la página, `IReadOnlyList<FacturaVentaResponse>`, línea 395).
   En la tabla, primera celda `<SeleccionFila Href="@BuildListUrl(Pagina, sel: f.Numero)" Seleccionada="@(f.Numero == FacturaSeleccionada?.Numero)" Etiqueta="@f.Numero" />`
   (+ `<th>` y resaltado de fila).
5. Vista de detalle (`Numero is not null`): añadir encima del contenido
   `<PageToolbar Titulo="@($"Factura {Numero}")" Migas="@([new("Facturación", "/modulos/facturacion"), new("Facturas", "/facturas-venta"), new(Numero!)])" SeleccionId="@Numero" Crear="@CrearFactura" Ver="@VerDetalle" />`
   con `VerDetalle` = `[new("Movimientos del cliente", IconosModulo.Lista, _ => $"/ventas/movimientos-cliente?socioId={detalle?.Cabecera.SocioNegocioId}", ApplicationPolicies.CanConsult, RequiereSeleccion: true)]`
   (el botón existente "Crear nota de crédito" y su diálogo se conservan).

- [ ] **Step 4: `FacturaVentaNueva.razor` con `?socioId=`**

Añadir `@inject ITerminoPagoApiClient TerminoPagoApiClient`, y en `@code`:

```csharp
    [SupplyParameterFromQuery(Name = "socioId")] private Guid? SocioId { get; set; }
    private SocioNegocioResponse? socioPrecargado;
    private string? terminoPrecargado;
```

al final de `OnInitializedAsync` (dentro de `if (canAdd)`):

```csharp
        if (SocioId is { } socioId)
        {
            try
            {
                socioPrecargado = await SocioApiClient.GetByIdAsync(socioId);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Could not load socio {Id} for nueva factura.", socioId);
            }

            if (socioPrecargado is null)
            {
                Show("warning", "No se encontró el cliente indicado; seleccione uno de la lista.");
            }
            else
            {
                // Cliente bloqueado a cambio explícito: el formulario lo envía oculto; "Cambiar cliente" quita el socioId.
                AddInput!.SocioNegocioId = socioPrecargado.Id;
                if (socioPrecargado.TerminoPagoId is { } terminoId)
                {
                    terminoPrecargado = await TerminoPagoOpciones.NombreAsync(TerminoPagoApiClient, terminoId, Logger);
                }
            }
        }
```

y en el marcado, dentro del `EditForm` y **antes** de `@CamposBorrador(AddInput!)`:

```razor
@if (socioPrecargado is not null)
{
    <div class="mb-6 rounded-xl border border-brand-100 bg-brand-50/60 px-4 py-3 text-sm dark:border-brand-500/20 dark:bg-brand-500/10" data-testid="cliente-precargado">
        <input type="hidden" name="AddInput.SocioNegocioId" value="@socioPrecargado.Id" />
        <p class="font-semibold text-slate-800 dark:text-slate-100">@socioPrecargado.Codigo — @socioPrecargado.NombreComercial</p>
        <p class="text-xs text-slate-500 dark:text-slate-400">Término de pago: @(terminoPrecargado ?? "Sin término") · Grupo de cliente: @(socioPrecargado.GrupoClienteContableCodigo ?? "Sin asignar") · Fecha de registro: hoy</p>
        <a href="/facturas-venta/nueva" class="mt-1 inline-block text-xs font-semibold text-brand-600 hover:text-brand-700">Cambiar cliente</a>
    </div>
}
```

y en `CamposBorrador` renderizar el `<select>` de "Vender a" solo si `socioPrecargado is null` (el de "Facturar a" se
mantiene). El formulario de búsqueda de socio se oculta cuando hay `socioPrecargado`. `TerminoPagoOpciones.NombreAsync`
ya existe (lo usa `ClienteDetail`): devuelve `"{Codigo} — {Descripcion}"`.

- [ ] **Step 5: `NotaCreditoNueva.razor`**

`src/OpenSource1.Blazor/Components/Pages/NotaCreditoNueva.razor`:

```razor
@page "/notas-credito-venta/nueva"
@attribute [Authorize(Policy = ApplicationPolicies.CanConsult)]
@using System.ComponentModel.DataAnnotations
@using System.Globalization
@using Microsoft.AspNetCore.Authorization
@using OpenSource1.Application.Features.MovimientosCliente.Dtos
@using OpenSource1.Application.Features.SociosNegocio.Dtos
@using OpenSource1.Application.Security
@using OpenSource1.Blazor.Services
@using OpenSource1.Core.Enums
@inject INotaCreditoVentaApiClient NotaApiClient
@inject ICobroApiClient CobroApiClient
@inject ISocioNegocioApiClient SocioApiClient
@inject IAuthorizationService AuthorizationService
@inject ILogger<NotaCreditoNueva> Logger
@inject NavigationManager Nav

<EntityFormPage Titulo="Nueva nota de crédito" Subtitulo="Elija una factura posteada del cliente con importe pendiente."
                Migas="@Migas" Mensaje="@message" TipoMensaje="@messageType" Errores="@errores">
    @if (!canAdd)
    {
        <MessageBox Type="warning" Message="No tiene permiso para realizar esta acción." />
    }
    else if (SocioId is null)
    {
        <form method="get" action="/notas-credito-venta/nueva" data-enhance class="mb-4 flex flex-wrap items-end gap-2">
            <div class="min-w-60 flex-1">
                <label class="mb-1 block text-xs font-semibold text-slate-500" for="socioQuery">Buscar cliente por nombre</label>
                <input id="socioQuery" type="text" name="socioQuery" value="@SocioQuery" class="@InputCss" />
            </div>
            <button type="submit" class="btn-press rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-600 shadow-sm hover:bg-slate-50">Buscar</button>
        </form>
        <ul class="divide-y divide-slate-100 rounded-xl border border-slate-200 dark:divide-slate-700 dark:border-slate-700" data-testid="elegir-cliente">
            @foreach (var s in socios.Items)
            {
                <li><a href="@($"/notas-credito-venta/nueva?socioId={s.Id}")" class="block px-4 py-2 text-sm hover:bg-slate-50 dark:hover:bg-slate-700/40">@s.Codigo — @s.NombreComercial</a></li>
            }
        </ul>
    }
    else
    {
        <p class="mb-4 text-sm text-slate-600 dark:text-slate-300">Cliente: <strong>@(socio is null ? SocioId.ToString() : $"{socio.Codigo} — {socio.NombreComercial}")</strong> · <a href="/notas-credito-venta/nueva" class="font-semibold text-brand-600">Cambiar cliente</a></p>
        @if (facturas.Count == 0)
        {
            <MessageBox Type="info" Message="El cliente no tiene facturas posteadas con importe pendiente." />
        }
        else
        {
            <EditForm Model="NotaInput" FormName="nueva-nota-socio" OnValidSubmit="CrearAsync" Enhance="true">
                <DataAnnotationsValidator />
                <fieldset class="mb-4">
                    <legend class="mb-2 text-sm font-medium text-slate-700 dark:text-slate-300">Factura <span class="text-red-500">*</span></legend>
                    @foreach (var f in facturas)
                    {
                        <label class="flex items-center gap-2 py-1 text-sm">
                            <input type="radio" name="NotaInput.FacturaVentaNumero" value="@f.NumeroDocumento" checked="@(f.NumeroDocumento == NotaInput!.FacturaVentaNumero)" />
                            <span class="font-semibold">@f.NumeroDocumento</span>
                            <span class="text-slate-500">@f.FechaRegistro.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) · pendiente @f.ImporteRestante.ToString("N2", CultureInfo.InvariantCulture)</span>
                        </label>
                    }
                    <ValidationMessage For="() => NotaInput!.FacturaVentaNumero" />
                </fieldset>
                <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
                    <div>
                        <label class="mb-1 block text-sm font-medium text-slate-700" for="NotaInput_FechaRegistroTexto">Fecha de registro</label>
                        <input id="NotaInput_FechaRegistroTexto" type="date" name="NotaInput.FechaRegistroTexto" value="@NotaInput!.FechaRegistroTexto" class="@InputCss" />
                        <ValidationMessage For="() => NotaInput.FechaRegistroTexto" />
                    </div>
                    <div>
                        <label class="mb-1 block text-sm font-medium text-slate-700" for="NotaInput_FechaDocumentoTexto">Fecha de documento</label>
                        <input id="NotaInput_FechaDocumentoTexto" type="date" name="NotaInput.FechaDocumentoTexto" value="@NotaInput.FechaDocumentoTexto" class="@InputCss" />
                        <ValidationMessage For="() => NotaInput.FechaDocumentoTexto" />
                    </div>
                    <div class="sm:col-span-2">
                        <label class="mb-1 block text-sm font-medium text-slate-700" for="NotaInput_Descripcion">Descripción</label>
                        <input id="NotaInput_Descripcion" type="text" name="NotaInput.Descripcion" value="@NotaInput.Descripcion" maxlength="200" class="@InputCss" />
                        <ValidationMessage For="() => NotaInput.Descripcion" />
                    </div>
                </div>
                <label class="mt-4 flex items-center gap-2 text-sm"><input type="checkbox" name="NotaInput.CopiarLineas" value="true" checked="@NotaInput.CopiarLineas" /> Copiar las líneas pendientes de la factura</label>
                <label class="mt-2 flex items-center gap-2 text-sm"><input type="checkbox" name="NotaInput.DevolverInventario" value="true" checked="@NotaInput.DevolverInventario" /> Devolver inventario en las líneas de producto</label>
                <FormularioAcciones CancelarHref="@Volver" LimpiarHref="@Nav.Uri" TextoGuardar="Crear borrador" />
            </EditForm>
        }
    }
</EntityFormPage>

@if (!(canAdd && SocioId is not null && facturas.Count > 0))
{
    <EditForm Model="NotaInput" FormName="nueva-nota-socio" OnValidSubmit="CrearAsync"></EditForm>
}

@code {
    private const string InputCss = "block w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-800 shadow-sm focus:border-brand-600 focus:outline-none focus:ring-2 focus:ring-brand-600/20";

    [SupplyParameterFromQuery(Name = "socioId")] private Guid? SocioId { get; set; }
    [SupplyParameterFromQuery(Name = "socioQuery")] private string? SocioQuery { get; set; }
    [SupplyParameterFromQuery(Name = "returnUrl")] private string? ReturnUrl { get; set; }
    [SupplyParameterFromForm(FormName = "nueva-nota-socio")] private NuevaNotaForm? NotaInput { get; set; }
    [CascadingParameter] private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    private OpcionesCargadas<SocioNegocioResponse> socios = OpcionesCargadas<SocioNegocioResponse>.SinCargar;
    private SocioNegocioResponse? socio;
    private IReadOnlyList<MovimientoClienteResponse> facturas = [];
    private bool canAdd;
    private string? message;
    private string messageType = "info";
    private IReadOnlyList<string> errores = [];

    private string Volver => RetornoLocal.Validar(ReturnUrl, "/notas-credito-venta/borradores");
    private IReadOnlyList<Miga> Migas => [new("Facturación", "/modulos/facturacion"), new("Borradores de nota de crédito", Volver), new("Nueva nota de crédito")];

    protected override async Task OnInitializedAsync()
    {
        NotaInput ??= new() { CopiarLineas = true };
        if (AuthenticationStateTask is not null)
        {
            canAdd = (await AuthorizationService.AuthorizeAsync((await AuthenticationStateTask).User, ApplicationPolicies.CanAdd)).Succeeded;
        }

        if (!canAdd)
        {
            return;
        }

        if (SocioId is not { } socioId)
        {
            socios = await VentasOpciones.SociosAsync(SocioApiClient, SocioQuery, Logger);
            return;
        }

        try
        {
            socio = await SocioApiClient.GetByIdAsync(socioId);
            var abiertos = await CobroApiClient.ListMovimientosAbiertosAsync(socioId) ?? [];
            // Solo facturas posteadas con importe pendiente (restante > 0), en orden cronológico.
            facturas = [.. abiertos.Where(m => m.TipoDocumento == TipoDocumentoCliente.Factura && m.ImporteRestante > 0)];
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not load facturas pendientes of socio {Id}.", socioId);
            message = "No fue posible cargar las facturas pendientes del cliente.";
            messageType = "danger";
        }
    }

    private async Task CrearAsync()
    {
        var f = NotaInput!;
        try
        {
            var result = await NotaApiClient.CreateBorradorAsync(new NotaCreditoBorradorInput(
                f.FacturaVentaNumero.Trim(), f.FechaRegistro, f.FechaDocumento,
                string.IsNullOrWhiteSpace(f.Descripcion) ? null : f.Descripcion.Trim(), f.CopiarLineas, f.DevolverInventario));
            if (result.Succeeded)
            {
                Nav.NavigateTo($"/notas-credito-venta/borradores/{result.Valor!.Id}?ok=creado");
                return;
            }

            errores = result.Errors ?? [];
            message = result.Message;
            messageType = "warning";
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not create nota de crédito from factura {Numero}.", f.FacturaVentaNumero);
            message = "No fue posible crear el borrador de nota de crédito.";
            messageType = "danger";
        }
    }

    public sealed class NuevaNotaForm : IValidatableObject
    {
        [Required(ErrorMessage = "Seleccione la factura.")]
        public string FacturaVentaNumero { get; set; } = string.Empty;

        public string? FechaRegistroTexto { get; set; }

        public string? FechaDocumentoTexto { get; set; }

        [MaxLength(200, ErrorMessage = "La descripción admite como máximo 200 caracteres.")]
        public string? Descripcion { get; set; }

        public bool CopiarLineas { get; set; }

        public bool DevolverInventario { get; set; }

        public DateOnly? FechaRegistro => Fecha(FechaRegistroTexto);

        public DateOnly? FechaDocumento => Fecha(FechaDocumentoTexto);

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            foreach (var (texto, campo, etiqueta) in new[]
                     {
                         (FechaRegistroTexto, nameof(FechaRegistroTexto), "La fecha de registro"),
                         (FechaDocumentoTexto, nameof(FechaDocumentoTexto), "La fecha de documento"),
                     })
            {
                if (!string.IsNullOrWhiteSpace(texto) && !EntradaFecha.TryParse(texto, out _, out var error, etiqueta))
                {
                    yield return new ValidationResult(error ?? $"{etiqueta} no es válida.", [campo]);
                }
            }
        }

        private static DateOnly? Fecha(string? texto) =>
            !string.IsNullOrWhiteSpace(texto) && EntradaFecha.TryParse(texto, out var valor, out _) ? valor : null;
    }
}
```

- [ ] **Step 6: Borradores (Nueva factura / nota y Ver ▾)**

`FacturasVentaBorradores.razor`: en su `<PageToolbar>` añadir
`Ver="@([new AccionPagina("Abrir borrador", IconosModulo.Documento, id => $"/facturas-venta/borradores/{id}", null, RequiereSeleccion: true), new AccionPagina("Cliente", IconosModulo.Persona, _ => $"/clientes/{BorradorSeleccionado?.SocioNegocioId}", ApplicationPolicies.CanConsult, RequiereSeleccion: true)])"`
con `private FacturaVentaBorradorResponse? BorradorSeleccionado => Sel is { } s ? borradores.FirstOrDefault(b => b.Id == s) : null;`
(el `NuevoHref`/"Nueva factura" ya lo puso B2d). Para que "Abrir borrador"/"Cliente" sirvan también con borradores
liberados, la barra usa `SeleccionId="@BorradorSeleccionado?.Id.ToString()"` y la restricción a `Abierta` se mantiene solo
en `EditarUrl`/`EliminarUrl` (pasar `EditarUrl`/`EliminarUrl` como `null` cuando `BorradorSeleccionado?.Estado != EstadoFacturaBorrador.Abierta`).

`NotasCreditoVentaBorradores.razor`: en su `<PageToolbar>` añadir `NuevoHref="@RetornoLocal.ConRetorno("/notas-credito-venta/nueva", BuildListUrl())" NuevoTexto="Nueva nota de crédito"`
y `Ver="@([new AccionPagina("Abrir borrador", IconosModulo.Documento, id => $"/notas-credito-venta/borradores/{id}", null, RequiereSeleccion: true), new AccionPagina("Cliente", IconosModulo.Persona, _ => $"/clientes/{NotaSeleccionada?.SocioNegocioId}", ApplicationPolicies.CanConsult, RequiereSeleccion: true)])"`
con `NotaSeleccionada` análogo sobre `borradores`.

- [ ] **Step 7: Ejecutar tests**

Run: `dotnet test tests/OpenSource1.SmokeTests --filter "FullyQualifiedName~AccionesFacturasTests|FullyQualifiedName~ConversionDocumentosUsuariosTests|FullyQualifiedName~FacturaVentaApiClientTests|FullyQualifiedName~NotaCreditoVentaApiClientTests"`
Expected: PASS.

- [ ] **Step 8: Build sin avisos**

Run: `dotnet build test.slnx -warnaserror`
Expected: 0 avisos.

- [ ] **Step 9: Commit**

```bash
git add src/OpenSource1.Blazor/Components/Pages/FacturasVenta.razor src/OpenSource1.Blazor/Components/Pages/FacturaVentaNueva.razor src/OpenSource1.Blazor/Components/Pages/FacturasVentaBorradores.razor src/OpenSource1.Blazor/Components/Pages/NotasCreditoVentaBorradores.razor src/OpenSource1.Blazor/Components/Pages/NotaCreditoNueva.razor tests/OpenSource1.SmokeTests/Blazor/AccionesFacturasTests.cs
git commit -m "feat: nueva factura con cliente precargado, nota de credito por cliente y facturas filtradas por cliente"
```

---

### Task G-C: Puerta de la Fase C

**Grupo de paralelismo:** serie (tras C1–C3).

- [ ] **Step 1: Integrar C1–C3** (mismo procedimiento que G-B).
- [ ] **Step 2: Suite completa y build**

Run: `dotnet build test.slnx -warnaserror && env DOCKER_CONTEXT=default dotnet test test.slnx`
Expected: 0 avisos; suite verde.

- [ ] **Step 3: Runtime sobre Docker** — `docker compose up -d --build api blazor`; como `admin`: desde un cliente seleccionado,
  Crear ▾ Factura abre `/facturas-venta/nueva` con el cliente, término y grupo; guardar lleva al editor del borrador;
  Crear ▾ Nota de crédito lista solo facturas con pendiente; Crear ▾ Cobro precarga el cliente; Ver ▾ lleva a movimientos,
  estado de cuenta, cobros y facturas filtradas; desde un producto, Crear ▾ Ajuste crea el lote y abre la línea del
  producto; Facturas ▸ seleccionar ▸ Crear ▾ Nota de crédito. Como `supervisor`: no ve Factura/Nota; sí Cobro.
- [ ] **Step 4: Sin commit si no hubo arreglos.**

---
## Fase D — Entregable Parte 1

Carpeta `docs/entregable-parte-1/`. Estilo del entregable 3 (`docs/ENTREGABLE-Final-A00120258.md`): español formal,
secciones numeradas, tablas de apoyo, **sin portada** ni video. El `.md` es la fuente; el único formato de entrega del
documento es `.docx` con párrafos justificados. Tooling en `tests/e2e/` (Node, fuera de `test.slnx`).

| Task | Cuándo puede empezar | Depende de |
|---|---|---|
| D1 Textos Pasos 1–5 | **Inmediatamente**, en paralelo con A1/A3a/D2 | — |
| D2 Diagramas (arquitectura + ER del esquema real) | **Inmediatamente**, en paralelo con A1/A3a/D1 | stack Docker levantado (esquema migrado) |
| D3 Suite E2E + capturas | Tras **G-C** | UI final (A–C) desplegada en Docker |
| D4 Documento final, presentación y guion | Tras **D3** | capturas, reporte E2E, resumen xUnit |

### Task D1: Documento técnico — Pasos 1 a 5 (textos)

**Grupo de paralelismo:** Inicio (en paralelo con A1, A3a y D2). Solo toca `docs/entregable-parte-1/documento-tecnico.md`.

**Files:**
- Create: `docs/entregable-parte-1/documento-tecnico.md`
- Create: `tests/e2e/scripts/verificar-documento.mjs`

**Interfaces:**
- Produces: `documento-tecnico.md` con las secciones `## 1.` … `## 5.` completas y los encabezados vacíos `## 6.` … `## 10.` marcados `<!-- D4 -->` (D4 los rellena); referencias a `diagramas/arquitectura.png` y `diagramas/modelo-er.png` (D2 los genera); `verificar-documento.mjs` (D4 lo reutiliza).

- [ ] **Step 1: Verificador del documento (falla: no existe el documento)**

`tests/e2e/scripts/verificar-documento.mjs`:

```javascript
#!/usr/bin/env node
// Verifica la estructura de docs/entregable-parte-1/documento-tecnico.md (Fix-Features D1/D4).
// Uso: node tests/e2e/scripts/verificar-documento.mjs [--completo]
import { readFile } from 'node:fs/promises';

const ruta = new URL('../../../docs/entregable-parte-1/documento-tecnico.md', import.meta.url);
const texto = await readFile(ruta, 'utf8');
const completo = process.argv.includes('--completo');
const fallos = [];

const secciones = [
  '## 1. Planteamiento del problema',
  '## 2. Tecnología y lenguaje',
  '## 3. Arquitectura del sistema',
  '## 4. Diseño de la base de datos',
  '## 5. Persistencia de datos',
  '## 6. Mantenimientos (CRUD)',
  '## 7. Búsquedas y consultas',
  '## 8. Menú y navegación',
  '## 9. Pruebas',
  '## 10. Entrega',
];
for (const s of secciones) if (!texto.includes(s)) fallos.push(`Falta la sección "${s}"`);
if (!texto.startsWith('# AxionERP')) fallos.push('El documento debe empezar por el título (sin portada).');
for (const termino of ['AxionERP', 'Administrador', 'Supervisor', 'Ejecutor', 'Blazor', 'PostgreSQL', 'MediatR', 'Dapper', 'SociosNegocio', 'AspNetUsers']) {
  if (!texto.includes(termino)) fallos.push(`No aparece "${termino}"`);
}
for (const imagen of ['diagramas/arquitectura.png', 'diagramas/modelo-er.png']) {
  if (!texto.includes(imagen)) fallos.push(`No se referencia ${imagen}`);
}
if (completo) {
  if (texto.includes('<!-- D4 -->')) fallos.push('Quedan secciones pendientes de D4.');
  if (!/capturas\/[\w-]+\.png/.test(texto)) fallos.push('No hay capturas referenciadas.');
}

if (fallos.length) {
  console.error(fallos.map((f) => `✗ ${f}`).join('\n'));
  process.exit(1);
}
console.log(`✓ documento-tecnico.md (${completo ? 'completo' : 'pasos 1-5'})`);
```

Run: `node tests/e2e/scripts/verificar-documento.mjs`
Expected: FAIL (`ENOENT`: el documento no existe).

- [ ] **Step 2: Escribir las secciones 1–5**

Crear `docs/entregable-parte-1/documento-tecnico.md` con este esqueleto y redactar cada sección en prosa (párrafos de 3–6
líneas, tercera persona, sin viñetas salvo en listas de objetivos) con los datos indicados:

```markdown
# AxionERP — Documento técnico (Entregable, Parte 1)

**Asignatura:** Desarrollo de Software con Tecnologías Propietarias y Open Source I (ISO-615)
**Proyecto:** AxionERP · **Repositorio:** OpenSource1 · **Rama:** Fix-Features

> Por indicación académica, este documento no incluye portada. Las capturas provienen de una ejecución real del sistema
> sobre Docker Compose y las pruebas se ejecutaron sobre la misma versión del código.

## 1. Planteamiento del problema
### 1.1 Nombre del sistema
### 1.2 Situación actual
### 1.3 Objetivo general
### 1.4 Objetivos específicos
### 1.5 Usuarios del sistema
### 1.6 Alcance
### 1.7 Limitaciones
### 1.8 Beneficios esperados

## 2. Tecnología y lenguaje
### 2.1 Plataforma elegida
### 2.2 Justificación
### 2.3 Por qué Blazor Static SSR en lugar de MVC con Razor Views

## 3. Arquitectura del sistema
![Arquitectura de AxionERP](diagramas/arquitectura.png)
### 3.1 Flujo de una petición
### 3.2 Capas y proyectos
### 3.3 Seguridad en la arquitectura

## 4. Diseño de la base de datos
![Modelo entidad-relación (núcleo)](diagramas/modelo-er.png)
### 4.1 Bases de datos
### 4.2 Correspondencia con las entidades solicitadas
### 4.3 Claves y tipos principales

## 5. Persistencia de datos
### 5.1 Code First con migraciones de EF Core
### 5.2 Lecturas con Dapper
### 5.3 Transacciones y concurrencia

## 6. Mantenimientos (CRUD)
<!-- D4 -->
## 7. Búsquedas y consultas
<!-- D4 -->
## 8. Menú y navegación
<!-- D4 -->
## 9. Pruebas
<!-- D4 -->
## 10. Entrega
<!-- D4 -->
```

Contenido obligatorio por sección (redactado, no en viñetas salvo 1.4):
- **1.1–1.2:** AxionERP, sistema de gestión empresarial para una pyme comercial dominicana: hoy clientes, productos, existencias, facturación y cuentas por cobrar se llevan en hojas dispersas, sin control de acceso ni trazabilidad, con errores de existencia y saldos.
- **1.3–1.4:** objetivo general (centralizar la operación comercial con control de acceso por rol); específicos: mantenimientos de clientes, productos, categorías y usuarios; inventario por libro de movimientos; facturación y notas de crédito con posteo contable; cuentas por cobrar con cobros y aplicaciones; búsquedas por varios criterios y búsqueda global; reportes PDF/Excel; bitácora.
- **1.5:** tabla de roles (Administrador: consultar, agregar, modificar, eliminar y configurar; Supervisor: consultar y modificar; Ejecutor: consultar y agregar), igual que la sección "Roles y permisos" del `README.md`.
- **1.6–1.8:** alcance (módulos de la tabla de grupos del menú: Clientes, Productos, Inventario, Ventas, Facturación, Contabilidad, Reportes, Configuración, Administración); limitaciones (una moneda DOP, sin facturación electrónica ante la DGII, sin compras/proveedores, despliegue local con Docker); beneficios (una sola fuente de datos, permisos coherentes UI/API, trazabilidad de documentos posteados append-only).
- **2:** ASP.NET Core 10 con C# (LTS de noviembre de 2025): API REST con controladores MVC + Blazor Web App en modo Static SSR como capa de vistas; justificación: soporte LTS, rendimiento de Kestrel, tipado estático compartido (DTOs de `OpenSource1.Application` usados por API y UI), ecosistema EF Core + Dapper, mantenibilidad y pruebas (xUnit, WebApplicationFactory, Testcontainers). 2.3: SSR produce HTML en servidor como MVC, pero con componentes reutilizables (`PageToolbar`, `EntityFormPage`, `*Fields.razor`), formularios con antiforgery y enlace de modelos (`[SupplyParameterFromForm]`), navegación mejorada sin recargar, y sin runtime interactivo (sin WebSocket ni WebAssembly), lo que mantiene el JWT fuera del navegador (cookie HttpOnly + sesión de servidor).
- **3:** flujo Usuario → Interfaz (Blazor) → Controlador (API) → Lógica (Application/MediatR con behaviors de logging, validación y transacción) → Acceso a datos (EF Core para escrituras y migraciones, Dapper para lecturas) → PostgreSQL; tabla de proyectos (`OpenSource1.Core`, `.Application`, `.Infrastructure`, `.Api`, `.Blazor`) con su responsabilidad (texto de la sección "Arquitectura" del `README.md`); 3.3: JWT emitido por la API y guardado en sesión de servidor, cookie HttpOnly/SameSite=Strict, políticas `CanConsult/CanAdd/CanModify/CanDelete/CanAdministrar` evaluadas en ambos lados, la UI oculta y la API decide (403 real).
- **4:** dos bases (`AxionERP_App` y `AxionERP_Identity`); tabla de correspondencia:

```markdown
| Entidad solicitada | Tabla(s) reales | Observaciones |
| - | - | - |
| Usuarios / Roles | `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles` (base `AxionERP_Identity`) | ASP.NET Core Identity; permisos coarse derivados del rol. |
| Clientes | `SociosNegocio` | Código correlativo, tipo, documento fiscal (RNC/cédula), término de pago, límite de crédito, grupos contables. |
| Productos | `Productos` | Precio, unidad base, método de costeo, costo unitario vigente, grupos contables. |
| Categorías | `CategoriasProducto` | Jerarquía con categoría padre. |
| Inventario | `MovimientosProducto`, `MovimientosValor`, `Almacenes` | Libro append-only de cantidades y de costo por almacén. |
| Ventas / DetalleVenta / Facturas | `FacturasVenta`, `LineasFacturaVenta`, `LineasIvaFacturaVenta` (+ `FacturasVentaBorrador`, `LineasFacturaVentaBorrador`) | Borrador editable y documento posteado inmutable. |
| CuentasPorCobrar | `MovimientosCliente`, `MovimientosClienteDetalle` | Importe restante derivado; cobros y aplicaciones. |
```

  4.3 remite al diccionario `diagramas/diccionario-datos.md` (PK, FK y tipos, generado por D2) y resume: PK `uuid` en maestros, PK `Numero` (texto) en documentos posteados, `bigint` identidad en libros, `numeric(18,2)`/`numeric(18,6)` en importes y cantidades, `xmin` como token de concurrencia.
- **5:** Code First con **38** migraciones de EF Core (`src/OpenSource1.Infrastructure/Data/Migrations`), aplicadas al arrancar la API (`Database__ApplyMigrationsOnStartup`); justificación (esquema versionado junto al código, reproducible en Docker y en Testcontainers); Dapper para listados paginados y consultas con filtros (SQL explícito, `ILIKE … ESCAPE`, columnas de orden en lista blanca); transacciones por comando (`TransactionBehavior`) y concurrencia optimista con `xmin`.

- [ ] **Step 3: Verificar**

Run: `node tests/e2e/scripts/verificar-documento.mjs`
Expected: `✓ documento-tecnico.md (pasos 1-5)`.

- [ ] **Step 4: Commit**

```bash
git add docs/entregable-parte-1/documento-tecnico.md tests/e2e/scripts/verificar-documento.mjs
git commit -m "docs: documento tecnico del entregable parte 1, pasos 1 a 5"
```

---

### Task D2: Diagramas — arquitectura y modelo ER desde el esquema real

**Grupo de paralelismo:** Inicio (en paralelo con A1, A3a y D1). Solo toca `tests/e2e/` y `docs/entregable-parte-1/diagramas/`.

**Files:**
- Create: `tests/e2e/package.json`
- Create: `tests/e2e/.gitignore`
- Create: `tests/e2e/scripts/render-mermaid.mjs`
- Create: `tests/e2e/scripts/esquema_a_mermaid.py`
- Test: `tests/e2e/scripts/test_esquema_a_mermaid.py`
- Create: `docs/entregable-parte-1/diagramas/arquitectura.mmd` (+ `.svg`, `.png` generados)
- Create (generados): `docs/entregable-parte-1/diagramas/modelo-er.mmd`, `modelo-er.svg`, `modelo-er.png`, `modelo-er-completo.mmd`, `diccionario-datos.md`

**Interfaces:**
- Produces: `node tests/e2e/scripts/render-mermaid.mjs <entrada.mmd> <salida-sin-extension>` (usa el chromium de Playwright; no requiere `mmdc` ni graphviz); `python3 tests/e2e/scripts/esquema_a_mermaid.py --salida <dir>` (lee `information_schema` de `AxionERP_App` y `AxionERP_Identity` vía `docker compose exec -T postgres psql`); `construir_mermaid(columnas, restricciones, tablas)` y `construir_diccionario(columnas, restricciones, tablas)` como funciones puras. D3 reutiliza `package.json`.

- [ ] **Step 1: Paquete Node del tooling**

`tests/e2e/package.json`:

```json
{
  "name": "axionerp-e2e",
  "private": true,
  "type": "module",
  "description": "E2E (Playwright) y tooling del entregable de AxionERP. Fuera de test.slnx.",
  "scripts": {
    "test": "playwright test",
    "diagramas": "node scripts/render-mermaid.mjs ../../docs/entregable-parte-1/diagramas/arquitectura.mmd ../../docs/entregable-parte-1/diagramas/arquitectura && node scripts/render-mermaid.mjs ../../docs/entregable-parte-1/diagramas/modelo-er.mmd ../../docs/entregable-parte-1/diagramas/modelo-er"
  },
  "devDependencies": {
    "@playwright/test": "^1.55.0",
    "mermaid": "^11.4.0"
  }
}
```

`tests/e2e/.gitignore`:

```gitignore
node_modules/
test-results/
playwright-report/
.auth/
```

Run: `cd tests/e2e && npm install && npx playwright install chromium`
Expected: `node_modules/` creado; chromium disponible (reutiliza `~/.cache/ms-playwright` si la revisión coincide). Añadir `package-lock.json` al commit.

- [ ] **Step 2: Test de la conversión esquema → Mermaid (falla)**

`tests/e2e/scripts/test_esquema_a_mermaid.py`:

```python
import unittest

from esquema_a_mermaid import construir_diccionario, construir_mermaid

COLUMNAS = [
    {"table_name": "SociosNegocio", "column_name": "Id", "data_type": "uuid", "is_nullable": "NO", "ordinal_position": 1},
    {"table_name": "SociosNegocio", "column_name": "NombreComercial", "data_type": "character varying", "is_nullable": "NO", "ordinal_position": 2},
    {"table_name": "FacturasVenta", "column_name": "Numero", "data_type": "character varying", "is_nullable": "NO", "ordinal_position": 1},
    {"table_name": "FacturasVenta", "column_name": "SocioNegocioId", "data_type": "uuid", "is_nullable": "NO", "ordinal_position": 2},
    {"table_name": "FacturasVenta", "column_name": "FechaRegistro", "data_type": "date", "is_nullable": "NO", "ordinal_position": 3},
    {"table_name": "Otra", "column_name": "Id", "data_type": "uuid", "is_nullable": "NO", "ordinal_position": 1},
]
RESTRICCIONES = [
    {"table_name": "SociosNegocio", "column_name": "Id", "constraint_type": "PRIMARY KEY", "ref_table": None, "ref_column": None},
    {"table_name": "FacturasVenta", "column_name": "Numero", "constraint_type": "PRIMARY KEY", "ref_table": None, "ref_column": None},
    {"table_name": "FacturasVenta", "column_name": "SocioNegocioId", "constraint_type": "FOREIGN KEY", "ref_table": "SociosNegocio", "ref_column": "Id"},
    {"table_name": "Otra", "column_name": "Id", "constraint_type": "FOREIGN KEY", "ref_table": "SociosNegocio", "ref_column": "Id"},
]


class EsquemaAMermaidTests(unittest.TestCase):
    def test_entidades_atributos_y_relaciones_solo_de_las_tablas_pedidas(self):
        texto = construir_mermaid(COLUMNAS, RESTRICCIONES, ["SociosNegocio", "FacturasVenta"])

        self.assertTrue(texto.startswith("erDiagram"))
        self.assertIn("  SociosNegocio {", texto)
        self.assertIn("    uuid Id PK", texto)
        self.assertIn("    character_varying NombreComercial", texto)
        self.assertIn("    uuid SocioNegocioId FK", texto)
        self.assertIn('  SociosNegocio ||--o{ FacturasVenta : "SocioNegocioId"', texto)
        self.assertNotIn("Otra", texto)

    def test_diccionario_con_tipos_nulos_y_claves(self):
        texto = construir_diccionario(COLUMNAS, RESTRICCIONES, ["FacturasVenta"])

        self.assertIn("### FacturasVenta", texto)
        self.assertIn("| Columna | Tipo | Nulo | Clave |", texto)
        self.assertIn("| SocioNegocioId | uuid | No | FK → SociosNegocio.Id |", texto)
        self.assertIn("| Numero | character varying | No | PK |", texto)


if __name__ == "__main__":
    unittest.main()
```

Run: `cd tests/e2e/scripts && python3 -m unittest test_esquema_a_mermaid -v`
Expected: FAIL (`ModuleNotFoundError: esquema_a_mermaid`).

- [ ] **Step 3: Script de extracción**

`tests/e2e/scripts/esquema_a_mermaid.py`:

```python
#!/usr/bin/env python3
"""Modelo ER (Mermaid) y diccionario de datos desde el esquema REAL de PostgreSQL (information_schema).

Uso (desde la raíz del repositorio, con el stack de Docker levantado):
    python3 tests/e2e/scripts/esquema_a_mermaid.py --salida docs/entregable-parte-1/diagramas

Genera modelo-er.mmd (núcleo), modelo-er-completo.mmd (todas las tablas de AxionERP_App) y diccionario-datos.md.
"""
import argparse
import json
import os
import subprocess
from pathlib import Path

TABLAS_NUCLEO = {
    "AxionERP_App": [
        "SociosNegocio", "TerminosPago", "Productos", "CategoriasProducto", "UnidadesMedida", "Almacenes",
        "MovimientosProducto", "MovimientosValor", "FacturasVentaBorrador", "LineasFacturaVentaBorrador",
        "FacturasVenta", "LineasFacturaVenta", "LineasIvaFacturaVenta", "NotasCreditoVenta", "LineasNotaCreditoVenta",
        "MovimientosCliente", "MovimientosClienteDetalle",
    ],
    "AxionERP_Identity": ["AspNetUsers", "AspNetRoles", "AspNetUserRoles"],
}

SQL_COLUMNAS = """
SELECT coalesce(json_agg(t ORDER BY t.table_name, t.ordinal_position), '[]')
FROM (SELECT table_name, column_name, data_type, is_nullable, ordinal_position
      FROM information_schema.columns WHERE table_schema = 'public') t;
"""

SQL_RESTRICCIONES = """
SELECT coalesce(json_agg(t), '[]')
FROM (SELECT tc.table_name, kcu.column_name, tc.constraint_type, ccu.table_name AS ref_table, ccu.column_name AS ref_column
      FROM information_schema.table_constraints tc
      JOIN information_schema.key_column_usage kcu
        ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema
      LEFT JOIN information_schema.constraint_column_usage ccu
        ON tc.constraint_type = 'FOREIGN KEY' AND ccu.constraint_name = tc.constraint_name AND ccu.table_schema = tc.table_schema
      WHERE tc.table_schema = 'public' AND tc.constraint_type IN ('PRIMARY KEY', 'FOREIGN KEY')) t;
"""


def consultar(base, sql):
    """Ejecuta SQL dentro del contenedor postgres del compose (DOCKER_CONTEXT=default solo en el entorno del proceso)."""
    usuario = os.environ.get("POSTGRES_USER", "Rainiery")
    entorno = dict(os.environ, DOCKER_CONTEXT="default")
    salida = subprocess.run(
        ["docker", "compose", "exec", "-T", "postgres", "psql", "-U", usuario, "-d", base, "-At", "-c", sql],
        check=True, capture_output=True, text=True, env=entorno)
    return json.loads(salida.stdout.strip() or "[]")


def _claves(restricciones):
    claves = {}
    for r in restricciones:
        clave = (r["table_name"], r["column_name"])
        claves.setdefault(clave, set()).add("PK" if r["constraint_type"] == "PRIMARY KEY" else "FK")
    return claves


def _tipo(data_type):
    return data_type.replace(" ", "_").replace("(", "").replace(")", "")


def construir_mermaid(columnas, restricciones, tablas):
    incluidas = list(dict.fromkeys(tablas))
    conjunto = set(incluidas)
    claves = _claves(restricciones)
    lineas = ["erDiagram"]
    for tabla in incluidas:
        filas = sorted((c for c in columnas if c["table_name"] == tabla), key=lambda c: c["ordinal_position"])
        if not filas:
            continue
        lineas.append(f"  {tabla} {{")
        for c in filas:
            marcas = sorted(claves.get((tabla, c["column_name"]), set()), key=lambda m: 0 if m == "PK" else 1)
            sufijo = f" {', '.join(marcas)}" if marcas else ""
            lineas.append(f"    {_tipo(c['data_type'])} {c['column_name']}{sufijo}")
        lineas.append("  }")
    vistas = set()
    for r in restricciones:
        if r["constraint_type"] != "FOREIGN KEY":
            continue
        if r["table_name"] not in conjunto or r["ref_table"] not in conjunto:
            continue
        relacion = (r["ref_table"], r["table_name"], r["column_name"])
        if relacion in vistas:
            continue
        vistas.add(relacion)
        lineas.append(f'  {r["ref_table"]} ||--o{{ {r["table_name"]} : "{r["column_name"]}"')
    return "\n".join(lineas) + "\n"


def construir_diccionario(columnas, restricciones, tablas):
    claves = {}
    for r in restricciones:
        clave = (r["table_name"], r["column_name"])
        texto = "PK" if r["constraint_type"] == "PRIMARY KEY" else f'FK → {r["ref_table"]}.{r["ref_column"]}'
        claves.setdefault(clave, []).append(texto)
    partes = ["# Diccionario de datos (núcleo)", "", "Generado desde `information_schema` por `tests/e2e/scripts/esquema_a_mermaid.py`.", ""]
    for tabla in dict.fromkeys(tablas):
        filas = sorted((c for c in columnas if c["table_name"] == tabla), key=lambda c: c["ordinal_position"])
        if not filas:
            continue
        partes += [f"### {tabla}", "", "| Columna | Tipo | Nulo | Clave |", "| - | - | - | - |"]
        for c in filas:
            nulo = "Sí" if c["is_nullable"] == "YES" else "No"
            clave = ", ".join(sorted(set(claves.get((tabla, c["column_name"]), [])), key=lambda k: 0 if k == "PK" else 1))
            partes.append(f'| {c["column_name"]} | {c["data_type"]} | {nulo} | {clave} |')
        partes.append("")
    return "\n".join(partes)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--salida", required=True, type=Path)
    args = parser.parse_args()
    args.salida.mkdir(parents=True, exist_ok=True)

    columnas, restricciones, nucleo = [], [], []
    for base, tablas in TABLAS_NUCLEO.items():
        columnas += consultar(base, SQL_COLUMNAS)
        restricciones += consultar(base, SQL_RESTRICCIONES)
        nucleo += tablas

    (args.salida / "modelo-er.mmd").write_text(construir_mermaid(columnas, restricciones, nucleo), encoding="utf-8")
    todas_app = sorted({c["table_name"] for c in consultar("AxionERP_App", SQL_COLUMNAS)} - {"__EFMigrationsHistory"})
    (args.salida / "modelo-er-completo.mmd").write_text(construir_mermaid(columnas, restricciones, todas_app), encoding="utf-8")
    (args.salida / "diccionario-datos.md").write_text(construir_diccionario(columnas, restricciones, nucleo), encoding="utf-8")
    print(f"OK: {args.salida}/modelo-er.mmd, modelo-er-completo.mmd y diccionario-datos.md")


if __name__ == "__main__":
    main()
```

Run: `cd tests/e2e/scripts && python3 -m unittest test_esquema_a_mermaid -v`
Expected: PASS (2 tests).

- [ ] **Step 4: Render de Mermaid con el chromium de Playwright**

`tests/e2e/scripts/render-mermaid.mjs`:

```javascript
#!/usr/bin/env node
// Renderiza un .mmd a SVG y PNG con mermaid + el chromium de Playwright (sin mmdc ni graphviz). Fix-Features D2.
// Uso: node scripts/render-mermaid.mjs <entrada.mmd> <salida-sin-extension>
import { readFile, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { chromium } from '@playwright/test';

const require = createRequire(import.meta.url);
const [, , entrada, salida] = process.argv;
if (!entrada || !salida) {
  console.error('Uso: node scripts/render-mermaid.mjs <entrada.mmd> <salida-sin-extension>');
  process.exit(2);
}

const codigo = await readFile(entrada, 'utf8');
const navegador = await chromium.launch();
try {
  const pagina = await navegador.newPage({ deviceScaleFactor: 2, viewport: { width: 1800, height: 1200 } });
  await pagina.setContent('<!doctype html><html><body style="margin:0;background:#ffffff"><div id="d"></div></body></html>');
  await pagina.addScriptTag({ path: require.resolve('mermaid/dist/mermaid.min.js') });
  const svg = await pagina.evaluate(async (texto) => {
    window.mermaid.initialize({
      startOnLoad: false, theme: 'neutral', securityLevel: 'strict',
      er: { useMaxWidth: false }, flowchart: { useMaxWidth: false, htmlLabels: true },
    });
    const { svg: resultado } = await window.mermaid.render('diagrama', texto);
    document.getElementById('d').innerHTML = resultado;
    return resultado;
  }, codigo);
  await writeFile(`${salida}.svg`, svg, 'utf8');
  await pagina.locator('#d svg').screenshot({ path: `${salida}.png` });
  console.log(`OK ${salida}.svg y ${salida}.png`);
} finally {
  await navegador.close();
}
```

- [ ] **Step 5: Diagrama de arquitectura**

`docs/entregable-parte-1/diagramas/arquitectura.mmd`:

```text
flowchart LR
  U["Usuario<br/>(navegador)"] -->|"GET/POST HTML<br/>cookie HttpOnly"| B["Interfaz<br/>OpenSource1.Blazor<br/>Blazor Static SSR + Tailwind"]
  B -->|"HttpClient tipado<br/>JWT en sesión de servidor"| A["Controladores<br/>OpenSource1.Api<br/>ASP.NET Core MVC (REST)"]
  A -->|"MediatR ISender"| L["Lógica de negocio<br/>OpenSource1.Application<br/>Commands · Queries · Behaviors"]
  L -.->|"entidades y reglas"| C["Dominio<br/>OpenSource1.Core"]
  L --> E["Acceso a datos<br/>OpenSource1.Infrastructure<br/>EF Core (escrituras, migraciones)"]
  L --> D["Acceso a datos<br/>OpenSource1.Infrastructure<br/>Dapper (lecturas)"]
  E --> P[("PostgreSQL 17<br/>AxionERP_App")]
  D --> P
  A -->|"ASP.NET Core Identity"| I[("PostgreSQL 17<br/>AxionERP_Identity")]
```

- [ ] **Step 6: Generar ER y diagramas desde el stack real**

Run (desde la raíz; el stack debe estar arriba con el esquema migrado):

```bash
docker compose up -d postgres api
python3 tests/e2e/scripts/esquema_a_mermaid.py --salida docs/entregable-parte-1/diagramas
cd tests/e2e && npm run diagramas
```

Expected: existen `docs/entregable-parte-1/diagramas/{arquitectura,modelo-er}.{mmd,svg,png}`, `modelo-er-completo.mmd` y
`diccionario-datos.md`; abrir los PNG y comprobar que se leen (si el ER núcleo resulta ilegible, partirlo en dos `.mmd`
—maestros e inventario / ventas y CxC— reutilizando `construir_mermaid` con dos listas y renderizar ambos).

- [ ] **Step 7: Commit**

```bash
git add tests/e2e/package.json tests/e2e/package-lock.json tests/e2e/.gitignore tests/e2e/scripts/render-mermaid.mjs tests/e2e/scripts/esquema_a_mermaid.py tests/e2e/scripts/test_esquema_a_mermaid.py docs/entregable-parte-1/diagramas
git commit -m "docs: diagramas de arquitectura y modelo ER generados desde el esquema real"
```

---

### Task D3: Suite E2E (Playwright) y capturas contra el stack de Docker

**Grupo de paralelismo:** tras **G-C** (necesita la UI final). Solo toca `tests/e2e/` y `docs/entregable-parte-1/{capturas,evidencias}/`.

**Files:**
- Create: `tests/e2e/playwright.config.ts`
- Create: `tests/e2e/scripts/con-credenciales.sh`
- Create: `tests/e2e/specs/ayuda.ts`
- Create: `tests/e2e/specs/01-acceso-menu.spec.ts`
- Create: `tests/e2e/specs/02-clientes-crud.spec.ts`
- Create: `tests/e2e/specs/03-productos-busquedas.spec.ts`
- Create: `tests/e2e/specs/04-categorias-usuarios.spec.ts`
- Create: `tests/e2e/specs/05-busqueda-global.spec.ts`
- Create: `tests/e2e/specs/06-acciones-documentos.spec.ts`
- Create: `tests/e2e/specs/07-errores.spec.ts`
- Create: `tests/e2e/specs/08-sin-flash.spec.ts`
- Create (generados): `docs/entregable-parte-1/capturas/*.png`, `docs/entregable-parte-1/evidencias/reporte-e2e/`

**Interfaces:**
- Consumes: `tests/e2e/package.json` (D2); UI de A–C; usuarios semilla `admin`, `supervisor`, `ejecutor` con la contraseña `AUTH_SEED_DEFAULT_PASSWORD` de `.env`.
- Produces: capturas `NN-nombre.png` (1440×900, claro; `*-oscuro.png` en modo oscuro) que D4 inserta en el documento; reporte HTML en `evidencias/reporte-e2e/`.

- [ ] **Step 1: Configuración y utilidades**

`tests/e2e/playwright.config.ts`:

```typescript
import { defineConfig, devices } from '@playwright/test';

// E2E contra el stack de Docker Compose (Blazor en :8080). Credenciales solo por variables de entorno (ver ayuda.ts).
export default defineConfig({
  testDir: './specs',
  timeout: 60_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  workers: 1,
  reporter: [['list'], ['html', { outputFolder: '../../docs/entregable-parte-1/evidencias/reporte-e2e', open: 'never' }]],
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:8080',
    locale: 'es-DO',
    viewport: { width: 1440, height: 900 },
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'], viewport: { width: 1440, height: 900 } } }],
});
```

`tests/e2e/scripts/con-credenciales.sh`:

```bash
#!/usr/bin/env bash
# Exporta E2E_PASSWORD desde el .env de la raíz (AUTH_SEED_DEFAULT_PASSWORD) sin escribirlo en ningún archivo, y ejecuta
# el comando recibido. Uso (desde tests/e2e): bash scripts/con-credenciales.sh npx playwright test
set -euo pipefail
raiz="$(cd "$(dirname "$0")/../../.." && pwd)"
if [[ -z "${E2E_PASSWORD:-}" ]]; then
  E2E_PASSWORD="$(grep -E '^AUTH_SEED_DEFAULT_PASSWORD=' "$raiz/.env" | head -n1 | cut -d= -f2-)"
  export E2E_PASSWORD
fi
exec "$@"
```

`tests/e2e/specs/ayuda.ts`:

```typescript
import { expect, type Page } from '@playwright/test';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

// El paquete es ESM ("type": "module"): sin __dirname; la ruta se resuelve desde este archivo (tests/e2e/specs).
export const CAPTURAS = fileURLToPath(new URL('../../../docs/entregable-parte-1/capturas/', import.meta.url));

export const usuarios = {
  admin: process.env.E2E_ADMIN_USER ?? 'admin',
  supervisor: process.env.E2E_SUPERVISOR_USER ?? 'supervisor',
  ejecutor: process.env.E2E_EJECUTOR_USER ?? 'ejecutor',
} as const;

function contrasena(): string {
  const valor = process.env.E2E_PASSWORD;
  if (!valor) throw new Error('Defina E2E_PASSWORD (o use scripts/con-credenciales.sh, que la lee de .env).');
  return valor;
}

export async function iniciarSesion(page: Page, usuario: string): Promise<void> {
  await page.goto('/account/login');
  await page.locator('#userNameOrEmail').fill(usuario);
  await page.locator('#password').fill(contrasena());
  await page.getByRole('button', { name: /iniciar sesión|entrar/i }).click();
  await expect(page.getByTestId('nav-menu')).toBeVisible();
}

export async function captura(page: Page, nombre: string): Promise<void> {
  await page.screenshot({ path: path.join(CAPTURAS, `${nombre}.png`) });
}

export const unico = (prefijo: string): string => `${prefijo}-${Date.now().toString(36).toUpperCase()}`;
```

- [ ] **Step 2: Specs**

`tests/e2e/specs/01-acceso-menu.spec.ts`:

```typescript
import { expect, test } from '@playwright/test';
import { captura, iniciarSesion, usuarios } from './ayuda';

test('login, inicio con grupos y menú por grupos del administrador', async ({ page }) => {
  await page.goto('/account/login');
  await captura(page, '01-login');
  await iniciarSesion(page, usuarios.admin);
  await expect(page.getByTestId('grupos-inicio')).toBeVisible();
  await captura(page, '02-inicio');
  await page.goto('/unidades-medida');
  await expect(page.locator('details[data-grupo="configuracion"]')).toHaveAttribute('open', '');
  await captura(page, '03-menu-grupos');
  await page.goto('/modulos/facturacion');
  await captura(page, '04-grupo-facturacion');
});

test('ejecutor no ve Administración y el modo oscuro se conserva', async ({ page, context }) => {
  await iniciarSesion(page, usuarios.ejecutor);
  await expect(page.locator('details[data-grupo="administracion"]')).toHaveCount(0);
  await context.addCookies([{ name: 'axionerp-theme', value: 'dark', url: page.url() }]);
  await page.goto('/');
  await expect(page.locator('html')).toHaveClass(/dark/);
  await captura(page, '05-inicio-ejecutor-oscuro');
});
```

`tests/e2e/specs/02-clientes-crud.spec.ts`:

```typescript
import { expect, test } from '@playwright/test';
import { captura, iniciarSesion, unico, usuarios } from './ayuda';

test('clientes: agregar, consultar, modificar, eliminar y validación', async ({ page }) => {
  const nombre = unico('E2E Cliente');
  await iniciarSesion(page, usuarios.admin);

  await page.goto('/clientes');
  await page.getByTestId('accion-nuevo').click();
  await expect(page.getByTestId('entity-form-page')).toBeVisible();
  await page.getByTestId('guardar').click();
  await expect(page.locator('.validation-message').first()).toBeVisible();
  await captura(page, '10-cliente-validacion');

  await page.locator('[name="Input.NombreComercial"]').fill(nombre);
  await page.locator('[name="Input.Email"]').fill(`${nombre.replace(/\s/g, '').toLowerCase()}@e2e.local`);
  await captura(page, '11-cliente-alta');
  await page.getByTestId('guardar').click();
  await expect(page).toHaveURL(/\/clientes\?.*ok=created/);

  await page.goto(`/clientes?view=list&nombre=${encodeURIComponent(nombre)}`);
  await expect(page.getByRole('cell', { name: nombre })).toBeVisible();
  await page.getByTestId('seleccionar-fila').first().click();
  await captura(page, '12-cliente-seleccion-acciones');
  await page.getByTestId('menu-crear').locator('summary').click();
  await captura(page, '13-cliente-menu-crear');

  await page.getByTestId('accion-editar').click();
  await page.locator('[name="Input.Telefono"]').fill('809-555-0101');
  await page.getByTestId('guardar').click();
  await expect(page).toHaveURL(/ok=updated/);
  await captura(page, '14-cliente-modificado');

  await page.getByTestId('seleccionar-fila').first().click();
  await page.getByTestId('accion-eliminar').click();
  await captura(page, '15-cliente-eliminar-confirmacion');
  await page.getByRole('button', { name: 'Sí, eliminar' }).click();
  await expect(page).toHaveURL(/ok=deleted/);
});
```

`tests/e2e/specs/03-productos-busquedas.spec.ts`:

```typescript
import { expect, test } from '@playwright/test';
import { captura, iniciarSesion, unico, usuarios } from './ayuda';

test('productos: alta y búsquedas por código, nombre, categoría, precio y estado', async ({ page }) => {
  const codigo = unico('E2E').slice(0, 14);
  await iniciarSesion(page, usuarios.admin);

  await page.goto('/productos/nuevo');
  await page.locator('[name="Input.Codigo"]').fill(codigo);
  await page.locator('[name="Input.Nombre"]').fill(`Producto ${codigo}`);
  await page.locator('[name="Input.PrecioVentaTexto"]').fill('125.50');
  await page.getByTestId('guardar').click();
  await expect(page).toHaveURL(/ok=created/);

  const busquedas: [string, string][] = [
    ['16-producto-busqueda-codigo', `/productos?view=list&filters=codigo&codigo=${codigo}`],
    ['17-producto-busqueda-nombre', `/productos?view=list&nombre=${encodeURIComponent(`Producto ${codigo}`)}`],
    ['18-producto-busqueda-categoria', `/productos?view=list&filters=categoriaCodigo&categoriaCodigo=GENERAL&nombre=${codigo}`],
    ['19-producto-busqueda-precio', `/productos?view=list&filters=precioVenta&precioVenta=125.50&nombre=${codigo}`],
    ['20-producto-busqueda-estado', `/productos?view=list&filters=estado&estado=without&nombre=${codigo}`],
  ];
  for (const [nombre, url] of busquedas) {
    await page.goto(url);
    await expect(page.getByText(codigo).first()).toBeVisible();
    await captura(page, nombre);
  }
});
```

`tests/e2e/specs/04-categorias-usuarios.spec.ts`:

```typescript
import { expect, test } from '@playwright/test';
import { captura, iniciarSesion, unico, usuarios } from './ayuda';

test('categorías: agregar, modificar y eliminar', async ({ page }) => {
  const codigo = unico('CAT').slice(0, 20);
  await iniciarSesion(page, usuarios.admin);
  await page.goto('/categorias-producto/nuevo');
  await page.locator('[name="SaveInput.Codigo"]').fill(codigo);
  await page.locator('[name="SaveInput.Nombre"]').fill(`Categoría ${codigo}`);
  await captura(page, '21-categoria-alta');
  await page.getByTestId('guardar').click();
  await expect(page).toHaveURL(/ok=created/);
  await page.goto(`/categorias-producto?codigo=${codigo}`);
  await page.getByTestId('seleccionar-fila').first().click();
  await page.getByTestId('accion-editar').click();
  await page.locator('[name="UpdateInput.Nombre"]').fill(`Categoría ${codigo} editada`);
  await page.getByTestId('guardar').click();
  await expect(page).toHaveURL(/ok=updated/);
  await page.getByTestId('seleccionar-fila').first().click();
  await page.getByTestId('accion-eliminar').click();
  await page.getByRole('button', { name: 'Sí, eliminar' }).click();
  await expect(page).toHaveURL(/ok=deleted/);
});

test('usuarios: alta en página propia y ficha para rol/estado', async ({ page }) => {
  const correo = `${unico('e2e').toLowerCase()}@e2e.local`;
  await iniciarSesion(page, usuarios.admin);
  await page.goto('/admin/users');
  await page.getByTestId('accion-nuevo').click();
  await page.locator('[name="CreateInput.FullName"]').fill('Usuario E2E');
  await page.locator('[name="CreateInput.Email"]').fill(correo);
  await page.locator('[name="CreateInput.Password"]').fill('E2e#Clave123');
  await captura(page, '22-usuario-alta');
  await page.getByTestId('guardar').click();
  await expect(page).toHaveURL(/\/admin\/users\?ok=/);
  await captura(page, '23-usuarios-listado');
});
```

`tests/e2e/specs/05-busqueda-global.spec.ts`:

```typescript
import { expect, test } from '@playwright/test';
import { captura, iniciarSesion, usuarios } from './ayuda';

test('paleta Ctrl+K: módulos al escribir, flechas, Enter y Esc', async ({ page }) => {
  await iniciarSesion(page, usuarios.admin);
  await page.goto('/');
  await page.keyboard.press('Control+k');
  await expect(page.locator('#busqueda-paleta')).toBeVisible();
  await page.keyboard.type('categoria');
  await expect(page.locator('#busqueda-paleta-lista [role="option"]').first()).toContainText('Categorías');
  await captura(page, '24-paleta-modulos');
  await page.keyboard.press('ArrowDown');
  await expect(page.locator('#busqueda-global')).toHaveAttribute('aria-activedescendant', 'busqueda-opcion-0');
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/\/categorias-producto/);
  await page.keyboard.press('/');
  await expect(page.locator('#busqueda-paleta')).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.locator('#busqueda-paleta')).toBeHidden();
});

test('búsqueda en servidor /buscar con módulos y registros', async ({ page }) => {
  await iniciarSesion(page, usuarios.admin);
  await page.goto('/buscar?q=general');
  await expect(page.getByTestId('resultados-modulos')).toBeVisible();
  await captura(page, '25-buscar-resultados');
});
```

`tests/e2e/specs/06-acciones-documentos.spec.ts`:

```typescript
import { expect, test } from '@playwright/test';
import { captura, iniciarSesion, usuarios } from './ayuda';

test('desde un cliente: Crear ▾ Factura abre la nueva factura con el cliente precargado', async ({ page }) => {
  await iniciarSesion(page, usuarios.admin);
  await page.goto('/clientes?view=list');
  await page.getByTestId('seleccionar-fila').first().click();
  await page.getByTestId('menu-crear').locator('summary').click();
  await page.getByRole('menuitem', { name: 'Factura' }).click();
  await expect(page.getByTestId('cliente-precargado')).toBeVisible();
  await captura(page, '26-nueva-factura-precargada');
});

test('facturas filtradas por cliente y ficha de producto con acciones', async ({ page }) => {
  await iniciarSesion(page, usuarios.admin);
  await page.goto('/productos?view=list');
  await page.getByTestId('seleccionar-fila').first().click();
  await page.getByTestId('menu-ver').locator('summary').click();
  await captura(page, '27-producto-menu-ver');
  await page.goto('/facturas-venta');
  await captura(page, '28-facturas');
});
```

`tests/e2e/specs/07-errores.spec.ts`:

```typescript
import { expect, test } from '@playwright/test';
import { captura, iniciarSesion, usuarios } from './ayuda';

test('403: el ejecutor no puede abrir Usuarios', async ({ page }) => {
  await iniciarSesion(page, usuarios.ejecutor);
  await page.goto('/admin/users');
  await expect(page.getByText(/no tiene autorización|acceso denegado/i)).toBeVisible();
  await captura(page, '29-error-403');
});

test('404: grupo inexistente', async ({ page }) => {
  await iniciarSesion(page, usuarios.admin);
  const respuesta = await page.goto('/modulos/noexiste');
  expect(respuesta?.status()).toBe(404);
  await captura(page, '30-error-404');
});

// Ejecutar aparte con la API detenida (ver Step 3): docker compose stop api
test('@api-caida: la página responde con aviso si la API no está disponible', async ({ page }) => {
  test.skip(process.env.E2E_API_CAIDA !== '1', 'Solo con la API detenida (E2E_API_CAIDA=1).');
  await page.goto('/account/login');
  await captura(page, '31-error-api-caida');
});
```

(Con la API detenida no se puede iniciar sesión; el caso captura el aviso real que muestra el login. Si la sesión ya
estaba iniciada antes de detener la API, `/clientes` muestra "No fue posible cargar los clientes."; el guion de la demo
lo recorre así.)

`tests/e2e/specs/08-sin-flash.spec.ts`:

```typescript
import { expect, test } from '@playwright/test';
import { captura, iniciarSesion, usuarios } from './ayuda';

test('navegación entre páginas sin destello azul', async ({ page }) => {
  await iniciarSesion(page, usuarios.admin);
  await page.goto('/unidades-medida');
  const barra = page.locator('#nav-progress-bar');
  await expect(barra).toHaveCSS('height', '2px');
  expect(await page.locator('[class*="from-brand-700"]').count()).toBe(0);

  const opacidades: string[] = [];
  const navegar = page.locator('a[href="/terminos-pago"]').first().click();
  for (let i = 0; i < 8; i++) {
    opacidades.push(await barra.evaluate((el) => getComputedStyle(el).opacity));
    await page.waitForTimeout(25);
  }
  await navegar;
  await expect(page).toHaveURL(/\/terminos-pago/);
  await captura(page, '32-navegacion-sin-flash');
  // En navegaciones locales rápidas (< 150 ms) la barra no llega a mostrarse.
  expect(opacidades.slice(0, 5).every((o) => Number(o) === 0)).toBe(true);
});
```

- [ ] **Step 3: Ejecutar contra el stack y generar capturas**

Run (desde la raíz):

```bash
docker compose up -d --build
grep -E '^POSTGRES_PORT=' .env && docker compose port postgres 5432
cd tests/e2e && bash scripts/con-credenciales.sh npx playwright test
```

Expected: todas las specs PASS salvo `@api-caida` (skipped); capturas en `docs/entregable-parte-1/capturas/`; reporte en
`docs/entregable-parte-1/evidencias/reporte-e2e/index.html`; `docker compose port postgres 5432` muestra el puerto de `.env`.
Después, para el caso de API caída:

```bash
docker compose stop api
cd tests/e2e && E2E_API_CAIDA=1 bash scripts/con-credenciales.sh npx playwright test specs/07-errores.spec.ts --grep "@api-caida"
docker compose start api
```

(En fish: `env E2E_API_CAIDA=1 bash scripts/con-credenciales.sh …`.)

- [ ] **Step 4: Commit**

```bash
git add tests/e2e/playwright.config.ts tests/e2e/scripts/con-credenciales.sh tests/e2e/specs docs/entregable-parte-1/capturas docs/entregable-parte-1/evidencias/reporte-e2e
git commit -m "test: suite e2e de playwright con capturas del entregable parte 1"
```

---

### Task D4: Documento final (.docx), presentación, guion y evidencias

**Grupo de paralelismo:** serie (tras D3).

**Files:**
- Modify: `docs/entregable-parte-1/documento-tecnico.md` (secciones 6–10)
- Create: `docs/entregable-parte-1/evidencias/resumen-xunit.txt`, `docs/entregable-parte-1/evidencias/resumen-e2e.md`
- Create: `docs/entregable-parte-1/documento-tecnico.docx` (skill `anthropic-skills:docx`)
- Create: `docs/entregable-parte-1/presentacion.pptx` (skill `anthropic-skills:pptx`)
- Create: `docs/entregable-parte-1/guion-demo.md`
- Create: `docs/entregable-parte-1/README.md` (índice del entregable)
- Create: `tests/e2e/scripts/verificar-office.py`

- [ ] **Step 1: Verificador de los archivos Office (falla: no existen)**

`tests/e2e/scripts/verificar-office.py`:

```python
#!/usr/bin/env python3
"""Verifica el .docx (justificado, sin portada, con imágenes) y el .pptx (8–10 diapositivas) del entregable (D4)."""
import re
import sys
import zipfile
from pathlib import Path

RAIZ = Path(__file__).resolve().parents[3] / "docs" / "entregable-parte-1"
fallos = []

docx = RAIZ / "documento-tecnico.docx"
with zipfile.ZipFile(docx) as z:
    xml = z.read("word/document.xml").decode("utf-8")
    medios = [n for n in z.namelist() if n.startswith("word/media/")]
parrafos = re.findall(r"<w:p[ >].*?</w:p>", xml, flags=re.S)
con_texto = [p for p in parrafos if "<w:t" in p]
justificados = [p for p in con_texto if 'w:jc w:val="both"' in p]
if len(justificados) < len(con_texto) * 0.5:
    fallos.append(f"Solo {len(justificados)} de {len(con_texto)} párrafos con texto están justificados.")
primer_texto = re.sub(r"<[^>]+>", "", con_texto[0]) if con_texto else ""
if "portada" in primer_texto.lower() or not primer_texto.strip().startswith("AxionERP"):
    fallos.append(f"El documento no empieza por el título (posible portada): {primer_texto[:60]!r}")
if 'w:type="page"' in "".join(parrafos[:3]):
    fallos.append("Hay un salto de página al inicio (portada).")
if len(medios) < 10:
    fallos.append(f"Solo {len(medios)} imágenes embebidas (diagramas + capturas).")

pptx = RAIZ / "presentacion.pptx"
with zipfile.ZipFile(pptx) as z:
    diapositivas = [n for n in z.namelist() if re.fullmatch(r"ppt/slides/slide\d+\.xml", n)]
if not 8 <= len(diapositivas) <= 10:
    fallos.append(f"La presentación tiene {len(diapositivas)} diapositivas (se esperan 8–10).")

if fallos:
    print("\n".join(f"✗ {f}" for f in fallos))
    sys.exit(1)
print(f"✓ docx ({len(con_texto)} párrafos, {len(medios)} imágenes) y pptx ({len(diapositivas)} diapositivas)")
```

Run: `python3 tests/e2e/scripts/verificar-office.py`
Expected: FAIL (`FileNotFoundError`).

- [ ] **Step 2: Evidencias de pruebas**

Run (desde la raíz):

```bash
env DOCKER_CONTEXT=default dotnet test test.slnx 2>&1 | tee docs/entregable-parte-1/evidencias/resumen-xunit.txt | tail -n 5
```

Expected: `Passed!` con el total (≥ 1361 + los nuevos). Escribir `docs/entregable-parte-1/evidencias/resumen-e2e.md` con
la tabla spec → resultado → capturas (del `list` reporter de D3) y el enlace a `reporte-e2e/index.html`.

- [ ] **Step 3: Secciones 6–10 del documento**

Sustituir cada `<!-- D4 -->` por su contenido:
- **6 Mantenimientos (CRUD):** Clientes, Productos, Categorías y Usuarios; para cada uno: crear (`/x/nuevo`), consultar
  (lista con filtros y selección), modificar (`/x/{id}/editar`), eliminar/desactivar (diálogo de confirmación; usuarios se
  desactivan desde su ficha) y limpiar campos; capturas `10`–`15`, `21`–`23`; permisos por rol (tabla del Paso 1).
- **7 Búsquedas y consultas:** productos por código, nombre, categoría, precio y estado de existencia (capturas `16`–`20`,
  sintaxis `*`, `||`, `&&` de los filtros); búsqueda global `/buscar` y paleta Ctrl+K (capturas `24`–`25`; módulos filtrados
  sin acentos, registros vía API con `ILIKE` escapado, 2–100 caracteres).
- **8 Menú y navegación:** grupos del registro de módulos (Inicio, Clientes, Productos, Inventario, Ventas, Facturación,
  Contabilidad, Reportes, Configuración, Administración), menú lateral con el grupo actual abierto, páginas de grupo con
  indicadores, barra de acciones `+ Nuevo / Crear ▾ / Ver ▾ / Editar / Eliminar`; capturas `02`–`05`, `12`–`13`, `26`–`28`, `32`.
- **9 Pruebas:** suite xUnit (total de `resumen-xunit.txt`; unitarias, integración API contra PostgreSQL con Testcontainers,
  SSR del host Blazor con `WebApplicationFactory` y `HtmlRenderer`), suite E2E Playwright (tabla de `resumen-e2e.md`):
  altas, modificaciones, eliminaciones, validaciones (`10`), búsquedas, errores 403 (`29`), 404 (`30`) y API caída (`31`).
- **10 Entrega:** índice (código fuente en el repositorio y rama; `documento-tecnico.docx`; `diagramas/`; `capturas/`;
  `evidencias/`; `presentacion.pptx`; `guion-demo.md`) y cómo levantar el sistema (`docker compose up -d --build`, puertos
  8080/8081, `POSTGRES_PORT` en `.env`, usuarios semilla).

Run: `node tests/e2e/scripts/verificar-documento.mjs --completo`
Expected: `✓ documento-tecnico.md (completo)`.

- [ ] **Step 4: Generar el `.docx` con la skill `anthropic-skills:docx`**

Invocar la skill `anthropic-skills:docx` con esta petición: *"Convierte `docs/entregable-parte-1/documento-tecnico.md` en
`docs/entregable-parte-1/documento-tecnico.docx`: sin portada (la primera línea es el título 'AxionERP — Documento
técnico (Entregable, Parte 1)'), todos los párrafos de texto con alineación justificada (`w:jc w:val="both"`), encabezados
con estilos Título 1/2/3, tablas con bordes simples, e imágenes embebidas desde las rutas relativas del Markdown
(`diagramas/*.png`, `capturas/*.png`) a ancho de página con su pie de figura; fuente Calibri 11, márgenes de 2,5 cm,
numeración de páginas en el pie; idioma español."* No generar PDF ni otra copia: el `.docx` es el único formato de entrega.

- [ ] **Step 5: Generar `presentacion.pptx` con la skill `anthropic-skills:pptx`**

Invocar la skill `anthropic-skills:pptx` pidiendo `docs/entregable-parte-1/presentacion.pptx`, **9 diapositivas**, en
español, estilo sobrio con el azul de marca `#2155d9` como acento:
1. AxionERP — título, asignatura, integrantes/autor, rama. 2. Problema y objetivos (Paso 1). 3. Usuarios y permisos (tabla de roles).
4. Tecnología: ASP.NET Core 10, Blazor Static SSR, PostgreSQL (Paso 2). 5. Arquitectura (`diagramas/arquitectura.png`).
6. Base de datos (`diagramas/modelo-er.png` + tabla de correspondencia resumida). 7. Mantenimientos y patrón de página
(capturas `12`/`13`). 8. Búsqueda global y paleta Ctrl+K (capturas `24`/`25`). 9. Pruebas y resultados (totales xUnit/E2E) y cierre.

- [ ] **Step 6: Guion de la demostración e índice**

`docs/entregable-parte-1/guion-demo.md`: guion de 8–10 minutos en pasos numerados con tiempo estimado, usuario y URL:
login (admin) → inicio con grupos → menú por grupos → Ctrl+K "categoria" → alta de cliente con validación → seleccionar
cliente → Crear ▾ Factura (precarga) → Productos: búsquedas por código/categoría/precio/estado → Ver ▾ Existencias →
Usuarios: alta → cerrar sesión → login ejecutor (sin Administración, 403 en `/admin/users`) → `/modulos/noexiste` (404) →
detener la API (`docker compose stop api`) y mostrar el aviso → `docker compose start api`. Incluir al inicio los
requisitos (`docker compose up -d --build`, `.env` con `AUTH_SEED_DEFAULT_PASSWORD` y `POSTGRES_PORT`).

`docs/entregable-parte-1/README.md`: índice de los artefactos con una línea por archivo/carpeta y cómo regenerarlos
(`python3 tests/e2e/scripts/esquema_a_mermaid.py …`, `npm run diagramas`, `bash scripts/con-credenciales.sh npx playwright test`).

- [ ] **Step 7: Verificar**

Run: `python3 tests/e2e/scripts/verificar-office.py && node tests/e2e/scripts/verificar-documento.mjs --completo`
Expected: `✓ docx (…) y pptx (9 diapositivas)` y `✓ documento-tecnico.md (completo)`. Abrir el `.docx` y comprobar a ojo que
no hay portada y que las imágenes se ven.

- [ ] **Step 8: Commit**

```bash
git add docs/entregable-parte-1 tests/e2e/scripts/verificar-office.py
git commit -m "docs: documento tecnico en word, presentacion, guion de demostracion y evidencias del entregable parte 1"
```

---

## Task Z: Cierre de la rama Fix-Features

**Grupo de paralelismo:** serie (último).

**Files:**
- Modify: `docs/superpowers/specs/2026-09-28-fix-features-navegacion-y-entregable-design.md` (sección final "Resultado")
- Modify: `docs/superpowers/plans/2026-09-28-fix-features.md` (sección final "Resultado de Fix-Features": commits, totales, decisiones durante la ejecución)
- Modify: `README.md` (sección "Estado actual": navegación por grupos, búsqueda global/Ctrl+K, patrón de página; "Changelog": "Versión 0.4 — Fix-Features"; "Estructura del repositorio": `tests/e2e/` y `docs/entregable-parte-1/`)

- [ ] **Step 1: Suite completa y build sin avisos**

Run: `dotnet build test.slnx -warnaserror && env DOCKER_CONTEXT=default dotnet test test.slnx`
Expected: 0 avisos, 0 errores; todos los tests en verde (anotar el total).

- [ ] **Step 2: Runtime sobre Docker Compose**

Run:

```bash
docker compose up -d --build
docker compose ps
grep -E '^POSTGRES_PORT=' .env && docker compose port postgres 5432
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:8080/account/login
cd tests/e2e && bash scripts/con-credenciales.sh npx playwright test
```

Expected: los tres servicios `running`/`healthy`; el puerto publicado de postgres coincide con `POSTGRES_PORT` de `.env`;
login `200`; E2E en verde (salvo `@api-caida`, skipped).

- [ ] **Step 3: Revisión de restricciones globales**

Run:

```bash
grep -rnE "@rendermode|@onclick|@bind" src/OpenSource1.Blazor/Components --include=*.razor || echo "sin interactividad"
git diff --stat main...HEAD -- '*appsettings*.json' | tail -n 1
git diff --stat main...HEAD -- src/OpenSource1.Infrastructure/Data/Migrations | tail -n 1
```

Expected: "sin interactividad" (el `@bind-Value` de `Login.razor`/`ChangePassword.razor`/otros formularios de cuenta
preexistentes queda fuera de esta rama si ya estaba en `main`; comprobar con `git diff main...HEAD` que la rama no añade
ninguno); ningún cambio en `appsettings*.json` ni en migraciones.

- [ ] **Step 4: Documentación**

Añadir la sección "Resultado" al spec y "Resultado de Fix-Features" al final de este plan (commits por task, total de la
suite, decisiones tomadas durante la ejecución con el formato "FA/FB/…" del plan de la Fase 8) y actualizar el `README.md`.

- [ ] **Step 5: Commit**

```bash
git add README.md docs/superpowers/specs/2026-09-28-fix-features-navegacion-y-entregable-design.md docs/superpowers/plans/2026-09-28-fix-features.md
git commit -m "docs: resultado y cierre de la rama Fix-Features"
```

---

## Decisiones tomadas al planificar (ambigüedades del spec)

| # | Ambigüedad | Decisión |
|---|---|---|
| 1 | "tests bUnit/SSR" pero el repo no tiene bUnit y la regla es no añadir paquetes | `HtmlRenderer` (componentes) + `WebApplicationFactory<BlazorApp::Program>` con clientes tipados simulados (páginas). |
| 2 | El Paso 8 cita "Inicio" como grupo, la tabla de grupos no | "Inicio" es el enlace superior del menú; no es un grupo del registro. |
| 3 | "Fechas de registro" (Fase 8) no está en la tabla de módulos | Se registra en Configuración con política `CanAdministrar`. |
| 4 | Inicio: "franja compacta de KPIs + tarjetas de grupo" | Se retiran del inicio los gráficos, la actividad reciente y las guías por rol (siguen en `/dashboard/*`). |
| 5 | `/modulos/{grupo}` de un grupo existente sin módulos visibles | 200 con "No tiene módulos disponibles en este grupo." (solo el grupo inexistente es 404). |
| 6 | "Ver todos" de la búsqueda: qué filtro usar | Número si la consulta tiene dígitos, si no nombre (facturas y borradores); clientes/productos por nombre; notas por número. |
| 7 | Test de la búsqueda de notas de crédito sin fixture posteada | Los grupos de notas se prueban por ejecución de su SQL (presentes y vacíos); el camino positivo lo cubre el E2E/runtime. |
| 8 | Setups contables comparten cargadores con su lista | Variante "mismo componente" (`/setups-contables/nuevo` y `/{id}/editar` en `SetupsContables.razor`). |
| 9 | Usuarios: "Editar → /x/{id}/editar" | Alias `/admin/users/{id}/editar` de la ficha existente (roles, estado y datos); sin "Eliminar" en la barra (se desactivan en la ficha). |
| 10 | Borradores de nota de crédito sin alta propia | B2d solo convierte la edición de cabecera; C3 añade `+ Nueva nota de crédito` → `/notas-credito-venta/nueva`. |
| 11 | "Nueva factura" aparece en B2 (alta de borradores) y en C | `/facturas-venta/nueva` nace en B2d (alta genérica) y C3 le añade la precarga por `socioId`. |
| 12 | `/cobros/nuevo` pedido en C, Cobros no está en la lista de B2 | C1 crea la página y retira del listado el formulario de registrar pago; "Crear ▾ Cobro" exige `CanModify` (regla de la API). |
| 13 | Destino tras guardar | Siempre `returnUrl` (o la lista) con `ok=`; antes Clientes/Productos iban a la ficha. |
| 14 | "Búsqueda de productos por estado" | Estado de existencia (`stockState` con/sin existencia); no hay otro filtro de estado en la API. |
| 15 | Selección en Clientes/Productos ya usaba `?focusId=` | Se unifica a `?sel=` (la ficha lateral usa la selección). |
| 16 | "Barra solo si tarda > 150 ms" y el velo de carga | Retardo 150 ms; el velo de pantalla completa solo acompaña envíos de formulario; el fundido del `ConfirmDialog` (modal) se conserva. |
| 17 | E2E "API caída" | Caso etiquetado `@api-caida`, se ejecuta aparte con `docker compose stop api`. |
| 18 | ER de dos bases (App e Identity) | Un ER "núcleo" con las tablas de la correspondencia (incluye `AspNetUsers/Roles/UserRoles`) y un ER completo de `AxionERP_App`. |
| 19 | Atajo `/` de la paleta | Se ignora cuando el foco está en un campo editable. |
| 20 | A6 en paralelo con A5 (ambos tocan el inicio) | El hero lo retira A5 (y su test lo cubre); el test de A6 excluye `Home.razor`. |

---

## Resultado de Fix-Features

Rama `Fix-Features` desde `8e81a41` (commits locales, sin merge ni push). Suite xUnit final: **1684/1684 verdes** (partida
1361), `dotnet build test.slnx -warnaserror` con **0 avisos y 0 errores**. Runtime sobre Docker Compose
(`proyecto-opensource1`, `POSTGRES_PORT=5433` de `.env`, publicado en `0.0.0.0:5433`): `postgres` healthy, `api` y
`blazor` en marcha, `/account/login` → 200. E2E Playwright (`tests/e2e`): **14 passed, 1 skipped** (`@api-caida`, se
ejecuta aparte con `docker compose stop api`; verde en D3). Restricciones: la rama no añade ningún `@bind`, `@onclick`
ni `@rendermode` (`git diff main...HEAD -G'@bind|@onclick|@rendermode' -- 'src/**/*.razor'` solo muestra comentarios
añadidos y tres `@bind-Value` retirados de `UserManagement.razor`); desde `8e81a41` no hay cambios en
`appsettings*.json` ni en migraciones (el `main...HEAD` sí los muestra porque incluye las Fases 0–8, anteriores a
esta rama).

### Commits por task

| Task | Commits |
|---|---|
| Spec y plan | `5ce0647`, `a4c6f41`, `8c36e2a` |
| D1 Documento técnico, pasos 1–5 | `20e7328` |
| D2 Diagramas y ER | `de2a987`, `77961b2` |
| A1 Registro de módulos | `6e942aa` |
| A2 Menú lateral por grupos | `32c5765`, `0eedc32` |
| A3a Búsqueda global en la API | `b0d544b` |
| A3b Barra superior y `/buscar` | `49dbb01` |
| A4 Paleta Ctrl+K | `ff53779`, `7288e08` |
| A5 Inicio y páginas de grupo | `436353c` |
| A6 Sin destello azul | `17a6eaf` |
| Puerta G-A | `2f7f7d7`, `2f7a64d` |
| B1 Componentes compartidos | `3765cbe`, `68fd97e` |
| B2a Maestros | `864600d` |
| B2b Maestros contables | `3d75c5c` |
| B2c Clientes y productos | `9d1556f` |
| B2d Lotes, cabeceras y usuarios | `ca629c1` |
| Puerta G-B | `591d151`, `d069eea`, `4247623`, `7d19223`, `b1c26b9` |
| C1 Clientes y cobros | `0edac17` |
| C2 Productos | `571a8cd` |
| C3 Facturas y notas de crédito | `999c8bd` |
| Puerta G-C | `b54b3a8`, `9200fa1` |
| D3 E2E y capturas | `f575a69`, `526eafd` |
| D4 Documento Word, presentación y guion | `4ad9f33`, `4a980f9` |
| Z Cierre | este commit (`docs: resultado y cierre de la rama Fix-Features`) |

### Decisiones tomadas durante la ejecución

Rulings del pre-flight (vinculantes para los implementadores):
- **R1:** helper `HtmlSsr` único; los tests afirman texto sobre HTML decodificado.
- **R2:** `AccionesFacturasTests` configura un almacén en `IAlmacenApiClient.ListAsync`.
- **R3:** `BlazorSsrFactory` apunta la API a una dirección muerta (`http://127.0.0.1:9/`); aplicado sobre la sección real `Api:BaseAddress`.
- **R4:** A6 en worktree aislado y `git add` solo de sus archivos.
- **R5:** un solo helper público `RaizRepositorio` en TestInfrastructure.
- **R6:** helper `PermisosPagina` (CanAdd/CanModify/CanDelete una vez por página) para barra y editores.
- **R7:** en facturas, Editar/Eliminar se anulan solo con borrador seleccionado no Abierto; sin selección, deshabilitados con `title`.
- **R8:** el código se localiza por símbolo, no por número de línea del plan.
- **R9:** se retira la aserción vacía `DoesNotContain("@bind")`; el Administrador sí ve `/admin/users`; las evidencias aseveran texto visible antes de capturar.
- **R10:** tests de selección y POST forzado solo en la entidad representativa de cada lote B2, más render + envío de las cabeceras de borradores.
- **R11:** `verificar-documento.mjs` se actualiza con los ER parciales en el mismo commit.
- **R12:** el manejador `beforeunload` también respeta el retardo de 150 ms.
- **R13:** la fila entera es seleccionable (enlace "stretched"); Usuarios con `?sel=` y Editar, sin Eliminar (se desactivan en su ficha).
- **R14:** globs entrecomillados (fish) y comprobación de interactividad limitada al diff de la rama.
- **R15:** el contrato de B1 (listado, selección/URL, editor) prevalece sobre el código de ejemplo de los briefs de B2/C.
- **R16:** los selectores E2E de menús usan `getByTestId('menu-crear'|'menu-ver').getByRole('link', …)`.
- **R17:** en el host de pruebas `Nav.NavigateTo` va después del `try/catch` (destino guardado dentro del `try`).

Decisiones del controlador (ledger):
- **FFA:** las tareas de código paralelas corren en worktrees aislados y se integran con cherry-pick en la misma rama.
- **FFB:** D1/D2 (solo `docs/` y `tests/e2e/`) corren en el árbol principal junto a un implementador de código, cada uno con `git add` de sus rutas.
- **FFC:** se acepta retirar gráficos, actividad y guías del inicio (siguen en `/dashboard/*`); el coste de carga se corrigió en la puerta G-A.
- **FFD:** B2 esperó a la revisión de B1 (cuatro lotes dependían de su contrato).
- **FFE:** tras crear o modificar una cabecera de borrador se va al borrador (sus líneas).
- **FFF:** C3 cambia "Modificar cabecera" de factura/nota a `/…/{id}/editar?returnUrl=/…/borradores/{id}`.
- **FFG:** Usuarios en tarjetas con marcador de fila y `Sel` de tipo `string`.
- **FFH:** el alta de clientes/productos exige `CanConsult` y avisa sin `CanAdd` (el POST forzado recibe el 403 de la API); vuelve a la lista con `ok=created`.
- **FFI:** los generadores del entregable (`md2docx.cjs`, `deck.cjs`, `resolver.py`, `tpl.md`) se versionan en `tests/e2e/scripts/entregable/`.

### Residuales aceptados

- Búsqueda: notas de crédito solo por número; sin `unaccent` ni `pg_trgm`; borrado lógico probado solo en socios; los
  listados interpretan `*`, `||`, `&&` como sintaxis; "Ver todos" elige el campo por la forma de `q` (casos límite:
  códigos sin dígitos, RNC corto, `FV-0001`).
- Indicadores de grupo: `WaitAsync` no cancela la llamada subyacente si el cliente ignora el token; setter público de
  `TiempoMaximoPorIndicador` solo para tests.
- Paleta: `role=presentation` en vez de `group` en el listbox; `json()` tras una redirección HTML; `VisiblesAsync` dos
  veces por render.
- Menú: `NavMenu` recalcula los grupos en lugar de reutilizar `GruposVisiblesAsync`.
- Editores B2b: no distinguen carga fallida de registro no encontrado.
- Barra de Clientes/Almacenes se parte en dos líneas a 1440 px.
- Tests de selección y POST forzado solo en la entidad representativa de cada lote (R10).
- Fase C: comentario de `PuedeAsync`; `?crearNota=true` manual en una factura ya acreditada abre el diálogo (la API lo
  rechaza); la ficha pasa su propia ruta como `RetornoUrl` y pierde el `returnUrl` previo a la lista.
- E2E: la captura 20 (búsqueda por estado) no tiene exclusión porque crear existencia es irreversible (se explica en el
  pie); las capturas 25, 26 y 28 dependen de los datos; el spec 04 usa un usuario efímero con clave fija que se borra al
  terminar.
- Documento: sin autor en el `.docx` por la decisión "sin portada" (pendiente de confirmar con el usuario).

### Datos de prueba que quedan en la base de desarrollo

- Factura `00000002`, cobro `00000002` y cliente `000005` "GC Puerta C Cliente" (creados en la puerta G-C; no se pueden
  borrar por las reglas contables).
- Filas con prefijo `E2E` creadas por la suite Playwright: la suite las elimina, pero las entidades con borrado lógico
  quedan marcadas como eliminadas en la base.

### Hallazgo de seguridad (preexistente, fuera del alcance de la rama)

La contraseña semilla (`AUTH_SEED_DEFAULT_PASSWORD`) está versionada en `README.md`, `auth.http`,
`docs/superpowers/plans/2026-09-13-fase-2-dominio-maestro.md` y cinco archivos de `tests/OpenSource1.SmokeTests`
(`AuthPermissionsApiTests.cs`, `ConfiguracionFechasRegistroApiTests.cs`, `ImagePathApiTests.cs`, `UsersApiTests.cs`,
`TestInfrastructure/PostgresTestFixture.cs`). Hay que **rotarla** en todos los entornos y **retirarla** del repositorio
(leerla de variables de entorno o `.env`, como ya hace la suite E2E). Este documento no la reproduce.
