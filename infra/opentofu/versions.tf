terraform {
  required_version = ">= 1.8.0"

  required_providers {
    google = {
      source  = "hashicorp/google"
      version = "~> 7.4"
    }

    github = {
      source  = "integrations/github"
      version = "~> 6.6"
    }
  }
}
