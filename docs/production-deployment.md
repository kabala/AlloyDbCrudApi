# Production Deployment

Docker Compose is only for local development with AlloyDB Omni. Production uses a real AlloyDB for PostgreSQL instance in Google Cloud.

## Architecture

- GitHub Actions authenticates to Google Cloud with Workload Identity Federation.
- The API image is pushed to Artifact Registry.
- Cloud Run hosts the API container.
- Cloud Run reaches AlloyDB private IP through a Serverless VPC Access connector.
- Secret Manager stores database connection strings.
- EF Core migrations run from a separate Cloud Run Job built from `Dockerfile.migrations`.

The application does not run `Database.Migrate()` in production. Production schema changes are applied by the `Migrate Production Database` workflow.

## Required Google Cloud Resources

Create these before running the workflows:

- AlloyDB cluster and primary instance.
- Database, application user, and migration user.
- VPC with private services access configured for AlloyDB.
- Serverless VPC Access connector in the same region as Cloud Run.
- Artifact Registry Docker repository.
- Secret Manager secret for the application connection string.
- Optional separate Secret Manager secret for the migration connection string.
- Runtime service account for Cloud Run.
- Migration service account for the Cloud Run Job.
- Deploy service account used by GitHub Actions.

Use an AlloyDB private IP address in the connection string:

```text
Host=10.0.0.5;Port=5432;Database=cruddb;Username=app_user;Password=REPLACE_ME;SSL Mode=Disable
```

Store it in Secret Manager:

```bash
printf '%s' 'Host=10.0.0.5;Port=5432;Database=cruddb;Username=app_user;Password=REPLACE_ME;SSL Mode=Disable' \
  | gcloud secrets create alloydb-crud-api-connection --data-file=-
```

For migrations, prefer a separate database user that can apply DDL:

```bash
printf '%s' 'Host=10.0.0.5;Port=5432;Database=cruddb;Username=migration_user;Password=REPLACE_ME;SSL Mode=Disable' \
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
