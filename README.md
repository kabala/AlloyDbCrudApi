# Retail CRM/POS API

Backend for a textile retail CRM/POS MVP built with .NET 10 Minimal APIs, Entity Framework Core, ASP.NET Core Identity, JWT authentication, and PostgreSQL.

The API stores operational data that can later feed BI datasets in Google Cloud. BI processing, BigQuery models, dashboards, and ETL workers are intentionally out of scope for this project.

## What It Does

The application exposes authenticated retail operations for:

- Users, roles, login, JWT access tokens, and refresh tokens.
- Products, suppliers, customers, stores, and inventory.
- POS sales with line items, discount validation, margin rules, and stock decrement.
- Returns with sale status updates and inventory restoration.
- Audit logs for sensitive business actions.
- Health checks and OpenAPI/Scalar docs in development.

Domain rules protect the data quality expected by the BI source system: products cannot use invalid categories such as `???`, colors are required, customer gender is controlled, sale quantities must be positive, discounts are bounded, stock cannot go negative, and duplicate transaction ids are rejected.

## Architecture

```text
Browser SPA or API client
  -> ASP.NET Core .NET 10 Minimal API
    -> Application services and validators
      -> Domain entities and rules
        -> EF Core / PostgreSQL operational database
          -> future Google Cloud BI pipeline
```

Local development uses PostgreSQL 16 in Docker Compose. Production uses Cloud Run and Cloud SQL for PostgreSQL. Cloud SQL logical decoding is enabled by infrastructure so a future Datastream or BigQuery pipeline can be added outside this API.

## Tech Stack

- .NET 10 Minimal APIs
- Entity Framework Core 10
- ASP.NET Core Identity
- JWT Bearer authentication
- FluentValidation
- Npgsql.EntityFrameworkCore.PostgreSQL
- Scalar.AspNetCore API docs UI
- Serilog structured logging
- PostgreSQL 16 for local development
- Cloud Run, Cloud SQL, Secret Manager, Artifact Registry, and OpenTofu for production

## Prerequisites

- .NET 10 SDK
- Docker Desktop or Docker Engine
- OpenTofu and Google Cloud CLI for production infrastructure
- Restored local tools for EF commands:

```bash
dotnet tool restore
```

## Run Locally With Docker

Start PostgreSQL and the API:

```bash
docker compose up -d --build
```

Open:

- API base URL: `http://localhost:8080`
- Scalar docs: `http://localhost:8080/scalar`
- Health check: `http://localhost:8080/health`

Stop containers:

```bash
docker compose down
```

Reset the local database volume:

```bash
docker compose down -v
```

## Run Locally With dotnet

Start only PostgreSQL:

```bash
docker compose up -d postgres
```

Run the API from the host:

```bash
dotnet run --launch-profile http
```

Open:

- API base URL: `http://localhost:5200`
- Scalar docs: `http://localhost:5200/scalar`

In development, the app applies EF Core migrations and runs the retail seed data automatically on startup.

## Build And Test

Restore dependencies:

```bash
dotnet restore
```

Build the solution:

```bash
dotnet build AlloyDbCrudApi.slnx
```

Run tests:

```bash
dotnet test AlloyDbCrudApi.slnx
```

Run the same release build used by CI:

```bash
dotnet build --configuration Release --no-restore
```

## Database Migrations

Apply migrations locally:

```bash
dotnet ef database update
```

Add a migration after changing the EF model:

```bash
dotnet ef migrations add YourMigrationName
```

Build the production migration bundle:

```bash
dotnet ef migrations bundle --configuration Release --self-contained --target-runtime linux-x64 --output ./artifacts/efbundle --force
```

Production does not run `Database.Migrate()` on API startup. Schema changes are applied by the `Migrate Production Database` GitHub Actions workflow through a Cloud Run Job.

## Main API Routes

