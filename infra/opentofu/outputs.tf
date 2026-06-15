output "cloud_sql_instance_name" {
  description = "Cloud SQL PostgreSQL instance name."
  value       = google_sql_database_instance.postgres.name
}

output "cloud_sql_connection_name" {
  description = "Cloud SQL instance connection name used by Cloud Run."
  value       = google_sql_database_instance.postgres.connection_name
}

output "cloud_sql_database_name" {
  description = "Application database name."
  value       = google_sql_database.app.name
}

output "cloud_sql_app_user" {
  description = "Cloud SQL PostgreSQL user used by app and migration connection strings."
  value       = google_sql_user.app.name
}

output "artifact_registry_repository" {
  description = "Artifact Registry repository ID."
  value       = google_artifact_registry_repository.containers.repository_id
}

output "cloud_run_service_url" {
  description = "Cloud Run API service URL."
  value       = google_cloud_run_v2_service.api.uri
}

output "workload_identity_provider" {
  description = "Workload Identity Provider name used by GitHub Actions."
  value       = google_iam_workload_identity_pool_provider.github.name
}

output "deploy_service_account" {
  description = "GitHub Actions deploy service account email."
  value       = google_service_account.deploy.email
}
