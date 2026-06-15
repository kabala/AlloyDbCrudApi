locals {
  deploy_service_account_id    = "github-deploy"
  runtime_service_account_id   = "cloudsql-crud-api"
  migration_service_account_id = "cloudsql-crud-migrations"

  deploy_service_account_email    = "${local.deploy_service_account_id}@${var.project_id}.iam.gserviceaccount.com"
  runtime_service_account_email   = "${local.runtime_service_account_id}@${var.project_id}.iam.gserviceaccount.com"
  migration_service_account_email = "${local.migration_service_account_id}@${var.project_id}.iam.gserviceaccount.com"

  workload_identity_pool_id     = "github"
  workload_identity_provider_id = "github-provider"

  api_image = "${var.region}-docker.pkg.dev/${var.project_id}/${var.artifact_registry_repository}/${var.cloud_run_image_name}:${var.cloud_run_image_tag}"
}

data "google_project" "current" {
  project_id = var.project_id
}

resource "random_password" "database" {
  length  = 32
  special = false
}

resource "google_project_service" "required" {
  for_each = toset([
    "artifactregistry.googleapis.com",
    "cloudresourcemanager.googleapis.com",
    "iam.googleapis.com",
    "iamcredentials.googleapis.com",
    "run.googleapis.com",
    "secretmanager.googleapis.com",
    "serviceusage.googleapis.com",
    "sql-component.googleapis.com",
    "sqladmin.googleapis.com",
  ])

  project            = var.project_id
  service            = each.value
  disable_on_destroy = false
}

resource "google_sql_database_instance" "postgres" {
  project          = var.project_id
  name             = var.cloud_sql_instance_name
  region           = var.region
  database_version = var.cloud_sql_database_version

  deletion_protection = var.cloud_sql_deletion_protection

  settings {
    tier = var.cloud_sql_tier
    # Cloud SQL calls the standard edition "ENTERPRISE"; this is not Enterprise Plus.
    # It is required for shared-core tiers such as db-f1-micro.
    edition           = "ENTERPRISE"
    availability_type = "ZONAL"
    disk_type         = var.cloud_sql_disk_type
    disk_size         = var.cloud_sql_disk_size_gb
    disk_autoresize   = true

    ip_configuration {
      ipv4_enabled = true
    }

    backup_configuration {
      enabled                        = var.cloud_sql_backups_enabled
      point_in_time_recovery_enabled = false
      start_time                     = "06:00"
    }

    database_flags {
      name  = "cloudsql.logical_decoding"
      value = "on"
    }
  }

  depends_on = [google_project_service.required]
}

resource "google_sql_database" "app" {
  project  = var.project_id
  instance = google_sql_database_instance.postgres.name
  name     = var.cloud_sql_database_name
}

resource "google_sql_user" "app" {
  project  = var.project_id
  instance = google_sql_database_instance.postgres.name
  name     = var.cloud_sql_user
  password = random_password.database.result
}

resource "google_artifact_registry_repository" "containers" {
  project       = var.project_id
  location      = var.region
  repository_id = var.artifact_registry_repository
  format        = "DOCKER"
  description   = "Application container images"

  depends_on = [google_project_service.required]
}

resource "google_secret_manager_secret" "app_connection" {
  project   = var.project_id
  secret_id = var.db_connection_secret

  replication {
    auto {}
  }

  depends_on = [google_project_service.required]
}

resource "google_secret_manager_secret" "migration_connection" {
  project   = var.project_id
  secret_id = var.migration_db_connection_secret

  replication {
    auto {}
  }

  depends_on = [google_project_service.required]
}

resource "google_secret_manager_secret_version" "app_connection" {
  secret = google_secret_manager_secret.app_connection.id

  secret_data_wo         = "Host=/cloudsql/${google_sql_database_instance.postgres.connection_name};Port=5432;Database=${google_sql_database.app.name};Username=${google_sql_user.app.name};Password=${random_password.database.result}"
  secret_data_wo_version = var.connection_secret_version

  depends_on = [google_sql_user.app]
}

