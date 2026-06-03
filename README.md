# OpenSource1

Trabajo Final ISO615 UNAPEC.

ASP.NET Core MVC project targeting .NET 10.

## Run Locally

```bash
dotnet restore
dotnet build
dotnet run
```

## Run with Docker Compose

The project includes a multi-stage `Dockerfile` and a `docker-compose.yml` that runs:

- ASP.NET Core MVC app on `http://localhost:8080`
- SQL Server 2022 Developer on `localhost:1433`

Create a local `.env` file and start the full stack:

```bash
cp .env.example .env
docker compose up --build -d
```

The web container uses the SQL Server service name `sqlserver` internally and applies EF Core migrations automatically when `Database__ApplyMigrationsOnStartup=true` is set in `docker-compose.yml`.

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

The data layer uses Entity Framework Core with DDD-friendly abstractions:

- `AggregateRoot<TKey>` / `IAggregateRoot` for aggregate boundaries.
- `IGenericRepository<TEntity>` constrained to aggregate roots.
- `IUnitOfWork` for transaction persistence through `SaveChangesAsync`.

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
