# test

ASP.NET Core MVC project targeting .NET 10.

## Run Locally

```bash
dotnet restore
dotnet build
dotnet run
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

The development profiles are configured in `Properties/launchSettings.json`.

## Git Flow

- `main`: stable base branch.
- `qa`: validation branch before release.
- `deploy`: deployment-ready branch.
