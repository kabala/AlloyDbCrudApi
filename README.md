# Cloud SQL CRUD API Boilerplate

A Minimal API CRUD boilerplate built with **.NET 10**, **Entity Framework Core**, and **Npgsql** for PostgreSQL-compatible relational storage.

Local development uses a standard PostgreSQL container. Production uses **Cloud SQL for PostgreSQL** on Google Cloud, deployed with OpenTofu and Cloud Run.

## Tech Stack

- .NET 10 Minimal APIs
- Entity Framework Core 10
- Npgsql.EntityFrameworkCore.PostgreSQL
- Scalar.AspNetCore API docs UI
- PostgreSQL 16 container for local development
- Cloud Run + Cloud SQL for production

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Docker Desktop or Docker Engine
- Optional: `dotnet-ef` CLI tool if you want to add migrations

```bash
dotnet tool install --global dotnet-ef
```

## Run Locally

Docker Compose starts:

- PostgreSQL on host port `5432`
- the .NET API on host port `8080`

```bash
docker compose up -d
```

Open:

- API: `http://localhost:8080/api/items`
- Scalar docs: `http://localhost:8080/scalar`

Stop everything:

```bash
docker compose down
```

Remove local database data:

```bash
docker compose down -v
```

## Run Without Docker

Update the connection string in `appsettings.json` or through environment variables:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=cruddb;Username=postgres;Password=devpassword"
  }
}
```

Then run:

```bash
dotnet run --launch-profile http
```

## API Endpoints

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/items` | List all items |
| GET | `/api/items/{id}` | Get a single item |
| POST | `/api/items` | Create a new item |
| PUT | `/api/items/{id}` | Update an item |
| DELETE | `/api/items/{id}` | Delete an item |

Example:

```bash
curl -X POST http://localhost:8080/api/items \
  -H "Content-Type: application/json" \
  -d '{"name": "Sample", "description": "Hello PostgreSQL"}'
```

## Production

Production infrastructure lives in `infra/opentofu` and creates:

- Cloud SQL for PostgreSQL
- Cloud Run API service
- Cloud Run migration job identity
- Secret Manager connection strings
- Artifact Registry
- GitHub Actions Workload Identity Federation

See:

- [Production Deployment](docs/production-deployment.md)
- [Database Schema](docs/database-schema.md)
- [Frontend Integration](docs/frontend-integration.md)

Production does not auto-apply migrations on app startup. Use the `Migrate Production Database` GitHub Actions workflow.

## Migrations

Apply migrations locally:

```bash
dotnet ef database update
```

Add a migration after changing the model:

```bash
dotnet ef migrations add YourMigrationName
```

## Project Structure

```text
.
├── Data/
│   └── AppDbContext.cs
├── Endpoints/
│   └── ItemEndpoints.cs
├── Migrations/
├── Models/
│   └── Item.cs
├── docs/
├── infra/opentofu/
├── Program.cs
├── docker-compose.yml
└── Dockerfile
```

## Notes

- No Cloud SQL-specific NuGet package is required for the deployed API. Cloud Run mounts the Cloud SQL socket and the app uses Npgsql normally.
- The public API is intended for a browser SPA and uses configurable CORS. Add application-level authentication before exposing sensitive data or mutations.
