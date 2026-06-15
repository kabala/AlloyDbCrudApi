variable "project_id" {
  description = "Google Cloud project ID."
  type        = string
  default     = "personal-434212"
}

variable "region" {
  description = "Google Cloud region for AlloyDB, Cloud Run, Artifact Registry, and VPC connector resources."
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

variable "network_name" {
  description = "VPC network name created for AlloyDB and the Serverless VPC Access connector."
  type        = string
  default     = "alloydb-crud-vpc"
}

variable "allocated_ip_range_name" {
  description = "Name of the private services access allocated range."
  type        = string
  default     = "google-managed-services-default"
}

variable "private_services_prefix_length" {
  description = "Prefix length for the private services access range."
  type        = number
  default     = 16
}

variable "vpc_connector_name" {
  description = "Serverless VPC Access connector name."
  type        = string
  default     = "alloydb-crud-connector"
}

variable "vpc_connector_cidr_range" {
  description = "CIDR range for the Serverless VPC Access connector."
  type        = string
  default     = "10.8.0.0/28"
}

variable "vpc_connector_min_instances" {
  description = "Minimum number of Serverless VPC Access connector instances."
  type        = number
  default     = 2
}

variable "vpc_connector_max_instances" {
  description = "Maximum number of Serverless VPC Access connector instances."
  type        = number
  default     = 3
}

variable "artifact_registry_repository" {
  description = "Artifact Registry Docker repository name."
  type        = string
  default     = "containers"
}

variable "cloud_run_service" {
  description = "Cloud Run service name for the API."
  type        = string
  default     = "alloydb-crud-api"
}

variable "cloud_run_image_name" {
  description = "Artifact Registry image name for the API container."
  type        = string
  default     = "alloydb-crud-api"
}

variable "cloud_run_image_tag" {
  description = "Bootstrap API image tag used when OpenTofu creates the Cloud Run service. GitHub Actions updates the image on each deployment."
  type        = string
  default     = "eb44393dac2180dd0d26edfd33b038e2321427d5"
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

variable "migration_job" {
  description = "Cloud Run Job name used by the migration workflow."
  type        = string
  default     = "alloydb-crud-api-migrations"
}

variable "alloydb_cluster_id" {
  description = "AlloyDB free trial cluster ID."
  type        = string
  default     = "alloydb-crud-trial"
}

variable "alloydb_primary_instance_id" {
  description = "AlloyDB primary instance ID."
  type        = string
  default     = "primary"
}

variable "alloydb_database_name" {
  description = "Database name used by the application connection string. Free-trial provisioning uses the default postgres database unless you create another database separately."
  type        = string
  default     = "postgres"
}

variable "alloydb_user" {
  description = "Application AlloyDB built-in database user."
  type        = string
  default     = "app_user"
}

variable "alloydb_postgres_password" {
  description = "Initial AlloyDB postgres password, passed to the Google provider through write-only fields."
  type        = string
  sensitive   = true
}

variable "alloydb_postgres_password_version" {
  description = "Increment this when rotating the write-only AlloyDB built-in user password."
  type        = number
  default     = 4
}

variable "db_connection_secret" {
  description = "Secret Manager secret name for the application database connection string."
  type        = string
  default     = "alloydb-crud-api-connection"
}

variable "migration_db_connection_secret" {
  description = "Secret Manager secret name for the migration database connection string."
  type        = string
  default     = "alloydb-crud-api-migration-connection"
}

variable "connection_secret_version" {
  description = "Increment this when rotating the write-only Secret Manager connection string versions."
  type        = number
  default     = 5
}

variable "cloud_run_allow_unauthenticated" {
  description = "Whether the deploy workflow should deploy Cloud Run with public unauthenticated access."
  type        = bool
  default     = true
}

variable "cors_allowed_origins" {
  description = "Exact browser origins allowed to call the API through CORS, for example the public frontend URL."
  type        = list(string)
  default     = ["https://alloydb-crud-frontend-dmkxnmuy3q-ue.a.run.app"]
}
