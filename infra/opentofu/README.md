# OpenTofu Production Infrastructure

This directory owns the Google Cloud production architecture for the app. Docker Compose remains local-only.

## What It Creates

- Required Google APIs.
- Private services access on the selected VPC.
- AlloyDB free trial cluster with an 8 vCPU primary instance.
- Serverless VPC Access connector for Cloud Run to reach AlloyDB private IP.
- Artifact Registry Docker repository.
- AlloyDB built-in `app_user` database user.
- Secret Manager connection strings for the app and migration job.
- Cloud Run API service configured with private AlloyDB access.
- Cloud Run runtime and migration service accounts.
- GitHub deploy service account.
- Workload Identity Federation for GitHub Actions.
- GitHub Actions repository variables consumed by the deployment workflows.

## Prerequisites

- Billing enabled on the Google Cloud project.
- OpenTofu installed locally or in CI.
- `gcloud auth application-default login` completed for local use.
- `gh auth login` completed if managing GitHub repository variables from local OpenTofu.
- The selected VPC network name is available. Defaults create `alloydb-crud-vpc`.

## Usage

```powershell
cd infra/opentofu
Copy-Item terraform.tfvars.example terraform.tfvars
# Edit terraform.tfvars and set alloydb_postgres_password.
tofu init
tofu plan
tofu apply
```

After `tofu apply` completes:

1. Run the `Migrate Production Database` GitHub workflow.
2. Run the `Deploy Production` GitHub workflow.

OpenTofu owns the Cloud Run service shape: service account, VPC connector, secrets, scaling, CPU, and memory. The deploy workflow builds the .NET API image, pushes it to Artifact Registry, and updates the service image to create a new Cloud Run revision.

## State And Secrets

The AlloyDB built-in user password and generated connection-string secret values are passed with the Google provider's write-only fields where supported. Increment `alloydb_postgres_password_version` and `connection_secret_version` when rotating those values. Still treat the OpenTofu working directory and plan output as sensitive, and use encrypted remote state with restricted access for any shared environment.

This repo currently uses local OpenTofu state. The local `terraform.tfstate` file is ignored by Git and must not be deleted while these resources are managed from this workstation. Before moving management to CI or another machine, migrate this state to an encrypted remote backend such as a locked-down Google Cloud Storage bucket.

## Database Name

This configuration uses the default `postgres` database by default. Creating a separate database inside AlloyDB is a SQL operation after the instance exists, so it is intentionally outside OpenTofu. If you create another database later, set `alloydb_database_name` and re-apply to update the connection-string secrets.