| Module | Routes |
|---|---|
| Auth | `POST /api/auth/login`, `POST /api/auth/refresh` |
| Users | `GET /api/users`, `GET /api/users/{id}`, `POST /api/users` |
| Products | `GET /api/products`, `GET /api/products/{productId}`, `POST /api/products` |
| Customers | `GET /api/customers`, `GET /api/customers/{customerId}`, `POST /api/customers` |
| Sales | `GET /api/sales`, `GET /api/sales/{transactionId}`, `POST /api/sales` |
| Returns | `GET /api/returns/{id}`, `POST /api/returns` |
| Inventory | `GET /api/inventory`, `GET /api/inventory/by?storeId=&productId=`, `GET /api/inventory/stores` |
| BI | `GET /api/bi/dashboard`, `GET /api/bi/products/abc`, `GET /api/bi/customers/rfm`, `GET /api/bi/breakdowns/{dimension}` |
| Health | `GET /health`, `GET /health/ready` |

Most routes require JWT authentication and role-based authorization. Seeded development data includes users and catalog records for local testing.

## Production Deployment

Production infrastructure lives in `infra/opentofu` and creates:

- Cloud SQL for PostgreSQL shared-core instance.
- Application database and generated database user password.
- Secret Manager connection strings.
- Cloud Run API service.
- Cloud Run migration job service account.
- Artifact Registry repository.
- Workload Identity Federation for GitHub Actions.
- GitHub Actions repository variables.

Initialize and apply infrastructure:

```powershell
Set-Location infra/opentofu
Copy-Item terraform.tfvars.example terraform.tfvars
tofu init
tofu plan
tofu apply
```

After infrastructure exists, deploy in this order:

1. Push to GitHub and confirm the `CI` workflow passes.
2. Run the `Migrate Production Database` workflow manually if there are schema changes.
3. Run the `Deploy Production` workflow manually to build, push, and deploy the API image to Cloud Run.

The deploy workflow builds `Dockerfile`, pushes the image to Artifact Registry, and runs:

```bash
gcloud run deploy "$CLOUD_RUN_SERVICE" \
  --project "$PROJECT_ID" \
  --region "$REGION" \
  --platform managed \
  --image "$IMAGE" \
  --service-account "$CLOUD_RUN_SERVICE_ACCOUNT" \
  --update-env-vars "ASPNETCORE_ENVIRONMENT=Production" \
  --set-secrets "ConnectionStrings__DefaultConnection=${DB_CONNECTION_SECRET}:latest" \
  --add-cloudsql-instances "$CLOUD_SQL_INSTANCE_CONNECTION_NAME"
```

The migration workflow builds `Dockerfile.migrations`, deploys a Cloud Run Job, and executes it against Cloud SQL.

## Configuration

Important settings:

- `ConnectionStrings__DefaultConnection`: PostgreSQL connection string.
- `Jwt__Issuer`, `Jwt__Audience`, `Jwt__SigningKey`: JWT settings. Development has a fallback signing key; production must provide a real secret.
- `Cors__AllowedOrigins__0`: allowed frontend origin for browser clients.
- `ASPNETCORE_ENVIRONMENT`: use `Development` locally and `Production` in Cloud Run.

Cloud Run connects to Cloud SQL through the socket mount at `/cloudsql` using a normal Npgsql connection string. No Cloud SQL-specific NuGet package is required.

## Local BI Module

The API also exposes a fallback local BI module over the same operational PostgreSQL data:

- `GET /api/bi/dashboard` returns summary KPIs, yearly/monthly trends, category/store/city metrics, discount impact, and notebook-style recommendations.
- `GET /api/bi/products/abc` returns ABC-ranked products by cumulative revenue.
- `GET /api/bi/customers/rfm` returns customer RFM rankings using the max sale date in the filtered dataset as the recency reference.
- `GET /api/bi/breakdowns/{dimension}` returns read-only grouped metrics for category, store, city, supplier, season, discount, and return-focused cuts.

These responses are cached in memory for 5 minutes to reduce repeated load on the Cloud SQL instance. This module is intentionally a short-term embedded BI surface, not the final BigQuery architecture.

## Project Structure

```text
Application/      Contracts, service interfaces, validators
Domain/           Entities, enums, and domain exceptions
Endpoints/        Minimal API route modules
Infrastructure/   EF Core persistence, identity, seeding, services
Migrations/       EF Core migrations
tests/            xUnit domain and application tests
docs/             Extra deployment, schema, and frontend notes
infra/opentofu/   Google Cloud production infrastructure
```

## References

- [Production Deployment](docs/production-deployment.md)
- [Database Schema](docs/database-schema.md)
- [Frontend Integration](docs/frontend-integration.md)
- [Local BI Module Plan](docs/local-bi-module-plan.md)