resource "google_secret_manager_secret_version" "migration_connection" {
  secret = google_secret_manager_secret.migration_connection.id

  secret_data_wo         = "Host=/cloudsql/${google_sql_database_instance.postgres.connection_name};Port=5432;Database=${google_sql_database.app.name};Username=${google_sql_user.app.name};Password=${random_password.database.result}"
  secret_data_wo_version = var.connection_secret_version

  depends_on = [google_sql_user.app]
}

resource "google_service_account" "deploy" {
  project      = var.project_id
  account_id   = local.deploy_service_account_id
  display_name = "GitHub Actions deploy service account"

  depends_on = [google_project_service.required]
}

resource "google_service_account" "runtime" {
  project      = var.project_id
  account_id   = local.runtime_service_account_id
  display_name = "Cloud Run API runtime service account"

  depends_on = [google_project_service.required]
}

resource "google_service_account" "migrations" {
  project      = var.project_id
  account_id   = local.migration_service_account_id
  display_name = "Cloud Run migration job service account"

  depends_on = [google_project_service.required]
}

resource "google_project_iam_member" "deploy_project_roles" {
  for_each = toset([
    "roles/artifactregistry.writer",
    "roles/run.admin",
  ])

  project = var.project_id
  role    = each.value
  member  = "serviceAccount:${google_service_account.deploy.email}"
}

resource "google_project_iam_member" "cloud_sql_clients" {
  for_each = {
    runtime    = google_service_account.runtime.email
    migrations = google_service_account.migrations.email
  }

  project = var.project_id
  role    = "roles/cloudsql.client"
  member  = "serviceAccount:${each.value}"
}

resource "google_service_account_iam_member" "deploy_can_act_as_runtime" {
  service_account_id = google_service_account.runtime.name
  role               = "roles/iam.serviceAccountUser"
  member             = "serviceAccount:${google_service_account.deploy.email}"
}

resource "google_service_account_iam_member" "deploy_can_act_as_migrations" {
  service_account_id = google_service_account.migrations.name
  role               = "roles/iam.serviceAccountUser"
  member             = "serviceAccount:${google_service_account.deploy.email}"
}

resource "google_secret_manager_secret_iam_member" "runtime_reads_app_connection" {
  project   = var.project_id
  secret_id = google_secret_manager_secret.app_connection.secret_id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${google_service_account.runtime.email}"
}

resource "google_secret_manager_secret_iam_member" "migrations_reads_migration_connection" {
  project   = var.project_id
  secret_id = google_secret_manager_secret.migration_connection.secret_id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${google_service_account.migrations.email}"
}

resource "google_cloud_run_v2_service" "api" {
  project             = var.project_id
  location            = var.region
  name                = var.cloud_run_service
  deletion_protection = false
  ingress             = "INGRESS_TRAFFIC_ALL"

  template {
    service_account                  = google_service_account.runtime.email
    timeout                          = "300s"
    max_instance_request_concurrency = var.cloud_run_container_concurrency

    scaling {
      min_instance_count = var.cloud_run_min_instances
      max_instance_count = var.cloud_run_max_instances
    }

    volumes {
      name = "cloudsql"

      cloud_sql_instance {
        instances = [google_sql_database_instance.postgres.connection_name]
      }
    }

    containers {
      image = local.api_image

      ports {
        name           = "http1"
        container_port = 8080
      }

      volume_mounts {
        name       = "cloudsql"
        mount_path = "/cloudsql"
      }

      resources {
        limits = {
          cpu    = var.cloud_run_cpu
          memory = var.cloud_run_memory
        }

        cpu_idle          = true
        startup_cpu_boost = true
      }

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Production"
      }

      env {
        name = "ConnectionStrings__DefaultConnection"

        value_source {
          secret_key_ref {
            secret  = google_secret_manager_secret.app_connection.secret_id
            version = "latest"
          }
        }
      }

      dynamic "env" {
        for_each = var.cors_allowed_origins

        content {
          name  = "Cors__AllowedOrigins__${env.key}"
          value = env.value
        }
      }
    }
  }

  lifecycle {
    create_before_destroy = true

    ignore_changes = [
      client,
      client_version,
      template[0].containers[0].image,
      template[0].labels,
      template[0].revision,
      traffic,
    ]
  }

  depends_on = [
    google_project_service.required,
    google_project_iam_member.cloud_sql_clients,
    google_secret_manager_secret_iam_member.runtime_reads_app_connection,
    google_secret_manager_secret_version.app_connection,
  ]
}

