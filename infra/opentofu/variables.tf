variable "project_id" {
  description = "Google Cloud project ID."
  type        = string
  default     = "personal-434212"
}

variable "region" {
  description = "Google Cloud region for Cloud SQL, Cloud Run, and Artifact Registry resources."
  type        = string
  default     = "us-east1"
}

variable "github_owner" {
  description = "GitHub repository owner."
  type        = string
  default     = "kabala"
}

variable "github_repository_name" {
  description = "GitHub repository name, without owner."
  type        = string
  default     = "AlloyDbCrudApi"
}

variable "github_repository_full_name" {
  description = "GitHub repository in owner/name form for Workload Identity Federation conditions."
  type        = string
  default     = "kabala/AlloyDbCrudApi"
}

variable "artifact_registry_repository" {
  description = "Artifact Registry Docker repository name."
  type        = string
  default     = "containers"
}

variable "cloud_run_service" {
  description = "Cloud Run service name for the API."
  type        = string
  default     = "cloudsql-crud-api"
}

variable "cloud_run_image_name" {
  description = "Artifact Registry image name for the API container."
  type        = string
  default     = "cloudsql-crud-api"
}

variable "cloud_run_image_tag" {
  description = "Bootstrap API image tag used when OpenTofu creates the Cloud Run service. GitHub Actions updates the image on each deployment."
  type        = string
  default     = "bootstrap"
}

variable "cloud_run_cpu" {
  description = "CPU limit for the API Cloud Run container."
  type        = string
  default     = "1000m"
}

variable "cloud_run_memory" {
  description = "Memory limit for the API Cloud Run container."
  type        = string
  default     = "512Mi"
}

variable "cloud_run_min_instances" {
  description = "Minimum API Cloud Run instances. Keep zero for pay-per-use local/demo production."
  type        = number
  default     = 0
}

variable "cloud_run_max_instances" {
  description = "Maximum API Cloud Run instances."
  type        = number
  default     = 20
}

variable "cloud_run_container_concurrency" {
  description = "Maximum concurrent requests per API Cloud Run instance."
  type        = number
  default     = 80
}

variable "cloud_run_allow_unauthenticated" {
  description = "Whether the API Cloud Run service should allow public unauthenticated browser access."
  type        = bool
  default     = true
}

variable "cors_allowed_origins" {
  description = "Exact browser origins allowed to call the API through CORS, for example the public frontend URL."
  type        = list(string)
  default     = []
}

variable "migration_job" {
  description = "Cloud Run Job name used by the migration workflow."
  type        = string
  default     = "cloudsql-crud-api-migrations"
}

variable "cloud_sql_instance_name" {
  description = "Cloud SQL for PostgreSQL instance name."
  type        = string
  default     = "crud-postgres"
}

variable "cloud_sql_database_version" {
  description = "Cloud SQL PostgreSQL engine version."
  type        = string
  default     = "POSTGRES_16"
}

variable "cloud_sql_tier" {
  description = "Cloud SQL machine tier. db-f1-micro is the low-cost shared-core option for small demos."
  type        = string
  default     = "db-f1-micro"
}

variable "cloud_sql_disk_type" {
  description = "Cloud SQL disk type."
  type        = string
  default     = "PD_HDD"
}

variable "cloud_sql_disk_size_gb" {
  description = "Cloud SQL storage size in GB."
  type        = number
  default     = 10
}

variable "cloud_sql_deletion_protection" {
  description = "Whether Cloud SQL deletion protection is enabled."
  type        = bool
  default     = false
}

variable "cloud_sql_backups_enabled" {
  description = "Whether Cloud SQL automated backups are enabled. Keep false for the cheapest demo footprint."
  type        = bool
  default     = false
}

variable "cloud_sql_database_name" {
  description = "Application database name."
  type        = string
  default     = "cruddb"
}

variable "cloud_sql_user" {
  description = "Application Cloud SQL PostgreSQL user."
  type        = string
  default     = "app_user"
}

variable "db_connection_secret" {
  description = "Secret Manager secret name for the application database connection string."
  type        = string
  default     = "cloudsql-crud-api-connection"
}

variable "migration_db_connection_secret" {
  description = "Secret Manager secret name for the migration database connection string."
  type        = string
  default     = "cloudsql-crud-api-migration-connection"
}

variable "connection_secret_version" {
  description = "Increment this when rotating the write-only Secret Manager connection string versions."
  type        = number
  default     = 6
}
