# 🐳 Guía de despliegue local con Docker

Instrucciones para levantar el stack completo de **OpenSource1** en tu máquina usando Docker Compose.

---

## Prerequisitos

| Herramienta | Versión mínima | Instalación |
|-------------|---------------|-------------|
| **Docker Desktop** (Windows/macOS) o **Docker Engine** (Linux) | 24.x | https://docs.docker.com/get-docker/ |
| **Docker Compose** | v2.x (incluido en Docker Desktop) | https://docs.docker.com/compose/install/ |
| **Git** | cualquiera | https://git-scm.com/ |

> **Nota:** No necesitas instalar .NET SDK, SQL Server ni ningún otro runtime; todo corre dentro de contenedores.

---

## Estructura del stack

```
┌─────────────────────────────────────────────┐
│  Tu navegador  →  http://localhost:8080      │  ← Blazor Web App
│  Tu cliente    →  http://localhost:8081      │  ← REST API (JWT)
│  SQL Client    →  localhost:1433             │  ← SQL Server
└─────────────────────────────────────────────┘
```

| Servicio     | Puerto local | Imagen                  | Descripción             |
|--------------|-------------|-------------------------|-------------------------|
| `blazor`     | `8080`      | `opensource1-blazor:local` | Blazor Web App (UI)  |
| `api`        | `8081`      | `opensource1-api:local`    | ASP.NET Core Web API |
| `sqlserver`  | `1433`      | `mcr.microsoft.com/mssql/server:2022-latest` | Base de datos |

---

## Paso 1 – Clonar el repositorio

```bash
git clone https://github.com/RPCONSULTANT/OpenSource1.git
cd OpenSource1
```

---

## Paso 2 – Crear el archivo `.env`

Copia el archivo de ejemplo y completa las variables:

```bash
cp .env.example .env
```

Abre `.env` y establece tus contraseñas:

```env
# Contraseña del SA de SQL Server (mín. 8 chars, mayúscula, número y símbolo)
MSSQL_SA_PASSWORD=Tu_Contrasena_Segura123

# Contraseña que se usará al crear los usuarios semilla (admin/supervisor/ejecutor)
AUTH_SEED_DEFAULT_PASSWORD=OtraContrasena_Segura456
```

> ⚠️ **Nunca** subas tu `.env` real al repositorio. Está en `.gitignore`.

---

## Paso 3 – Construir las imágenes

```bash
docker compose build
```

Esto compila los proyectos .NET dentro de contenedores multistage.  
La primera vez puede tardar **2–5 minutos** mientras descarga las capas base.

---

## Paso 4 – Levantar el stack

```bash
docker compose up -d
```

El flag `-d` ejecuta los contenedores en segundo plano (detached).

Verificar que los tres servicios estén corriendo:

```bash
docker compose ps
```

Deberías ver algo como:

```
NAME                  STATUS          PORTS
test-blazor-1         running         0.0.0.0:8080->8080/tcp
test-api-1            running         0.0.0.0:8081->8081/tcp
test-sqlserver-1      running         0.0.0.0:1433->1433/tcp
```

---

## Paso 5 – Acceder a la aplicación

| URL | Descripción |
|-----|-------------|
| http://localhost:8080 | **Blazor Web App** (interfaz de usuario) |
| http://localhost:8081/swagger | **Swagger UI** del API |
| http://localhost:8081/health | Health check del API |

### Usuarios semilla disponibles

| Usuario       | Contraseña                    | Rol            | Permisos                        |
|---------------|-------------------------------|----------------|---------------------------------|
| `admin`       | valor de `AUTH_SEED_DEFAULT_PASSWORD` | Administrador  | Consultar, Agregar, Modificar, Eliminar |
| `supervisor`  | valor de `AUTH_SEED_DEFAULT_PASSWORD` | Supervisor     | Consultar, Modificar            |
| `ejecutor`    | valor de `AUTH_SEED_DEFAULT_PASSWORD` | Ejecutor       | Consultar, Agregar              |

---

## Comandos útiles

### Ver logs en tiempo real

```bash
# todos los servicios
docker compose logs -f

# solo el Blazor
docker compose logs -f blazor

# solo el API
docker compose logs -f api
```

### Reiniciar un servicio individual

```bash
docker compose restart blazor
docker compose restart api
```

### Reconstruir e reiniciar después de cambios en el código

```bash
docker compose build blazor && docker compose up -d blazor
```

### Detener el stack (conserva los datos)

```bash
docker compose stop
```

### Detener y eliminar contenedores (conserva volúmenes/datos)

```bash
docker compose down
```

### Eliminar todo incluyendo la base de datos (¡datos borrados!)

```bash
docker compose down -v
```

---

## Solución de problemas

### Puerto ocupado

Si el puerto `8080` o `8081` ya está en uso:

```bash
# Linux/macOS
sudo lsof -i :8080

# Windows PowerShell
netstat -ano | findstr :8080
```

Cambia el puerto en `docker-compose.yml`:

```yaml
ports:
  - "9090:8080"   # usa 9090 en tu host en lugar de 8080
```

### El API tarda en arrancar

SQL Server puede necesitar ~20 segundos en inicializarse. El API tiene un `healthcheck` y reintentos configurados; espera hasta que `docker compose ps` muestre `healthy` en el servicio `sqlserver`.

### Error de permisos en Linux

```bash
sudo usermod -aG docker $USER
newgrp docker
```

### Ver variables de entorno activas en un contenedor

```bash
docker compose exec api env | grep -v PASSWORD
```

---

## Arquitectura del proyecto

```
OpenSource1/
├── src/
│   ├── OpenSource1.Core/           # Entidades y contratos de dominio
│   ├── OpenSource1.Application/    # CQRS, MediatR, servicios de aplicación
│   ├── OpenSource1.Infrastructure/ # EF Core, Dapper, Identity, JWT
│   ├── OpenSource1.Api/            # ASP.NET Core Web API (controladores)
│   └── OpenSource1.Blazor/         # Blazor Web App (Static SSR, Tailwind CSS)
├── docker-compose.yml
├── Dockerfile.api
├── Dockerfile.blazor
├── .env.example
└── test.slnx
```

---

*Documentación generada para OpenSource1 · Blazor Web App + API JWT + SQL Server · 2026*
