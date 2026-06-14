output "alloydb_cluster_name" {
  description = "Full AlloyDB cluster resource name."
  value       = google_alloydb_cluster.main.name
}

output "alloydb_primary_instance_name" {
  description = "Full AlloyDB primary instance resource name."
  value       = google_alloydb_instance.primary.name
}

output "alloydb_private_ip" {
  description = "Private IP address for the AlloyDB primary instance."
  value       = google_alloydb_instance.primary.ip_address
}

output "alloydb_app_user" {
  description = "AlloyDB built-in database user used by app and migration connection strings."
  value       = google_alloydb_user.app.user_id
}

output "artifact_registry_repository" {
  description = "Artifact Registry repository ID."
  value       = google_artifact_registry_repository.containers.repository_id
}

output "cloud_run_service_url" {
  description = "Cloud Run API service URL."
  value       = google_cloud_run_v2_service.api.uri
}

output "vpc_connector" {
  description = "Serverless VPC Access connector path used by GitHub Actions."
  value       = local.vpc_connector_path
}

output "workload_identity_provider" {
  description = "Workload Identity Provider name used by GitHub Actions."
  value       = google_iam_workload_identity_pool_provider.github.name
}

output "deploy_service_account" {
  description = "GitHub Actions deploy service account email."
  value       = google_service_account.deploy.email
}
