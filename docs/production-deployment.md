# Production Deployment

Docker Compose is only for local development with a PostgreSQL container. Production uses Cloud SQL for PostgreSQL in Google Cloud.

## Architecture

- GitHub Actions authenticates to Google Cloud with Workload Identity Federation.
- The API image is pushed to Artifact Registry.
- Cloud Run hosts the API container. The service configuration is managed by OpenTofu; GitHub Actions updates the image for each release.
- Cloud Run connects to Cloud SQL through the Cloud SQL socket mount at `/cloudsql`.
- Secret Manager stores database connection strings.
- EF Core migrations run from a separate Cloud Run Job built from `Dockerfile.migrations`.
- The historical BI seed runs from a separate Cloud Run Job using the normal API image with `--seed retail-bi-history`.

The application does not run `Database.Migrate()` in production. Production schema changes are applied by the `Migrate Production Database` workflow. The historical BI seed is also explicit and never runs on API startup.

## Infrastructure With OpenTofu

Production Google Cloud resources are managed with OpenTofu in `infra/opentofu`.

```powershell
cd infra/opentofu
Copy-Item terraform.tfvars.example terraform.tfvars
tofu init
tofu plan
tofu apply
```

The OpenTofu configuration creates:

- Cloud SQL for PostgreSQL shared-core instance.
- Application database and user.
- Cloud SQL logical decoding flag for future CDC/lakehouse work.
- Artifact Registry repository.
- Cloud Run API service with min instances set to zero for pay-per-use operation.
- Secret Manager connection-string secrets.
- Runtime, migration, and GitHub deploy service accounts.
- Workload Identity Federation for GitHub Actions.
- Required GitHub repository variables.

The current setup uses local OpenTofu state under `infra/opentofu`. That file is ignored by Git. Keep it safe until you migrate state to a remote backend.

## Browser SPA Access

Because the frontend is a browser-only SPA in a separate repository, the API must be publicly invokable and CORS should be restricted to the exact frontend origin:

```hcl
cloud_run_allow_unauthenticated = true
cors_allowed_origins = [
  "https://your-frontend-url.run.app"
]
```

CORS only controls browser cross-origin reads. It does not authenticate users or block non-browser clients, so public API endpoints that mutate or expose private data should still have application-level authentication.

## Cloud SQL Connectivity

Cloud Run uses its built-in Cloud SQL integration. The connection string stored in Secret Manager uses a Unix socket path:

```text
Host=/cloudsql/PROJECT:REGION:INSTANCE;Port=5432;Database=cruddb;Username=app_user;Password=REPLACE_ME
```

No additional .NET NuGet package is required for this connection mode. The app continues to use `Npgsql.EntityFrameworkCore.PostgreSQL`; Cloud Run provides the socket mount and Cloud SQL authorization.

The Cloud Run runtime and migration service accounts need:

- `roles/cloudsql.client`
- `roles/secretmanager.secretAccessor` on the relevant connection-string secret

## GitHub Variables

OpenTofu manages these repository variables:

| Variable | Example |
|---|---|
| `GCP_PROJECT_ID` | `personal-434212` |
| `GCP_REGION` | `us-east1` |
| `GCP_WORKLOAD_IDENTITY_PROVIDER` | `projects/123/locations/global/workloadIdentityPools/github/providers/github` |
| `GCP_DEPLOY_SERVICE_ACCOUNT` | `github-deploy@personal-434212.iam.gserviceaccount.com` |
| `ARTIFACT_REGISTRY_REPOSITORY` | `containers` |
| `API_IMAGE_NAME` | `cloudsql-crud-api` |
| `CLOUD_RUN_SERVICE` | `cloudsql-crud-api` |
| `CLOUD_RUN_SERVICE_ACCOUNT` | `cloudsql-crud-api@personal-434212.iam.gserviceaccount.com` |
| `CLOUD_SQL_INSTANCE_CONNECTION_NAME` | `personal-434212:us-east1:crud-postgres` |
| `MIGRATION_JOB` | `cloudsql-crud-api-migrations` |
| `MIGRATION_SERVICE_ACCOUNT` | `cloudsql-crud-migrations@personal-434212.iam.gserviceaccount.com` |
| `DB_CONNECTION_SECRET` | `cloudsql-crud-api-connection` |
| `MIGRATION_DB_CONNECTION_SECRET` | `cloudsql-crud-api-migration-connection` |
| `CLOUD_RUN_ALLOW_UNAUTHENTICATED` | `true` |

## Lakehouse Direction

Cloud SQL for PostgreSQL is the relational source. The intended lakehouse path is:

```text
Cloud SQL for PostgreSQL
  -> Datastream CDC
    -> BigQuery and/or Cloud Storage / BigLake / Iceberg tables
```

The Cloud SQL instance enables `cloudsql.logical_decoding` for CDC readiness. A later infrastructure phase should add Datastream streams and the target analytical storage.

## Workflow Order

1. Run `CI` on pull requests and pushes.
2. Run `Migrate Production Database` manually when a deployment includes schema changes.
3. Run `Seed BI History` manually when the production database needs the initial historical BI load.
4. Run `Deploy Production` manually to deploy the API image to Cloud Run.

For breaking schema changes, use expand-and-contract migrations so the currently deployed app and the new app can both run during rollout.

## Manual Cloud Seed

If the seed workflow is not yet present on the default branch, run the initial historical seed directly with `gcloud`:

```bash
gcloud builds submit \
  --project "$PROJECT_ID" \
  --tag "$REGION-docker.pkg.dev/$PROJECT_ID/$ARTIFACT_REGISTRY_REPOSITORY/$IMAGE_NAME:$TAG" .

gcloud run jobs deploy cloudsql-crud-api-seed-bi-history \
  --project "$PROJECT_ID" \
  --region "$REGION" \
  --image "$REGION-docker.pkg.dev/$PROJECT_ID/$ARTIFACT_REGISTRY_REPOSITORY/$IMAGE_NAME:$TAG" \
  --service-account "$MIGRATION_SERVICE_ACCOUNT" \
  --set-secrets "ConnectionStrings__DefaultConnection=${MIGRATION_DB_CONNECTION_SECRET}:latest" \
  --set-cloudsql-instances "$CLOUD_SQL_INSTANCE_CONNECTION_NAME" \
  --args=--seed=retail-bi-history \
  --tasks 1 \
  --max-retries 0 \
  --task-timeout 1800s

gcloud run jobs execute cloudsql-crud-api-seed-bi-history \
  --project "$PROJECT_ID" \
  --region "$REGION" \
  --wait
```

This path was verified against the current project configuration in `personal-434212`. The working secret for the seed job is `cloudsql-crud-api-migration-connection`, because the migration service account already has access to that secret.

The successful Cloud Run execution inserted:

- `50000` products
- `25000` customers
- `43489` sales
- `36732` inventory rows
- `4317` returns

and logged final totals near the BI target profile: revenue `10799681.41`, margin `6164283.08`, database size from about `8.24MB` to about `44.3MB`.

## Local Development

Keep using Docker Compose locally:

```bash
docker compose up -d
```

The Compose file uses a local PostgreSQL container. It is not used for production deployment.
