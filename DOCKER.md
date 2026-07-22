# Guía de despliegue local con Docker

Instrucciones para levantar **OpenSource1 / AxionERP** con Docker Compose.

## Prerrequisitos

| Herramienta | Versión mínima |
| --- | --- |
| Docker Desktop o Docker Engine | 24.x |
| Docker Compose | v2.x |
| Git | cualquiera |

No necesitas instalar .NET SDK ni PostgreSQL local para esta ruta; todo corre dentro de contenedores.

## Stack

```txt
Navegador  -> http://localhost:8080  -> Blazor Web App
Cliente API -> http://localhost:8081 -> ASP.NET Core REST API
DB Client  -> localhost:5432         -> PostgreSQL
```

| Servicio | Puerto local | Imagen | Descripción |
| --- | --- | --- | --- |
| `blazor` | `8080` | `ggeasy75/opensource:blazor` | UI Blazor Static SSR |
| `api` | `8081` | `ggeasy75/opensource:api` | API ASP.NET Core con JWT |
| `postgres` | `5432` | `postgres:17-alpine` | Base de datos PostgreSQL |

## Crear `.env`

```bash
cp .env.example .env
```

Variables requeridas:

```env
POSTGRES_PASSWORD=Change_this_postgres_password_12345
JWT_SIGNING_KEY=Change_this_local_jwt_signing_key_1234567890
AUTH_SEED_DEFAULT_PASSWORD=Change_this_seed_password_12345
```

Nunca subas tu `.env` real al repositorio.

## Levantar el sistema

```bash
docker compose build
docker compose up -d
docker compose ps
```

El contenedor `postgres` crea las bases mediante `init-postgres.sh`:

- `AxionERP_App`
- `AxionERP_Identity`

La API aplica migraciones al arrancar en Docker con `Database__ApplyMigrationsOnStartup=true`.

## URLs

| URL | Uso |
| --- | --- |
| http://localhost:8080 | Blazor Web App |
| http://localhost:8081/scalar/v1 | Scalar API Explorer |
| http://localhost:8081/openapi/v1.json | OpenAPI JSON |
| http://localhost:8081/api/auth/login | Login API, método POST |

## Usuarios semilla

| Usuario | Contraseña | Rol |
| --- | --- | --- |
| `admin` | valor de `AUTH_SEED_DEFAULT_PASSWORD` | Administrador |
| `supervisor` | valor de `AUTH_SEED_DEFAULT_PASSWORD` | Supervisor |
| `ejecutor` | valor de `AUTH_SEED_DEFAULT_PASSWORD` | Ejecutor |

## Comandos útiles

```bash
docker compose logs -f
docker compose logs -f api
docker compose logs -f blazor
docker compose restart api
docker compose restart blazor
docker compose down --remove-orphans
docker compose down -v
```

## Solución de problemas

### Puerto ocupado

Si `8080`, `8081` o `5432` están ocupados, cambia el puerto izquierdo en `docker-compose.yml`.

```yaml
ports:
  - "9090:8080"
```

### PostgreSQL tarda en estar listo

El servicio `postgres` usa `pg_isready` como healthcheck. Espera hasta que `docker compose ps` muestre el contenedor saludable antes de probar la API.

### Ver configuración activa sin exponer secretos

```bash
docker compose exec api env | grep -v PASSWORD | grep -v KEY
```

## Arquitectura del proyecto

```txt
OpenSource1/
├── src/OpenSource1.Core/
├── src/OpenSource1.Application/
├── src/OpenSource1.Infrastructure/
├── src/OpenSource1.Api/
├── src/OpenSource1.Blazor/
├── docker-compose.yml
├── Dockerfile.api
├── Dockerfile.blazor
├── init-postgres.sh
└── test.slnx
```

Documentación para OpenSource1 · Blazor Static SSR + API JWT + PostgreSQL · 2026
