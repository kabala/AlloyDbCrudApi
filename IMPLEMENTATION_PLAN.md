# Implementation Plan — AlloyDB CRUD API Boilerplate

This document is written for another AI agent that needs to understand, maintain, or extend the project.

---

## 1. Project goal

Build a small, runnable .NET 10 Minimal API that demonstrates CRUD operations against **AlloyDB for PostgreSQL** using its standard PostgreSQL wire protocol. The project also shows how to consume the `Google.Cloud.AlloyDb.V1` NuGet package for AlloyDB **Admin API** operations (cluster listing).

Local development is provided by an **AlloyDB Omni** Docker container, which is a PostgreSQL-compatible AlloyDB build.

---

## 2. Architecture overview

```
┌─────────────────────────────────────────────────────────────┐
│  Client / Browser / curl                                    │
└──────────────┬──────────────────────────────────────────────┘
               │ HTTP
┌──────────────▼──────────────────────────────────────────────┐
│  .NET 10 Minimal API                                        │
│  • ItemEndpoints (CRUD on /api/items)                       │
│  • AlloyDbAdminEndpoints (Admin API sample)                 │
│  • EF Core AppDbContext                                     │
└──────────────┬───────────────────────┬──────────────────────┘
               │ SQL                   │ gRPC/REST
┌──────────────▼──────┐  ┌─────────────▼──────────────────┐
│  AlloyDB Omni       │  │  Google Cloud AlloyDB Admin API│
│  (Docker, port 5432)│  │  (via Google.Cloud.AlloyDb.V1) │
└─────────────────────┘  └────────────────────────────────┘
```

Key design choices:

- **Minimal APIs** are used instead of controllers to keep the boilerplate small.
- **Entity Framework Core** is used for the CRUD layer with the **Npgsql** PostgreSQL provider.
- Endpoints are organized in static `Map*Endpoints` extension methods in the `Endpoints/` folder.
- The Admin API sample is **opt-in** via configuration because it requires GCP credentials.

---

## 3. Technology stack

| Technology | Role | Version in project |
|---|---|---|
| .NET SDK | Runtime / build | 10.0.x (TFM `net10.0`) |
| ASP.NET Core Minimal APIs | HTTP API layer | 10.0.x |
| `Microsoft.AspNetCore.OpenApi` | OpenAPI document generation | 10.0.8 |
| `Scalar.AspNetCore` | Interactive API docs UI | 2.16.3 |
| Entity Framework Core | ORM / migrations | 10.0.4 |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | EF Core provider for PostgreSQL/AlloyDB | 10.0.2 |
| `Microsoft.EntityFrameworkCore.Design` | Design-time EF Core tooling | 10.0.4 |
| `Google.Cloud.AlloyDb.V1` | AlloyDB Admin API client | 1.14.0 |
| `google/alloydbomni:17` | Local PostgreSQL-compatible AlloyDB container | 17 (latest stable major) |

---

## 4. Project structure

```
.
├── AlloyDbCrudApi.csproj            # Project file + NuGet references
├── Program.cs                       # App bootstrap, DI, middleware pipeline
├── appsettings.json                 # Base config + connection string
├── appsettings.Development.json     # Development overrides
├── AlloyDbCrudApi.http              # REST Client / .http file test requests
├── Dockerfile                       # Multi-stage build for the API
├── docker-compose.yml               # Local AlloyDB Omni + API services
├── .env                             # Local DB credentials template
├── README.md                        # Human-readable quick start
├── IMPLEMENTATION_PLAN.md           # This file
├── Data/
│   └── AppDbContext.cs              # EF Core DbContext + model config
├── Endpoints/
│   ├── ItemEndpoints.cs             # /api/items CRUD Minimal API endpoints
│   └── AlloyDbAdminEndpoints.cs     # /api/admin/alloydb Admin API sample
├── Models/
│   └── Item.cs                      # Domain entity
└── Migrations/
    ├── YYYYMMDDHHMMSS_InitialCreate.cs
    ├── YYYYMMDDHHMMSS_InitialCreate.Designer.cs
    └── AppDbContextModelSnapshot.cs
```

---

## 5. Data layer

### Entity

`Models/Item.cs`:

