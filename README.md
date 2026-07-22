# AxionERP / OpenSource1

Proyecto final para la materia **DESARROLLO DE SOFTWARE CON TECNOLOGÍAS PROPIETARIAS Y OPEN SOURCE I / ISO-615**.

**Nombre del proyecto:** Sistema de Gestión Empresarial con Control de Acceso por Roles.

AxionERP es una aplicación cliente-servidor construida con **.NET 10**, **ASP.NET Core Web API**, **Blazor Web App Static SSR**, **PostgreSQL**, **ASP.NET Core Identity**, **JWT**, **Entity Framework Core**, **Dapper**, **Tailwind CSS** y arquitectura Onion.

## Resumen del proyecto

El sistema implementa una base funcional para gestión empresarial con:

- Autenticación de usuarios.
- Roles y permisos.
- CRUDs conectados a base de datos.
- Buscadores y consultas.
- Validaciones.
- Navegación por módulos.
- Seguridad básica.
- Interfaz web amigable.
- Integración entre frontend, API y base de datos.

## Tecnologías usadas

| Área | Tecnología |
| --- | --- |
| Lenguaje | C# |
| Backend | ASP.NET Core Web API |
| Frontend | Blazor Web App Static SSR |
| Base de datos | PostgreSQL |
| ORM / datos | Entity Framework Core y Dapper |
| Seguridad | ASP.NET Core Identity, JWT, cookies HttpOnly |
| Contenedores | Docker y Docker Compose |
| Estilos | Tailwind CSS |
| Control de versiones | Git |

## Arquitectura

La solución está organizada con arquitectura Onion:

| Proyecto | Responsabilidad |
| --- | --- |
| `OpenSource1.Core` | Entidades de dominio y abstracciones base. |
| `OpenSource1.Application` | Casos de uso, DTOs, CQRS, contratos y reglas de aplicación. |
| `OpenSource1.Infrastructure` | EF Core, Identity, Dapper, repositorios y Unit of Work. |
| `OpenSource1.Api` | Endpoints HTTP, autenticación JWT y autorización por políticas. |
| `OpenSource1.Blazor` | Interfaz web Blazor, formularios, menú y consumo de API. |

## Estado actual

Actualmente el proyecto cuenta con:

- Frontend Blazor en **Static SSR**.
- API REST con autenticación y autorización.
- Base de datos PostgreSQL para aplicación e identidad.
- Docker Compose con API, Blazor y PostgreSQL.
- Imágenes publicadas en Docker Hub: `ggeasy75/opensource`.
- CRUD de entradas.
- CRUD de configuraciones de aplicación.
- Gestión de usuarios, roles y estado activo/inactivo.
- Gestión de clientes con buscadores, filtros, reportes y Excel.
- Gestión de productos con buscadores, filtros, dashboards, reportes y Excel.
- Registro, login, logout, recuperación y cambio de contraseña.
- Menú principal con opciones según rol.
- Dashboard principal y dashboards por módulo.
- Bitácora operativa.
- Reportería histórica y exportación a Excel.
- Validaciones y mensajes en español.

## Entregable 3

La **Etapa III — Integración Total del Sistema** se encuentra documentada en:

- [`docs/ENTREGABLE-3.md`](docs/ENTREGABLE-3.md)

Carpeta sugerida para capturas del documento:

- [`docs/screenshots/entregable-3/`](docs/screenshots/entregable-3/)

### Resumen de cumplimiento

| Requerimiento Etapa III | Estado |
| --- | --- |
| Menú principal | Implementado |
| Login integrado | Implementado |
| Detección de roles | Implementado |
| Permisos por rol | Implementado |
| CRUD de usuarios | Implementado |
| CRUD de clientes | Implementado |
| CRUD de productos | Implementado |
| Consultas y filtros | Implementado |
| Reportes PDF | Implementado |
| Exportación a Excel | Implementado |
| Dashboard | Implementado |
| Bitácora | Implementado |
| Validaciones | Implementado |
| Integración BD | Implementado |

### Matriz principal de seguridad

| Módulo / Acción | Admin | Supervisor | Ejecutor |
| --- | --- | --- | --- |
| Usuarios | Sí | No | No |
| Clientes | Sí | Sí | Sí |
| Productos | Sí | Sí | Sí |
| Eliminar | Sí | No | No |

Políticas efectivas del sistema:

- `CanConsult`: Administrador, Supervisor, Ejecutor
- `CanAdd`: Administrador, Ejecutor
- `CanModify`: Administrador, Supervisor
- `CanDelete`: Administrador

## Cumplimiento del entregable 1

La **Etapa I — Sistema de Login y Roles** está cubierta con los siguientes puntos:

| Requerimiento | Estado |
| --- | --- |
| Pantalla de login | Implementada en Blazor. |
| Usuario y contraseña | Implementados. |
| Botón iniciar sesión | Implementado. |
| Validación contra base de datos | Implementada con ASP.NET Core Identity. |
| Mensajes de error | Implementados. |
| Bloqueo de acceso incorrecto | Implementado mediante validación y control de autenticación. |
| Contraseñas ocultas | Implementado en el formulario de login. |
| Roles requeridos | `Administrador`, `Supervisor`, `Ejecutor`. |
| Control de acceso por rol | Implementado en UI y API. |
| Opciones según rol | Implementado en el menú y acciones disponibles. |
| Permisos insuficientes | Controlados por políticas de autorización. |
| Menú principal | Implementado. |
| Formularios | Implementados en Blazor. |
| Base de datos obligatoria | Implementada con PostgreSQL. |
| Código fuente | Incluido en el repositorio. |
| Script / inicialización de BD | Incluido mediante migraciones e inicialización Docker. |

## Roles y permisos

| Rol | Consultar | Agregar | Modificar | Eliminar |
| --- | --- | --- | --- | --- |
| `Administrador` | Sí | Sí | Sí | Sí |
| `Supervisor` | Sí | No | Sí | No |
| `Ejecutor` | Sí | Sí | No | No |

Políticas internas usadas por la API:

- `CanConsult`
- `CanAdd`
- `CanModify`
- `CanDelete`

La interfaz oculta acciones según el rol, pero la seguridad principal se valida en la API con JWT y políticas de autorización.

## Usuarios iniciales

Cuando la carga inicial de usuarios está habilitada, se crean estos usuarios:

| Usuario | Rol |
| --- | --- |
| `admin` | `Administrador` |
| `supervisor` | `Supervisor` |
| `ejecutor` | `Ejecutor` |

Contraseña de desarrollo usada para demostración:

```txt
Password123
```

También existen usuarios de demostración adicionales para pruebas del módulo de usuarios.

## Autenticación y seguridad

Flujo actual:

1. Blazor envía las credenciales a `POST /api/auth/login`.
2. La API valida el usuario con ASP.NET Core Identity.
3. La API devuelve un JWT con roles y permisos.
4. Blazor crea una sesión web con cookie segura HttpOnly.
5. El JWT se guarda del lado servidor para llamar a la API.

El JWT no se guarda en `localStorage`, `sessionStorage` ni en cookies accesibles desde JavaScript.

Endpoints principales:

| Endpoint | Descripción |
| --- | --- |
| `POST /api/auth/register` | Registro de usuario. |
| `POST /api/auth/login` | Inicio de sesión y emisión de JWT. |
| `GET /api/auth/me` | Usuario actual, roles y permisos. |
| `POST /api/auth/forgot-password` | Solicitud de recuperación de contraseña. |
| `POST /api/auth/reset-password` | Restablecimiento de contraseña. |
| `POST /api/auth/change-password` | Cambio de contraseña autenticado. |

> Nota: el token de recuperación no debe mostrarse en pantalla. En producción debe enviarse por correo o por otro canal seguro. En desarrollo, use herramientas internas o logs controlados si necesita probar el flujo.

## Estructura del repositorio

