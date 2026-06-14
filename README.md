# AlloyDB CRUD API Boilerplate

A Minimal API CRUD boilerplate built with **.NET 10**, **Entity Framework Core**, and **Npgsql** to talk to **AlloyDB for PostgreSQL** using its standard PostgreSQL interface.

It also includes a sample integration with the **Google.Cloud.AlloyDb.V1** NuGet package for listing AlloyDB clusters through the Admin API.

---

## Tech stack

- .NET 10 (Minimal APIs)
- Entity Framework Core 10
- Npgsql.EntityFrameworkCore.PostgreSQL 10.0.2
- Google.Cloud.AlloyDb.V1 1.14.0
- Scalar.AspNetCore (API docs UI)
- AlloyDB Omni Docker image (`google/alloydbomni:17`) for local development

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) or Docker Engine (for local AlloyDB Omni)
- (Optional) `dotnet-ef` CLI tool if you want to add more migrations:
  ```bash
  dotnet tool install --global dotnet-ef
  ```

---

## Installation

1. Clone or generate the project, then open a terminal in the project folder.
2. Restore NuGet packages:
   ```bash
   dotnet restore
   ```
3. Build the project:
   ```bash
   dotnet build
   ```

---

## Run with Docker Compose (recommended)

This is the fastest way to get everything running locally. Docker Compose is local-only and starts:

- an **AlloyDB Omni** container on port `5432`
- the .NET API container on port `8080`

```bash
docker compose up -d
```

Wait for the `alloydb` service to report healthy:

```bash
docker compose ps
```

Once healthy, open:

- API base URL: `http://localhost:8080`
- Interactive API docs (Scalar): `http://localhost:8080/scalar`

Production does not use Docker Compose or AlloyDB Omni. See [Production Deployment](docs/production-deployment.md) for the Cloud Run + real AlloyDB workflow, including the `scripts/provision-gcp-free-trial.ps1` helper for creating an AlloyDB free-trial cluster and related Google Cloud resources.

### Stop everything

```bash
docker compose down
```

To also remove the database volume and start fresh:

```bash
docker compose down -v
```

---

## Run locally without Docker

If you already have a PostgreSQL-compatible AlloyDB instance (or the AlloyDB Omni container running separately), update the connection string in `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=cruddb;Username=postgres;Password=YOUR_PASSWORD"
  }
}
```

Then run:

```bash
dotnet run --launch-profile http
```

The API will be available at the URL shown in the terminal output (default `http://localhost:5200`).

---

## Test the API

You can use the included `.http` file with Visual Studio / VS Code REST Client, or run `curl` commands:

### Create an item

```bash
curl -X POST http://localhost:8080/api/items \
  -H "Content-Type: application/json" \
  -d '{"name": "Sample", "description": "Hello AlloyDB"}'
```

### List items

```bash
curl http://localhost:8080/api/items
```

### Get one item

```bash
curl http://localhost:8080/api/items/1
```

### Update an item

```bash
curl -X PUT http://localhost:8080/api/items/1 \
  -H "Content-Type: application/json" \
  -d '{"id": 1, "name": "Updated", "description": "New description"}'
```

### Delete an item

```bash
curl -X DELETE http://localhost:8080/api/items/1
```

---

## API endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/items` | List all items |
| GET | `/api/items/{id}` | Get a single item |
| POST | `/api/items` | Create a new item |
| PUT | `/api/items/{id}` | Update an item |
| DELETE | `/api/items/{id}` | Delete an item |

### Optional: AlloyDB Admin API sample

The endpoint `/api/admin/alloydb/clusters` demonstrates using the `Google.Cloud.AlloyDb.V1` NuGet package. It is **disabled by default**.

To enable it:

1. Authenticate with GCP:
   ```bash
   gcloud auth application-default login
   ```
2. Update `appsettings.Development.json`:
   ```json
   {
     "AlloyDbAdmin": {
       "Enabled": true,
       "ProjectId": "your-gcp-project-id",
       "Location": "us-central1"
     }
   }
   ```
3. Make sure the authenticated principal has the `roles/alloydb.viewer` IAM role.

---

## Database migrations

The project already contains an `InitialCreate` migration. It is applied automatically when the app starts in the `Development` environment.

Production does not auto-apply migrations on app startup. Use the `Migrate Production Database` GitHub Actions workflow, which builds an EF Core migration bundle and runs it as a Cloud Run Job inside the VPC that can reach AlloyDB.

To apply migrations manually:

```bash
dotnet ef database update
```

To add a new migration after changing the model:

```bash
dotnet ef migrations add YourMigrationName
```

---

## Project structure

```
.
├── Data/
│   └── AppDbContext.cs
├── Endpoints/
│   ├── ItemEndpoints.cs          # CRUD minimal API endpoints
│   └── AlloyDbAdminEndpoints.cs  # Admin API sample using Google.Cloud.AlloyDb.V1
├── Migrations/                   # EF Core migrations
├── Models/
│   └── Item.cs
├── Program.cs
├── appsettings.json
├── appsettings.Development.json
├── docker-compose.yml
├── Dockerfile
├── .env
├── AlloyDbCrudApi.http
└── README.md
```

---

## Notes

- `Google.Cloud.AlloyDb.V1` is the **AlloyDB Admin API** client (cluster/instance management), not the database driver. The CRUD operations use `Npgsql.EntityFrameworkCore.PostgreSQL` because AlloyDB is PostgreSQL-compatible.
- For production AlloyDB connections, use the **AlloyDB Auth Proxy** or a private IP connection string. Update `ConnectionStrings:DefaultConnection` accordingly.
