# Implementation Plan — Cloud SQL CRUD API Boilerplate

## Goal

Build a small .NET 10 Minimal API that demonstrates CRUD operations against PostgreSQL-compatible relational storage. Local development uses a PostgreSQL container. Production uses Cloud SQL for PostgreSQL on Google Cloud and is prepared for a later lakehouse phase using CDC.

## Current Architecture

```text
Browser SPA
  -> public Cloud Run API
    -> Cloud SQL for PostgreSQL
      -> future Datastream CDC
        -> future BigQuery / Cloud Storage / BigLake / Iceberg lakehouse
```

## Application Stack

- .NET 10 Minimal APIs
- Entity Framework Core
- Npgsql.EntityFrameworkCore.PostgreSQL
- Scalar.AspNetCore for development API docs
- PostgreSQL 16 local container

No Cloud SQL-specific NuGet package is required. In production, Cloud Run mounts the Cloud SQL socket at `/cloudsql`, and the app connects with a normal Npgsql connection string.

## Production Infrastructure

OpenTofu in `infra/opentofu` manages:

- Cloud SQL for PostgreSQL shared-core instance
- Application database and generated database user password
- Secret Manager connection strings
- Cloud Run API service
- Cloud Run migration job service account
- Artifact Registry repository
- Workload Identity Federation for GitHub Actions
- GitHub Actions repository variables

Cloud SQL has `cloudsql.logical_decoding` enabled so Datastream CDC can be added later.

## Deployment Flow

1. Apply OpenTofu from `infra/opentofu`.
2. Run `Migrate Production Database`.
3. Run `Deploy Production`.

Production migrations are run by an EF Core migration bundle in a Cloud Run Job. The API does not run `Database.Migrate()` in production.

## Frontend Assumption

The frontend will be a public browser SPA in a separate repository. The API is publicly invokable and uses CORS to allow only configured frontend origins. CORS is not authentication; application-level auth must be added before sensitive data or mutations are exposed.