resource "google_cloud_run_v2_service_iam_member" "public_invoker" {
  count = var.cloud_run_allow_unauthenticated ? 1 : 0

  project  = var.project_id
  location = google_cloud_run_v2_service.api.location
  name     = google_cloud_run_v2_service.api.name
  role     = "roles/run.invoker"
  member   = "allUsers"
}

resource "google_iam_workload_identity_pool" "github" {
  project                   = var.project_id
  workload_identity_pool_id = local.workload_identity_pool_id
  display_name              = "GitHub Actions"

  depends_on = [google_project_service.required]
}

resource "google_iam_workload_identity_pool_provider" "github" {
  project                            = var.project_id
  workload_identity_pool_id          = google_iam_workload_identity_pool.github.workload_identity_pool_id
  workload_identity_pool_provider_id = local.workload_identity_provider_id
  display_name                       = "GitHub Actions provider"

  attribute_mapping = {
    "google.subject"             = "assertion.sub"
    "attribute.actor"            = "assertion.actor"
    "attribute.repository"       = "assertion.repository"
    "attribute.repository_owner" = "assertion.repository_owner"
  }

  attribute_condition = "attribute.repository=='${var.github_repository_full_name}'"

  oidc {
    issuer_uri = "https://token.actions.githubusercontent.com"
  }
}

resource "google_service_account_iam_member" "github_workload_identity" {
  service_account_id = google_service_account.deploy.name
  role               = "roles/iam.workloadIdentityUser"
  member             = "principalSet://iam.googleapis.com/projects/${data.google_project.current.number}/locations/global/workloadIdentityPools/${google_iam_workload_identity_pool.github.workload_identity_pool_id}/attribute.repository/${var.github_repository_full_name}"
}

resource "github_actions_variable" "production" {
  for_each = {
    GCP_PROJECT_ID                     = var.project_id
    GCP_REGION                         = var.region
    GCP_WORKLOAD_IDENTITY_PROVIDER     = google_iam_workload_identity_pool_provider.github.name
    GCP_DEPLOY_SERVICE_ACCOUNT         = google_service_account.deploy.email
    ARTIFACT_REGISTRY_REPOSITORY       = google_artifact_registry_repository.containers.repository_id
    API_IMAGE_NAME                     = var.cloud_run_image_name
    CLOUD_RUN_SERVICE                  = var.cloud_run_service
    CLOUD_RUN_SERVICE_ACCOUNT          = google_service_account.runtime.email
    CLOUD_SQL_INSTANCE_CONNECTION_NAME = google_sql_database_instance.postgres.connection_name
    MIGRATION_JOB                      = var.migration_job
    MIGRATION_SERVICE_ACCOUNT          = google_service_account.migrations.email
    DB_CONNECTION_SECRET               = google_secret_manager_secret.app_connection.secret_id
    MIGRATION_DB_CONNECTION_SECRET     = google_secret_manager_secret.migration_connection.secret_id
    CLOUD_RUN_ALLOW_UNAUTHENTICATED    = tostring(var.cloud_run_allow_unauthenticated)
  }

  repository    = var.github_repository_name
  variable_name = each.key
  value         = each.value
}
