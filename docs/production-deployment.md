# Production Deployment

Docker Compose is only for local development with AlloyDB Omni. Production uses a real AlloyDB for PostgreSQL instance in Google Cloud.

## Architecture

- GitHub Actions authenticates to Google Cloud with Workload Identity Federation.
- The API image is pushed to Artifact Registry.
- Cloud Run hosts the API container. The service configuration is managed by OpenTofu; GitHub Actions updates the image for each release.
- Cloud Run reaches AlloyDB private IP through a Serverless VPC Access connector.
- Secret Manager stores database connection strings.
- EF Core migrations run from a separate Cloud Run Job built from `Dockerfile.migrations`.

The application does not run `Database.Migrate()` in production. Production schema changes are applied by the `Migrate Production Database` workflow.

## Infrastructure With OpenTofu

Production Google Cloud resources are managed with OpenTofu in `infra/opentofu`.

```powershell
cd infra/opentofu
Copy-Item terraform.tfvars.example terraform.tfvars
# Edit terraform.tfvars and set alloydb_postgres_password.
tofu init
tofu plan
tofu apply
```

The OpenTofu configuration creates:

- AlloyDB free trial cluster and 8 vCPU primary instance.
- Private services access on the selected VPC.
- Serverless VPC Access connector.
- Artifact Registry repository.
- Cloud Run API service with min instances set to zero for pay-per-use operation.
- Secret Manager connection-string secrets.
- Runtime, migration, and GitHub deploy service accounts.
- Workload Identity Federation for GitHub Actions.
- Required GitHub repository variables.

The configuration uses the default `postgres` database by default because AlloyDB database creation is a SQL operation after the instance exists. If you create another database separately in AlloyDB Studio or from a VPC-connected SQL client, update `alloydb_database_name` and re-apply.

The AlloyDB initial password and Secret Manager connection-string values are passed with the Google provider's write-only fields where supported. Still treat OpenTofu plans and state access as sensitive, and use encrypted remote state with tightly scoped access before sharing this environment.

The current setup uses local OpenTofu state under `infra/opentofu`. That file is ignored by Git. Keep it safe until you migrate state to a remote backend.

## Browser SPA Access

If the frontend is a browser-only SPA in a separate repository, it cannot use a private Cloud Run service account identity directly from the browser. In that architecture the API must be publicly invokable, and CORS should be restricted to the exact frontend origin:

```hcl
cloud_run_allow_unauthenticated = true
cors_allowed_origins = [
  "https://alloydb-crud-frontend-dmkxnmuy3q-ue.a.run.app"
]
```

CORS only controls browser cross-origin reads. It does not authenticate users or block non-browser clients, so public API endpoints that mutate or expose private data should still have application-level authentication.

## Required Google Cloud Resources

Create these before running the workflows:

- AlloyDB cluster and primary instance.
- Database, application user, and migration user.
- VPC with private services access configured for AlloyDB.
- Serverless VPC Access connector in the same region as Cloud Run.
- Artifact Registry Docker repository.
- Cloud Run API service.
- Secret Manager secret for the application connection string.
- Optional separate Secret Manager secret for the migration connection string.
- Runtime service account for Cloud Run.
- Migration service account for the Cloud Run Job.
- Deploy service account used by GitHub Actions.

Use an AlloyDB private IP address in the connection string:

```text
Host=10.0.0.5;Port=5432;Database=cruddb;Username=app_user;Password=REPLACE_ME;SSL Mode=Require;Trust Server Certificate=true
```

Store it in Secret Manager:

```bash
printf '%s' 'Host=10.0.0.5;Port=5432;Database=cruddb;Username=app_user;Password=REPLACE_ME;SSL Mode=Require;Trust Server Certificate=true' \
  | gcloud secrets create alloydb-crud-api-connection --data-file=-
```

For migrations, prefer a separate database user that can apply DDL:

```bash
printf '%s' 'Host=10.0.0.5;Port=5432;Database=cruddb;Username=migration_user;Password=REPLACE_ME;SSL Mode=Require;Trust Server Certificate=true' \
  | gcloud secrets create alloydb-crud-api-migration-connection --data-file=-
```

## GitHub Variables

Set these repository or environment variables in GitHub:

| Variable | Example |
|---|---|
| `GCP_PROJECT_ID` | `my-project` |
| `GCP_REGION` | `us-central1` |
| `GCP_WORKLOAD_IDENTITY_PROVIDER` | `projects/123/locations/global/workloadIdentityPools/github/providers/github` |
| `GCP_DEPLOY_SERVICE_ACCOUNT` | `github-deploy@my-project.iam.gserviceaccount.com` |
| `ARTIFACT_REGISTRY_REPOSITORY` | `containers` |
| `API_IMAGE_NAME` | `alloydb-crud-api` |
| `CLOUD_RUN_SERVICE` | `alloydb-crud-api` |
| `CLOUD_RUN_SERVICE_ACCOUNT` | `alloydb-crud-api@my-project.iam.gserviceaccount.com` |
| `MIGRATION_JOB` | `alloydb-crud-api-migrations` |
| `MIGRATION_SERVICE_ACCOUNT` | `alloydb-crud-api-migrations@my-project.iam.gserviceaccount.com` |
| `VPC_CONNECTOR` | `projects/my-project/locations/us-central1/connectors/serverless` |
| `DB_CONNECTION_SECRET` | `alloydb-crud-api-connection` |
| `MIGRATION_DB_CONNECTION_SECRET` | `alloydb-crud-api-migration-connection` |
| `CLOUD_RUN_ALLOW_UNAUTHENTICATED` | `false` |

`MIGRATION_DB_CONNECTION_SECRET` is optional. If omitted, the migration workflow uses `DB_CONNECTION_SECRET`.

## IAM Notes

The GitHub deploy service account needs permission to push images, deploy Cloud Run services and jobs, and act as the runtime service accounts. Typical roles:

- `roles/artifactregistry.writer`
- `roles/run.admin`
- `roles/iam.serviceAccountUser` on the Cloud Run runtime and migration service accounts
- `roles/vpcaccess.user` if required by your organization policy

The Cloud Run runtime and migration service accounts need:

- `roles/secretmanager.secretAccessor` on the relevant connection-string secret.

If you enable the optional AlloyDB Admin API endpoint, the runtime service account also needs an AlloyDB viewer role such as `roles/alloydb.viewer`.

## Workflow Order

1. Run `CI` on pull requests and pushes.
2. Run `Migrate Production Database` manually when a deployment includes schema changes.
3. Run `Deploy Production` manually to deploy the API image to Cloud Run.

For breaking schema changes, use expand-and-contract migrations so the currently deployed app and the new app can both run during rollout.

## Local Development

Keep using Docker Compose locally:

```bash
docker compose up -d
```

The Compose file uses AlloyDB Omni and a local development connection string. It is not used for production deployment.