```txt
.
├── src/
│   ├── OpenSource1.Core/
│   ├── OpenSource1.Application/
│   ├── OpenSource1.Infrastructure/
│   ├── OpenSource1.Api/
│   └── OpenSource1.Blazor/
├── tests/OpenSource1.SmokeTests/
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

Crear el archivo `.env` desde la plantilla:

```bash
cp .env.example .env
```

Variables requeridas:

```env
POSTGRES_PASSWORD=Change_this_postgres_password_12345
JWT_SIGNING_KEY=Change_this_local_jwt_signing_key_1234567890
AUTH_SEED_DEFAULT_PASSWORD=Change_this_seed_password_12345
```

Para usar los usuarios iniciales documentados:

```env
AUTH_SEED_DEFAULT_PASSWORD=Password123
```

No subir el archivo `.env` real al repositorio.

## Ejecución con Docker Compose

Levantar el sistema:

```bash
docker compose up -d
```

Servicios disponibles:

| Servicio | URL / Puerto |
| --- | --- |
| Blazor | <http://localhost:8080> |
| API | <http://localhost:8081> |
| PostgreSQL | `localhost:5432` |

Reconstruir desde el código local:

```bash
docker compose build api blazor
docker compose up -d
```

Rebuild limpio:

```bash
./rebuild.sh
```

Detener contenedores:

```bash
docker compose down --remove-orphans
```

Eliminar también datos persistentes:

```bash
docker compose down -v
```

## Ejecución sin Docker

Requiere PostgreSQL local y cadenas de conexión configuradas por variables de entorno, user-secrets o `appsettings.Development.json`.

Ejemplo de variables de entorno para PostgreSQL local:

```bash
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=AxionERP_App;Username=Rainiery;Password=<tu-password>"
export ConnectionStrings__IdentityConnection="Host=localhost;Port=5432;Database=AxionERP_Identity;Username=Rainiery;Password=<tu-password>"
```

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

## Base de datos

El contenedor PostgreSQL crea dos bases de datos:

- `AxionERP_App`
- `AxionERP_Identity`

La API puede aplicar migraciones automáticamente en Docker mediante `Database__ApplyMigrationsOnStartup=true`.

Volúmenes usados:

| Volumen | Uso |
| --- | --- |
| `postgres-data` | Datos persistentes de PostgreSQL. |
| `dataprotection-keys` | Llaves Data Protection compartidas entre API y Blazor. |

## Pruebas rápidas

Ver contenedores:

```bash
docker compose ps
```

Probar login desde la API:

```bash
curl -X POST http://localhost:8081/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"userNameOrEmail":"admin","password":"Password123"}'
```

Abrir la interfaz:

```txt
http://localhost:8080
```

Documentación interactiva de la API:

```txt
http://localhost:8081/scalar/v1
```

## Docker Hub

Repositorio:

```txt
ggeasy75/opensource
```

Imágenes principales:

```bash
docker pull ggeasy75/opensource:api
docker pull ggeasy75/opensource:blazor
```

## Git Flow y validación

Ramas principales:

- `main`: rama estable.
- `qa` / `QA`: validación previa a release.
- `deploy`: rama lista para despliegue.

El repositorio incluye workflows para validar PRs hacia `qa`, `deploy` y `main`, además de promoción automática de `deploy` hacia `main`.

Las pruebas smoke se encuentran en:

```txt
tests/OpenSource1.SmokeTests
```

## Changelog

### Versión 0.3 — Entregable 3

Integración final de AxionERP como sistema empresarial completo, con módulos operativos, seguridad por roles y evidencias automatizadas de funcionamiento.

Incluye:

- Integración completa de login, menú principal, frontend, API y PostgreSQL.
- CRUD de usuarios exclusivo para el rol `Administrador`.
- CRUD de clientes y productos conectado a base de datos.
- Búsquedas y filtros dinámicos por múltiples campos.
- Validaciones de formularios y reglas de negocio en frontend y backend.
- Políticas `CanConsult`, `CanAdd`, `CanModify` y `CanDelete`.
- Acceso de consulta para `Administrador`, `Supervisor` y `Ejecutor`.
- Alta de registros para `Administrador` y `Ejecutor`.
- Modificación de registros para `Administrador` y `Supervisor`.
- Eliminación restringida exclusivamente al `Administrador`.
- Protección del módulo de usuarios para impedir acceso de `Supervisor` y `Ejecutor`.
- Navegación y acciones visibles dinámicamente según rol.
- Bloqueo efectivo de rutas y endpoints no autorizados.
- Dashboard general y dashboards de clientes y productos.
- Reportes PDF y exportaciones a Excel.
- Bitácora operativa para Administrador y Supervisor.
- Modo oscuro y diseño responsivo.
- Documento académico final en [`docs/ENTREGABLE-3.md`](docs/ENTREGABLE-3.md).
- 47 capturas E2E con evidencia de módulos, formularios, confirmaciones y permisos.
- Pruebas específicas para demostrar restricciones de Supervisor y Ejecutor tanto en UI como en API.

#### Matriz de autorización validada

| Acción | Administrador | Supervisor | Ejecutor |
| --- | --- | --- | --- |
| Consultar clientes y productos | Sí | Sí | Sí |
| Agregar clientes y productos | Sí | No | Sí |
| Modificar clientes y productos | Sí | Sí | No |
| Eliminar clientes y productos | Sí | No | No |
| Gestionar usuarios | Sí | No | No |
| Consultar bitácora | Sí | Sí | No |

### Versión 0.1 — Entregable 1

Primera versión del sistema, enfocada en la **Etapa I: Sistema de Login y Roles**.

Incluye:

- Proyecto cliente-servidor en .NET.
- Backend ASP.NET Core Web API.
- Frontend Blazor Web App Static SSR.
- Base de datos PostgreSQL.
- Arquitectura organizada por capas usando Onion Architecture.
- Autenticación de usuarios con ASP.NET Core Identity.
- Inicio de sesión con usuario y contraseña.
- Contraseñas ocultas en el formulario.
- Validación de credenciales desde base de datos.
- Control de acceso incorrecto y mensajes de error.
- Roles iniciales: `Administrador`, `Supervisor` y `Ejecutor`.
- Usuarios iniciales: `admin`, `supervisor` y `ejecutor`.
- Permisos por rol para consultar, agregar, modificar y eliminar.
- Menú principal con opciones según permisos.
- Bloqueo de acciones no autorizadas desde UI y API.
- CRUD funcional de entradas.
- CRUD funcional de configuraciones.
- Gestión de usuarios y roles.
- Consultas y buscadores básicos.
- Validaciones en formularios.
- Navegación entre módulos.
- Docker Compose para ejecutar API, Blazor y PostgreSQL.
- Scripts y configuración para inicialización de base de datos.
- Workflows de validación y pruebas smoke.

## Notas de mantenimiento

- Target framework: `net10.0`.
- Solución: `test.slnx`.
- Proyecto Blazor: `src/OpenSource1.Blazor/OpenSource1.Blazor.csproj`.
- Proyecto API: `src/OpenSource1.Api/OpenSource1.Api.csproj`.
- Los paquetes se gestionan con `PackageReference` directo en cada proyecto.
- Nullable reference types e implicit usings están habilitados.
- El frontend Blazor usa Static SSR; no agregar `@rendermode` ni handlers interactivos salvo que se migre explícitamente a modo interactivo.
