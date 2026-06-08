# OpenSource1

Trabajo Final ISO615 UNAPEC.

.NET 10 solution with Onion Architecture, ASP.NET Core API, and a Blazor Web App client.

## Run Locally

```bash
dotnet restore
dotnet build
dotnet run
```


## Run with Docker Compose

The project includes multi-stage Dockerfiles and a `docker-compose.yml` that runs:

- Blazor Web App client on `http://localhost:8080`
- ASP.NET Core API on `http://localhost:8081`
- SQL Server 2022 Developer on `localhost:1433`

Create a local `.env` file and start the full stack:

```bash
cp .env.example .env
docker compose up --build -d
```

The app containers use the SQL Server service name `sqlserver` internally. The API container applies EF Core migrations automatically when `Database__ApplyMigrationsOnStartup=true` is set in `docker-compose.yml`.

Stop the stack:

```bash
docker compose down
```

Remove SQL Server data too:

```bash
docker compose down -v
```

## Local SQL Server

Create a local `.env` file from the example and set a strong SA password:

```bash
cp .env.example .env
docker compose up -d
```

Store the application connection strings with user secrets instead of committing passwords:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=TestAppDb;User Id=sa;Password=<your-password>;TrustServerCertificate=True;MultipleActiveResultSets=True"
dotnet user-secrets set "ConnectionStrings:IdentityConnection" "Server=localhost,1433;Database=TestIdentityDb;User Id=sa;Password=<your-password>;TrustServerCertificate=True;MultipleActiveResultSets=True"
```

Apply the EF Core migrations:

```bash
dotnet tool restore
dotnet dotnet-ef database update --context ApplicationDbContext
dotnet dotnet-ef database update --context AppIdentityDbContext
```

## Data Access Patterns

The solution follows Onion Architecture:

- `OpenSource1.Core`: enterprise/domain model, aggregate roots and core abstractions.
- `OpenSource1.Application`: use cases, CQRS contracts, DTOs, interfaces, MediatR handlers.
- `OpenSource1.Infrastructure`: EF Core, Identity, Dapper, repositories, Unit of Work and external implementations.
- `OpenSource1.Api`: HTTP API endpoints.
- `OpenSource1.Blazor`: Blazor Web App presentation client.

The data layer uses DDD-friendly abstractions:

- `AggregateRoot<TKey>` / `IAggregateRoot` for aggregate boundaries.
- `IGenericRepository<TEntity>` constrained to aggregate roots.
- `IUnitOfWork` for transaction persistence through `SaveChangesAsync`.

CQRS is implemented with MediatR for `AppSetting`:

- Commands use EF Core through Repository + Unit of Work.
- Queries use Dapper for lean read models.
- Identity operations use ASP.NET Core Identity APIs.

This keeps LINQ/EF for aggregate persistence, Dapper for optimized reads, and Identity for user/auth concerns.

## Security, users and permissions

Local seeding creates three users when `UserSeed__Enabled=true` and `UserSeed__DefaultPassword` is supplied through environment variables:

| User | Role |
| --- | --- |
| `admin` | `Administrador` |
| `supervisor` | `Supervisor` |
| `ejecutor` | `Ejecutor` |

Role permissions:

| Role | Add | Modify | Delete | Consult |
| --- | --- | --- | --- | --- |
| Administrador | yes | yes | yes | yes |
| Supervisor | no | yes | no | yes |
| Ejecutor | yes | no | no | yes |

Auth uses ASP.NET Core Identity lockout with 3 failed attempts and JWT Bearer tokens. The `/api/auth/me` endpoint returns the current user's roles and permissions so clients can show/hide UI options by role. Unauthorized operations return `401` or `403` with a safe message.

## Blazor web application UI

The Blazor front-end uses Static SSR and Bulma instead of Bootstrap for a minimal, responsive UI. It authenticates against the API with JWT, then creates a secure HttpOnly cookie for the Blazor client and stores the JWT server-side for API calls. The login screen includes the required form components:

- Label + TextBox for `Usuario`
- Label + password TextBox for `Contraseña`
- `Iniciar Sesión` button
- `Salir` button
- MessageBox-style notifications using Bulma notifications
- Main menu with options shown/hidden according to the authenticated role permissions

The Blazor client calls the existing API through typed `HttpClient` services and sends the API JWT with a delegating handler. API endpoints still enforce JWT policies; hiding menu options is only a user experience feature.

## dotnet skills guidance

This repository includes `.opencode/skills` from `https://github.com/dotnet/skills.git`, merged local Blazor expert skill files, and should also use any installed external Blazor skills after opencode is restarted. The agent workflow should keep using those official skills for templates, Blazor, Web API, EF Core, MSBuild, testing, diagnostics and .NET best practices before making architectural or code changes.

## Local JWT Auth

JWT settings are configured for local development in `appsettings*.json`.
Authentication endpoints are available at:

- `POST /api/auth/register`
- `POST /api/auth/login`

Example requests are included in `auth.http`.

The development profiles are configured in `Properties/launchSettings.json`.

## Git Flow

- `main`: stable base branch.
- `qa`: validation branch before release.
- `deploy`: deployment-ready branch.