```csharp
public class Item
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

### DbContext

`Data/AppDbContext.cs` configures the model:

- Table name: `items`
- `Id` is an identity column (`UseIdentityAlwaysColumn`)
- `Name` is required, max length 200
- `Description` is optional, max length 1000
- `CreatedAt` defaults to `NOW()`

### Migrations

- Initial migration: `InitialCreate`
- Applied automatically on startup in `Development` via `db.Database.Migrate()` in `Program.cs`
- Manual migration commands:
  ```bash
  dotnet ef migrations add <Name>
  dotnet ef database update
  ```

---

## 6. API layer

### CRUD endpoints

Implemented in `Endpoints/ItemEndpoints.cs` and registered with `app.MapItemEndpoints()`.

| Method | Route | Behavior |
|---|---|---|
| GET | `/api/items` | List all items ordered by `CreatedAt` desc |
| GET | `/api/items/{id}` | Get one item; 404 if not found |
| POST | `/api/items` | Create item; returns 201 with `Location` header |
| PUT | `/api/items/{id}` | Update name/description; 404 if not found |
| DELETE | `/api/items/{id}` | Delete item; 404 if not found |

The POST handler resets `Id` and `CreatedAt` server-side to avoid client tampering.

### Admin API sample

Implemented in `Endpoints/AlloyDbAdminEndpoints.cs` and registered with `app.MapAlloyDbAdminEndpoints()`.

- Route: `GET /api/admin/alloydb/clusters`
- Disabled unless `AlloyDbAdmin:Enabled` is `true`
- Uses `AlloyDBAdminClient.CreateAsync()` and `ListClustersAsync(...)`
- Requires GCP Application Default Credentials and `roles/alloydb.viewer` (or broader)
- Returns a JSON projection of cluster name, display name, state, and database version

---

## 7. Configuration

Configuration is read from `appsettings.json`, then environment-specific files, then environment variables.

### Connection string

Key: `ConnectionStrings:DefaultConnection`

Default in `appsettings.json`:

```
Host=localhost;Port=5432;Database=cruddb;Username=postgres;Password=devpassword
```

Override via environment variable:

```bash
ConnectionStrings__DefaultConnection="Host=..."
```

### AlloyDB Admin API settings

Key prefix: `AlloyDbAdmin`

```json
{
  "AlloyDbAdmin": {
    "Enabled": false,
    "ProjectId": "",
    "Location": "us-central1"
  }
}
```

---

## 8. Local development with Docker Compose

Docker Compose spins up two services:

1. `alloydb` — `google/alloydbomni:17` on host port `5432`
2. `api` — the .NET API on host port `8080`

The API waits for the AlloyDB service to be healthy before starting.

### Start

```bash
docker compose up -d
```

### Verify

```bash
docker compose ps
```

Look for `(healthy)` on the `alloydb` container.

### Access

- API: `http://localhost:8080`
- Scalar UI: `http://localhost:8080/scalar`

### Stop

```bash
docker compose down
```

Reset data:

```bash
docker compose down -v
```

---

## 9. Running without Docker

1. Ensure a PostgreSQL-compatible database is reachable on `localhost:5432`.
2. Update `appsettings.Development.json` (or set env vars) with the correct connection string.
3. Run:
   ```bash
   dotnet run --launch-profile http
   ```
4. Use the URL printed in the console (default `http://localhost:5200`).

---

## 10. Build & test commands

```bash
# Restore packages
dotnet restore

# Build
dotnet build

# Run
dotnet run --launch-profile http

# Add migration
dotnet ef migrations add <MigrationName>

# Apply migrations manually
dotnet ef database update
```

### Quick curl tests

```bash
# Create
curl -X POST http://localhost:8080/api/items \
  -H "Content-Type: application/json" \
  -d '{"name":"Hello","description":"AlloyDB item"}'

# List
curl http://localhost:8080/api/items

# Get one
curl http://localhost:8080/api/items/1

# Update
curl -X PUT http://localhost:8080/api/items/1 \
  -H "Content-Type: application/json" \
  -d '{"id":1,"name":"Updated","description":"New text"}'

# Delete
curl -X DELETE http://localhost:8080/api/items/1
```

---

## 11. How to extend this project

### Add a new entity

1. Create a class in `Models/`
2. Add a `DbSet<T>` property in `Data/AppDbContext.cs`
3. Configure the entity in `OnModelCreating` if needed
4. Generate a migration:
   ```bash
   dotnet ef migrations add <Name>
   ```

### Add new CRUD endpoints

1. Create a static class in `Endpoints/`
2. Add a `public static IEndpointRouteBuilder MapXxxEndpoints(this IEndpointRouteBuilder app)` method
3. Register it in `Program.cs` with `app.MapXxxEndpoints();`

### Enable the Admin API sample

1. Run `gcloud auth application-default login`
2. Set `AlloyDbAdmin:Enabled` to `true` and `AlloyDbAdmin:ProjectId` to a valid GCP project
3. Ensure the caller has IAM permission to list AlloyDB clusters

---

## 12. Important notes & caveats

- `Google.Cloud.AlloyDb.V1` is the **Admin API** client, not the SQL driver. CRUD uses Npgsql/EF Core because AlloyDB is PostgreSQL-compatible.
- `app.Environment.IsDevelopment()` triggers automatic migration. This is convenient for local work but should not be used in production.
- `UseHttpsRedirection` was intentionally removed to avoid issues inside the Docker container.
- The AlloyDB Omni container is large (~1.6 GB) and may take time to pull on first run.
- The healthcheck in `docker-compose.yml` uses `pg_isready`, which is included in the AlloyDB Omni image.

---

## 13. Files to read first when onboarding

1. `Program.cs` — app setup and DI
2. `Endpoints/ItemEndpoints.cs` — main CRUD API
3. `Data/AppDbContext.cs` + `Models/Item.cs` — data model
4. `docker-compose.yml` — local runtime topology
5. `appsettings.json` — configuration defaults
