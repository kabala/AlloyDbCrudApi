# Frontend Repository Integration

This API can be consumed by a browser-only SPA deployed from a separate repository. In that setup the frontend is public, the API is also publicly invokable, and browser access is limited with CORS to the frontend origin.

## Ownership Split

API repo owns:

- Cloud SQL and database connection secrets.
- API Cloud Run service.
- API Artifact Registry image repository.
- API GitHub Actions deploy identity.
- API public/private invoker setting.
- CORS allowlist for frontend browser origins.

Frontend repo owns:

- Frontend Cloud Run service or static hosting service.
- Frontend Artifact Registry image repository if it deploys a container.
- Frontend GitHub Actions deploy identity.
- Frontend public URL.
- Build-time API base URL used by the SPA.

## Values The Frontend Repo Needs

Use these values in the frontend repo OpenTofu variables, GitHub Actions variables, or frontend build environment:

| Name | Current value | Purpose |
|---|---|---|
| `GCP_PROJECT_ID` | `personal-434212` | Google Cloud project where frontend deploys. |
| `GCP_REGION` | `us-east1` | Region to colocate frontend near the API. |
| `API_BASE_URL` | `https://cloudsql-crud-api-REPLACE.run.app` | Base URL for browser requests from the SPA. |
| `API_ITEMS_URL` | `https://cloudsql-crud-api-REPLACE.run.app/api/items` | Existing CRUD items endpoint. |

For common SPA frameworks, pass the API URL as a build-time environment variable:

```bash
# Vite
VITE_API_BASE_URL=https://cloudsql-crud-api-REPLACE.run.app

# Create React App
REACT_APP_API_BASE_URL=https://cloudsql-crud-api-REPLACE.run.app

# Next.js browser-exposed value
NEXT_PUBLIC_API_BASE_URL=https://cloudsql-crud-api-REPLACE.run.app
```

## Values This API Repo Needs Back

The frontend Cloud Run service has a stable public origin:

```text
https://your-frontend-url.run.app
```

This API repo stores that origin directly in OpenTofu defaults:

```hcl
cloud_run_allow_unauthenticated = true
cors_allowed_origins = [
  "https://your-frontend-url.run.app"
]
```

The origin must include scheme and host, with no path:

```text
https://your-frontend-url.run.app
```

Do not use:

```text
https://your-frontend-url.run.app/
https://your-frontend-url.run.app/app
*
```

Then apply this repo's API infrastructure:

```powershell
cd infra/opentofu
tofu plan
tofu apply
```

## Frontend OpenTofu Shape

A separate frontend repo can use its own OpenTofu stack. For a Cloud Run hosted SPA container, the important pieces are:

```hcl
variable "project_id" {
  type    = string
  default = "personal-434212"
}

variable "region" {
  type    = string
  default = "us-east1"
}

variable "api_base_url" {
  type    = string
  default = "https://cloudsql-crud-api-REPLACE.run.app"
}

variable "frontend_service_name" {
  type    = string
  default = "cloudsql-crud-frontend"
}
```

The frontend deployment workflow should pass the API URL into the SPA build, not discover it at runtime from this API repo:

```bash
docker build \
  --build-arg VITE_API_BASE_URL="$API_BASE_URL" \
  --tag "$FRONTEND_IMAGE" .
```

If the frontend framework reads runtime environment variables server-side, set the equivalent env var on the frontend Cloud Run service instead.

## CORS Behavior

This API reads CORS origins from:

```text
Cors:AllowedOrigins
```

OpenTofu writes those values to Cloud Run as:

```text
Cors__AllowedOrigins__0=https://your-frontend-url.run.app
Cors__AllowedOrigins__1=https://another-allowed-origin.run.app
```

The API allows:

- Any HTTP method.
- Any request header.
- Only configured origins.

CORS only controls browser cross-origin access. It does not authenticate users and does not block non-browser clients. Add application-level auth before exposing sensitive data or mutation operations.

## Deployment Order

1. Deploy the API infrastructure and API service.
2. Create the frontend repo infrastructure.
3. Deploy the frontend and get its public URL.
4. Confirm that this repo's `cors_allowed_origins` still matches the frontend origin.
5. Apply this repo's OpenTofu config.
6. Redeploy the API only if the CORS code has not already been deployed.
