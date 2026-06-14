locals {
  deploy_service_account_id    = "github-deploy"
  runtime_service_account_id   = "alloydb-crud-api"
  migration_service_account_id = "alloydb-crud-migrations"

  deploy_service_account_email    = "${local.deploy_service_account_id}@${var.project_id}.iam.gserviceaccount.com"
  runtime_service_account_email   = "${local.runtime_service_account_id}@${var.project_id}.iam.gserviceaccount.com"
  migration_service_account_email = "${local.migration_service_account_id}@${var.project_id}.iam.gserviceaccount.com"

  workload_identity_pool_id     = "github"
  workload_identity_provider_id = "github-provider"

  vpc_connector_path = "projects/${var.project_id}/locations/${var.region}/connectors/${var.vpc_connector_name}"
  api_image          = "${var.region}-docker.pkg.dev/${var.project_id}/${var.artifact_registry_repository}/${var.cloud_run_image_name}:${var.cloud_run_image_tag}"
}

data "google_project" "current" {
  project_id = var.project_id
}

resource "google_project_service" "required" {
  for_each = toset([
    "alloydb.googleapis.com",
    "artifactregistry.googleapis.com",
    "cloudresourcemanager.googleapis.com",
    "compute.googleapis.com",
    "iam.googleapis.com",
    "iamcredentials.googleapis.com",
    "run.googleapis.com",
    "secretmanager.googleapis.com",
    "servicenetworking.googleapis.com",
    "serviceusage.googleapis.com",
    "vpcaccess.googleapis.com",
  ])

  project            = var.project_id
  service            = each.value
  disable_on_destroy = false
}

resource "google_compute_network" "app" {
  project                 = var.project_id
  name                    = var.network_name
  auto_create_subnetworks = true

  depends_on = [google_project_service.required]
}

resource "google_compute_global_address" "private_services" {
  project       = var.project_id
  name          = var.allocated_ip_range_name
  purpose       = "VPC_PEERING"
  address_type  = "INTERNAL"
  prefix_length = var.private_services_prefix_length
  network       = google_compute_network.app.id

  depends_on = [google_project_service.required]
}

resource "google_service_networking_connection" "private_services" {
  network                 = google_compute_network.app.id
  service                 = "servicenetworking.googleapis.com"
  reserved_peering_ranges = [google_compute_global_address.private_services.name]

  depends_on = [google_project_service.required]
}

resource "google_alloydb_cluster" "main" {
  project             = var.project_id
  cluster_id          = var.alloydb_cluster_id
  location            = var.region
  database_version    = "POSTGRES_17"
  subscription_type   = "TRIAL"
  deletion_protection = true

  network_config {
    network            = google_compute_network.app.id
    allocated_ip_range = google_compute_global_address.private_services.name
  }

  initial_user {
    user                = "postgres"
    password_wo         = var.alloydb_postgres_password
    password_wo_version = var.alloydb_postgres_password_version
  }

  depends_on = [google_service_networking_connection.private_services]
}

resource "google_alloydb_instance" "primary" {
  cluster       = google_alloydb_cluster.main.name
  instance_id   = var.alloydb_primary_instance_id
  instance_type = "PRIMARY"
  display_name  = var.alloydb_primary_instance_id

  machine_config {
    cpu_count = 8
  }
}

resource "google_alloydb_user" "app" {
  cluster   = google_alloydb_cluster.main.name
  user_id   = var.alloydb_user
  user_type = "ALLOYDB_BUILT_IN"

  password_wo         = var.alloydb_postgres_password
  password_wo_version = var.alloydb_postgres_password_version
  database_roles      = ["alloydbsuperuser"]

  depends_on = [google_alloydb_instance.primary]
}

resource "google_artifact_registry_repository" "containers" {
  project       = var.project_id
  location      = var.region
  repository_id = var.artifact_registry_repository
  format        = "DOCKER"
  description   = "Application container images"

  depends_on = [google_project_service.required]
}

resource "google_vpc_access_connector" "cloud_run" {
  project       = var.project_id
  region        = var.region
  name          = var.vpc_connector_name
  network       = google_compute_network.app.name
  ip_cidr_range = var.vpc_connector_cidr_range
  min_instances = var.vpc_connector_min_instances
  max_instances = var.vpc_connector_max_instances

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

  secret_data_wo         = "Host=${google_alloydb_instance.primary.ip_address};Port=5432;Database=${var.alloydb_database_name};Username=${var.alloydb_user};Password=${var.alloydb_postgres_password};SSL Mode=Require;Trust Server Certificate=true"
  secret_data_wo_version = var.connection_secret_version

  depends_on = [google_alloydb_user.app]
}

resource "google_secret_manager_secret_version" "migration_connection" {
  secret = google_secret_manager_secret.migration_connection.id

  secret_data_wo         = "Host=${google_alloydb_instance.primary.ip_address};Port=5432;Database=${var.alloydb_database_name};Username=${var.alloydb_user};Password=${var.alloydb_postgres_password};SSL Mode=Require;Trust Server Certificate=true"
  secret_data_wo_version = var.connection_secret_version

  depends_on = [google_alloydb_user.app]
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
    "roles/vpcaccess.user",
  ])

  project = var.project_id
  role    = each.value
  member  = "serviceAccount:${google_service_account.deploy.email}"
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
  deletion_protection = true
  ingress             = "INGRESS_TRAFFIC_ALL"

  template {
    service_account                  = google_service_account.runtime.email
    timeout                          = "300s"
    max_instance_request_concurrency = var.cloud_run_container_concurrency

    scaling {
      min_instance_count = var.cloud_run_min_instances
      max_instance_count = var.cloud_run_max_instances
    }

    vpc_access {
      connector = google_vpc_access_connector.cloud_run.id
      egress    = "PRIVATE_RANGES_ONLY"
    }

    containers {
      image = local.api_image

      ports {
        name           = "http1"
        container_port = 8080
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
        name  = "AlloyDbAdmin__Enabled"
        value = "false"
      }

      env {
        name  = "AlloyDbAdmin__ProjectId"
        value = var.project_id
      }

      env {
        name  = "AlloyDbAdmin__Location"
        value = var.region
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
    google_secret_manager_secret_iam_member.runtime_reads_app_connection,
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
    GCP_PROJECT_ID                  = var.project_id
    GCP_REGION                      = var.region
    GCP_WORKLOAD_IDENTITY_PROVIDER  = google_iam_workload_identity_pool_provider.github.name
    GCP_DEPLOY_SERVICE_ACCOUNT      = google_service_account.deploy.email
    ARTIFACT_REGISTRY_REPOSITORY    = google_artifact_registry_repository.containers.repository_id
    API_IMAGE_NAME                  = var.cloud_run_image_name
    CLOUD_RUN_SERVICE               = var.cloud_run_service
    CLOUD_RUN_SERVICE_ACCOUNT       = google_service_account.runtime.email
    MIGRATION_JOB                   = var.migration_job
    MIGRATION_SERVICE_ACCOUNT       = google_service_account.migrations.email
    VPC_CONNECTOR                   = local.vpc_connector_path
    DB_CONNECTION_SECRET            = google_secret_manager_secret.app_connection.secret_id
    MIGRATION_DB_CONNECTION_SECRET  = google_secret_manager_secret.migration_connection.secret_id
    CLOUD_RUN_ALLOW_UNAUTHENTICATED = tostring(var.cloud_run_allow_unauthenticated)
  }

  repository    = var.github_repository_name
  variable_name = each.key
  value         = each.value
}
