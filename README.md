# Proyecto OpenSource1 / AxionERP

Trabajo final ISO615 UNAPEC.

Sistema Integral de Gestión Empresarial construido con **.NET 10**, **ASP.NET Core Web API**, **Blazor Web App Static SSR**, **PostgreSQL**, **ASP.NET Core Identity**, **JWT**, **Dapper**, **EF Core**, **Tailwind CSS** y arquitectura Onion.

## Estado actual

- Frontend Blazor Web App en **Static SSR**.
- API-first auth: la API emite JWT; Blazor guarda el JWT en sesión server-side y usa cookie segura HttpOnly para la sesión web.
- PostgreSQL como base de datos única para aplicación e identidad.
- Docker Compose completo con API, Blazor y PostgreSQL.
- Imágenes publicadas en Docker Hub bajo `ggeasy75/opensource`.
- CRUD operativo para:
  - Entradas.
  - Configuraciones de aplicación.
  - Gestión de usuarios y roles.
- Seguridad por roles y permisos.
- Flujos de autenticación:
  - Registro.
  - Login.
  - Logout.
  - Recuperación/restablecimiento de contraseña.
  - Cambio de contraseña autenticado.

## Arquitectura

La solución sigue Onion Architecture:

| Proyecto | Responsabilidad |
| --- | --- |
| `OpenSource1.Core` | Entidades de dominio, aggregate roots y abstracciones base. |
| `OpenSource1.Application` | Casos de uso, CQRS, DTOs, contratos, seguridad y servicios de aplicación. |
| `OpenSource1.Infrastructure` | EF Core, Identity, Dapper, repositorios, Unit of Work e implementaciones externas. |
| `OpenSource1.Api` | Endpoints HTTP, autenticación JWT y autorización por políticas. |
| `OpenSource1.Blazor` | Cliente Blazor Static SSR, UI Tailwind y consumo de API por `HttpClient` tipado. |

La capa de datos usa:

- EF Core para persistencia transaccional y comandos.
- Dapper para consultas optimizadas.
- ASP.NET Core Identity para usuarios, roles, lockout y tokens de contraseña.
- `IUnitOfWork` y repositorios genéricos para agregados.

## Tecnologías principales

- .NET `net10.0`
- ASP.NET Core Web API
- Blazor Web App Static SSR
- PostgreSQL `17-alpine`
- ASP.NET Core Identity
- JWT Bearer
- EF Core
- Dapper
- MediatR
- Tailwind CSS Play CDN
- Docker / Docker Compose

> Nota: Tailwind está configurado mediante Play CDN para desarrollo/demostración. Para producción real conviene compilar Tailwind en build time.

## Estructura del repositorio

```txt
.
├── src/
│   ├── OpenSource1.Core/
│   ├── OpenSource1.Application/
│   ├── OpenSource1.Infrastructure/
│   ├── OpenSource1.Api/
│   └── OpenSource1.Blazor/
├── Dockerfile.api
├── Dockerfile.blazor
├── docker-compose.yml
├── init-postgres.sh
├── rebuild.sh
├── Makefile
├── auth.http
├── .env.example
└── test.slnx
```

## Configuración local

Cree un archivo `.env` desde la plantilla:

```bash
cp .env.example .env
```

Variables requeridas:

```env
POSTGRES_PASSWORD=Change_this_postgres_password_12345
JWT_SIGNING_KEY=Change_this_local_jwt_signing_key_1234567890
AUTH_SEED_DEFAULT_PASSWORD=Change_this_seed_password_12345
```

Para usar los usuarios seed documentados abajo, puede usar:

```env
AUTH_SEED_DEFAULT_PASSWORD=Password123
```

No suba el `.env` real al repositorio.

## Ejecutar con Docker Compose

El stack Compose se llama:

```txt
proyecto-opensource1
```

Servicios:

| Servicio | Imagen | Contenedor | URL / Puerto |
| --- | --- | --- | --- |
| Blazor | `ggeasy75/opensource:blazor` | `Proyecto-OpenSource1-blazor` | <http://localhost:8080> |
| API | `ggeasy75/opensource:api` | `Proyecto-OpenSource1-api` | <http://localhost:8081> |
| PostgreSQL | `postgres:17-alpine` | `Proyecto-OpenSource1-postgres` | `localhost:5432` |

Levantar el stack con imágenes existentes o descargadas:

```bash
docker compose up -d
```

Construir desde el código local y levantar:

```bash
docker compose build api blazor
docker compose up -d
```

Rebuild limpio sin cache:

```bash
./rebuild.sh
```

Rebuild usando cache:

```bash
./rebuild.sh --soft
```

Detener contenedores preservando datos:

```bash
docker compose down --remove-orphans
```

Eliminar también volúmenes de datos:

```bash
docker compose down -v
```

## Comandos Makefile

```bash
make help
```

Comandos disponibles:

| Comando | Descripción |
| --- | --- |
| `make rebuild` | Rebuild limpio sin cache y levanta el stack. |
| `make soft` | Rebuild con cache y levanta el stack. |
| `make up` | Levanta el stack sin rebuild. |
| `make down` | Detiene contenedores y preserva volúmenes. |
| `make clean` | Detiene contenedores y elimina imágenes locales del proyecto. |
| `make push` | Sube imágenes API y Blazor a Docker Hub. |
| `make logs` | Logs de todos los servicios. |
| `make logs-api` | Logs del API. |
| `make logs-blazor` | Logs de Blazor. |
| `make ps` | Estado de contenedores. |

## Docker Hub

Repositorio Docker Hub:

```txt
ggeasy75/opensource
```

Tags publicados:

```bash
docker pull ggeasy75/opensource:api
docker pull ggeasy75/opensource:blazor
```

Para construir y subir nuevas versiones:

```bash
docker compose build api blazor
docker compose push api blazor
```

Equivalente manual:

```bash
docker push ggeasy75/opensource:api
docker push ggeasy75/opensource:blazor
```

> Nota: Docker Hub muestra el patrón `docker push ggeasy75/opensource:tagname`; en este proyecto usamos `api` y `blazor` como tags dentro del mismo repositorio.

## Ejecutar sin Docker

Requiere PostgreSQL local y connection strings configurados por variables de entorno, user-secrets o `appsettings.Development.json`.

Restaurar y compilar:

```bash
dotnet restore test.slnx
dotnet build test.slnx
```

Ejecutar API:

```bash
dotnet run --project src/OpenSource1.Api/OpenSource1.Api.csproj
```

Ejecutar Blazor:

```bash
dotnet run --project src/OpenSource1.Blazor/OpenSource1.Blazor.csproj
```

URLs por defecto de los perfiles de desarrollo:

- Blazor: <http://localhost:5171>
- API: revisar `src/OpenSource1.Api/Properties/launchSettings.json`

## Base de datos

El contenedor PostgreSQL crea dos bases de datos mediante `init-postgres.sh`:

- `AxionERP_App`
- `AxionERP_Identity`

El API aplica migraciones automáticamente en Docker porque `Database__ApplyMigrationsOnStartup=true` está configurado en `docker-compose.yml`.

Volúmenes Docker:

| Volumen | Uso |
| --- | --- |
| `postgres-data` | Datos persistentes de PostgreSQL. |
| `dataprotection-keys` | Llaves Data Protection compartidas entre API y Blazor. |

## Usuarios seed

Cuando `UserSeed__Enabled=true` y `AUTH_SEED_DEFAULT_PASSWORD` está configurado, se crean usuarios iniciales.

Contraseña usada en desarrollo:

```txt
Password123
```

Usuarios principales:

| Usuario | Rol |
| --- | --- |
| `admin` | `Administrador` |
| `supervisor` | `Supervisor` |
| `ejecutor` | `Ejecutor` |

Usuarios adicionales de demostración:

| Usuario | Estado |
| --- | --- |
| `cmendes` | Activo |
| `agarcia` | Activo |
| `ltorres` | Activo |
| `mrodriguez` | Activo |
| `jperez` | Activo |
| `svargas` | Inactiva |

## Roles y permisos

| Rol | Consultar | Agregar | Modificar | Eliminar |
| --- | --- | --- | --- | --- |
| `Administrador` | Sí | Sí | Sí | Sí |
| `Supervisor` | Sí | No | Sí | No |
| `Ejecutor` | Sí | Sí | No | No |

Políticas internas:

- `CanConsult`
- `CanAdd`
- `CanModify`
- `CanDelete`

La UI oculta acciones según permisos, pero la autorización real se aplica en la API con JWT y políticas.

## Autenticación y seguridad

Flujo actual:

1. Blazor envía credenciales a `POST /api/auth/login`.
2. La API valida con ASP.NET Core Identity.
3. La API devuelve un JWT con roles y permisos.
4. Blazor crea una cookie HttpOnly para la sesión web.
5. Blazor guarda el JWT en sesión server-side.
6. `BearerTokenHandler` adjunta el JWT a las llamadas API.

El JWT no se guarda en `localStorage`, `sessionStorage` ni cookies accesibles por JavaScript.

Endpoints de auth:

| Endpoint | Auth | Descripción |
| --- | --- | --- |
| `POST /api/auth/register` | Anónimo | Registro local. |
| `POST /api/auth/login` | Anónimo | Login y emisión de JWT. |
| `GET /api/auth/me` | JWT | Usuario actual, roles y permisos. |
| `POST /api/auth/forgot-password` | Anónimo | Genera token de recuperación. |
| `POST /api/auth/reset-password` | Anónimo | Restablece contraseña con token. |
| `POST /api/auth/change-password` | JWT | Cambia contraseña autenticada. |

> Nota de recuperación de contraseña: actualmente el token se muestra en pantalla para demostración/local. En producción debe enviarse por correo y no exponerse visualmente.

## Blazor UI

El frontend usa Blazor Static SSR sin runtime interactivo.

Reglas importantes del proyecto:

- No agregar `@rendermode` salvo que se migre explícitamente a interactividad.
- No usar `@onclick` ni handlers interactivos de Blazor.
- Formularios con `EditForm`, `FormName` y `[SupplyParameterFromForm]`.
- Los nombres de inputs SSR deben respetar el prefijo del modelo de formulario: por ejemplo `Input.Password`, `CreateInput.Titulo`, `UpdateInput.Estado`.
- No duplicar `<AntiforgeryToken />` dentro de `EditForm`; Blazor lo genera automáticamente.

Características UI actuales:

- Tailwind CSS.
- Heroicons SVG inline.
- Mensajes amigables en español.
- Login, registro, logout, recuperación y cambio de contraseña.
- Menú con opciones según rol.
- CRUD de entradas con confirmación al editar y eliminar.
- CRUD de configuraciones.
- Gestión de usuarios, roles y estado activo/inactivo.

## API y documentación interactiva

Con Docker Compose:

- API base: <http://localhost:8081>
- Scalar/OpenAPI: <http://localhost:8081/scalar/v1>

Ejemplos HTTP están en:

```txt
auth.http
```

## Pruebas de salud rápida

Levantar stack:

```bash
docker compose up -d
```

Ver contenedores:

```bash
docker compose ps
```

Login API con usuario seed:

```bash
curl -X POST http://localhost:8081/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"userNameOrEmail":"admin","password":"Password123"}'
```

Abrir Blazor:

```txt
http://localhost:8080
```

## Git Flow

- `main`: rama estable.
- `qa`/`QA`: validación previa a release.
- `deploy`: rama lista para despliegue.

## CI workflows and merge prevention

Este repositorio incluye workflows de validación y promoción:

- `.github/workflows/pr-to-qa.yml`: ejecuta restore/build/test para PRs hacia `qa`/`QA`.
- `.github/workflows/pr-to-deploy.yml`: bloquea PRs hacia `deploy` si la rama origen no es `qa`/`QA`, luego ejecuta restore/build/test.
- `.github/workflows/pr-to-main.yml`: bloquea PRs hacia `main` si la rama origen no es `deploy`, luego ejecuta restore/build/test.
- `.github/workflows/auto-promote-deploy-to-main.yml`: al hacer push a `deploy`, crea/reusa PR `deploy -> main` y habilita auto-merge.

La suite mínima de smoke tests vive en:

```txt
tests/OpenSource1.SmokeTests
```

Para completar la prevención de merges, configure branch protection según:

```txt
.github/branch-protection.md
```

## Notas para agentes / mantenimiento

- Target framework: `net10.0`.
- Solución: `test.slnx`.
- Proyecto Blazor: `src/OpenSource1.Blazor/OpenSource1.Blazor.csproj`.
- Proyecto API: `src/OpenSource1.Api/OpenSource1.Api.csproj`.
- No hay `Directory.Packages.props`; los paquetes se gestionan con `PackageReference` directo en cada proyecto.
- Nullable reference types e implicit usings están habilitados.
- Mantener el cliente Blazor desacoplado de Infrastructure; consumir datos mediante typed API clients.
