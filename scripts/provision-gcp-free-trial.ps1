param(
    [string]$ProjectId = $(gcloud config get-value project),
    [string]$Region = $(gcloud config get-value compute/region),
    [string]$Repository = "kabala/AlloyDbCrudApi",
    [string]$ClusterId = "alloydb-crud-trial",
    [string]$InstanceId = "primary",
    [string]$Network = "default",
    [string]$AllocatedRangeName = "google-managed-services-default",
    [string]$VpcConnector = "alloydb-crud-connector",
    [string]$VpcConnectorRange = "10.8.0.0/28",
    [string]$ArtifactRepository = "containers",
    [string]$CloudRunService = "alloydb-crud-api",
    [string]$MigrationJob = "alloydb-crud-api-migrations",
    [string]$DatabaseName = "postgres",
    [string]$DbUser = "postgres",
    [string]$DbConnectionSecret = "alloydb-crud-api-connection",
    [string]$MigrationDbConnectionSecret = "alloydb-crud-api-migration-connection",
    [switch]$AllowUnauthenticated
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectId)) {
    throw "ProjectId is required. Pass -ProjectId or set gcloud config core/project."
}

if ([string]::IsNullOrWhiteSpace($Region)) {
    $Region = "us-central1"
}

function Invoke-Gcloud {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)
    & gcloud @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "gcloud $($Arguments -join ' ') failed with exit code $LASTEXITCODE"
    }
}

function Invoke-Gh {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)
    & gh @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "gh $($Arguments -join ' ') failed with exit code $LASTEXITCODE"
    }
}

function Test-GcloudExists {
    param([string[]]$Arguments)
    & gcloud @Arguments *> $null
    return $LASTEXITCODE -eq 0
}

function New-Password {
    $bytes = [byte[]]::new(24)
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    return [Convert]::ToBase64String($bytes).TrimEnd("=") -replace "[+/]", "A"
}

function Set-SecretValue {
    param(
        [string]$SecretName,
        [string]$Value
    )

    if (-not (Test-GcloudExists @("secrets", "describe", $SecretName, "--project", $ProjectId))) {
        Invoke-Gcloud secrets create $SecretName --project $ProjectId --replication-policy automatic
    }

    $tmp = New-TemporaryFile
    try {
        Set-Content -LiteralPath $tmp.FullName -Value $Value -NoNewline
        Invoke-Gcloud secrets versions add $SecretName --project $ProjectId --data-file $tmp.FullName
    }
    finally {
        Remove-Item -LiteralPath $tmp.FullName -Force -ErrorAction SilentlyContinue
    }
}

$billing = gcloud billing projects describe $ProjectId --format json | ConvertFrom-Json
if (-not $billing.billingEnabled) {
    throw "Billing is not enabled/open for project '$ProjectId'. Enable billing first, then rerun this script."
}

$projectNumber = (gcloud projects describe $ProjectId --format "value(projectNumber)")
$deployServiceAccountId = "github-deploy"
$runtimeServiceAccountId = "alloydb-crud-api"
$migrationServiceAccountId = "alloydb-crud-migrations"
$deployServiceAccount = "$deployServiceAccountId@$ProjectId.iam.gserviceaccount.com"
$runtimeServiceAccount = "$runtimeServiceAccountId@$ProjectId.iam.gserviceaccount.com"
$migrationServiceAccount = "$migrationServiceAccountId@$ProjectId.iam.gserviceaccount.com"
$workloadIdentityPool = "github"
$workloadIdentityProvider = "github-provider"
$workloadIdentityProviderName = "projects/$projectNumber/locations/global/workloadIdentityPools/$workloadIdentityPool/providers/$workloadIdentityProvider"

Invoke-Gcloud services enable `
    alloydb.googleapis.com `
    artifactregistry.googleapis.com `
    cloudresourcemanager.googleapis.com `
    compute.googleapis.com `
    iam.googleapis.com `
    iamcredentials.googleapis.com `
    run.googleapis.com `
    secretmanager.googleapis.com `
    servicenetworking.googleapis.com `
    serviceusage.googleapis.com `
    vpcaccess.googleapis.com `
    --project $ProjectId

if (-not (Test-GcloudExists @("compute", "networks", "describe", $Network, "--project", $ProjectId))) {
    Invoke-Gcloud compute networks create $Network --project $ProjectId --subnet-mode auto
}

if (-not (Test-GcloudExists @("compute", "addresses", "describe", $AllocatedRangeName, "--global", "--project", $ProjectId))) {
    Invoke-Gcloud compute addresses create $AllocatedRangeName `
        --project $ProjectId `
        --global `
        --purpose VPC_PEERING `
        --prefix-length 16 `
        --network $Network
}

$peeringExists = (gcloud services vpc-peerings list --project $ProjectId --network $Network --format "value(peering)") -contains "servicenetworking-googleapis-com"
if (-not $peeringExists) {
    Invoke-Gcloud services vpc-peerings connect `
        --project $ProjectId `
        --service servicenetworking.googleapis.com `
        --ranges $AllocatedRangeName `
        --network $Network
}

$dbPassword = New-Password

if (-not (Test-GcloudExists @("alloydb", "clusters", "describe", $ClusterId, "--region", $Region, "--project", $ProjectId))) {
    Invoke-Gcloud alloydb clusters create $ClusterId `
        --project $ProjectId `
        --region $Region `
        --network $Network `
        --password $dbPassword `
        --subscription-type TRIAL
}
else {
    Write-Host "AlloyDB cluster '$ClusterId' already exists. Keeping the existing postgres password."
    $dbPassword = Read-Host "Enter the existing AlloyDB postgres password for secret creation"
}

if (-not (Test-GcloudExists @("alloydb", "instances", "describe", $InstanceId, "--cluster", $ClusterId, "--region", $Region, "--project", $ProjectId))) {
    Invoke-Gcloud alloydb instances create $InstanceId `
        --project $ProjectId `
        --region $Region `
        --cluster $ClusterId `
        --instance-type PRIMARY `
        --cpu-count 8
}

$instance = gcloud alloydb instances describe $InstanceId --cluster $ClusterId --region $Region --project $ProjectId --format json | ConvertFrom-Json
$privateIp = $instance.ipAddress
if ([string]::IsNullOrWhiteSpace($privateIp)) {
    $privateIp = $instance.ipAddresses[0]
}
if ([string]::IsNullOrWhiteSpace($privateIp)) {
    throw "Could not read the AlloyDB private IP address from the instance description."
}

$connectionString = "Host=$privateIp;Port=5432;Database=$DatabaseName;Username=$DbUser;Password=$dbPassword;SSL Mode=Disable"
Set-SecretValue -SecretName $DbConnectionSecret -Value $connectionString
Set-SecretValue -SecretName $MigrationDbConnectionSecret -Value $connectionString

foreach ($sa in @($deployServiceAccountId, $runtimeServiceAccountId, $migrationServiceAccountId)) {
    $email = "$sa@$ProjectId.iam.gserviceaccount.com"
    if (-not (Test-GcloudExists @("iam", "service-accounts", "describe", $email, "--project", $ProjectId))) {
        Invoke-Gcloud iam service-accounts create $sa --project $ProjectId --display-name $sa
    }
}

foreach ($role in @("roles/artifactregistry.writer", "roles/run.admin", "roles/vpcaccess.user")) {
    Invoke-Gcloud projects add-iam-policy-binding $ProjectId `
        --member "serviceAccount:$deployServiceAccount" `
        --role $role `
        --condition=None `
        --quiet
}

foreach ($target in @($runtimeServiceAccount, $migrationServiceAccount)) {
    Invoke-Gcloud iam service-accounts add-iam-policy-binding $target `
        --project $ProjectId `
        --member "serviceAccount:$deployServiceAccount" `
        --role roles/iam.serviceAccountUser
}

foreach ($binding in @(
    @{ Secret = $DbConnectionSecret; Account = $runtimeServiceAccount },
    @{ Secret = $MigrationDbConnectionSecret; Account = $migrationServiceAccount }
)) {
    Invoke-Gcloud secrets add-iam-policy-binding $binding.Secret `
        --project $ProjectId `
        --member "serviceAccount:$($binding.Account)" `
        --role roles/secretmanager.secretAccessor
}

if (-not (Test-GcloudExists @("artifacts", "repositories", "describe", $ArtifactRepository, "--location", $Region, "--project", $ProjectId))) {
    Invoke-Gcloud artifacts repositories create $ArtifactRepository `
        --project $ProjectId `
        --location $Region `
        --repository-format docker `
        --description "Application container images"
}

if (-not (Test-GcloudExists @("compute", "networks", "vpc-access", "connectors", "describe", $VpcConnector, "--region", $Region, "--project", $ProjectId))) {
    Invoke-Gcloud compute networks vpc-access connectors create $VpcConnector `
        --project $ProjectId `
        --region $Region `
        --network $Network `
        --range $VpcConnectorRange
}

if (-not (Test-GcloudExists @("iam", "workload-identity-pools", "describe", $workloadIdentityPool, "--location", "global", "--project", $ProjectId))) {
    Invoke-Gcloud iam workload-identity-pools create $workloadIdentityPool `
        --project $ProjectId `
        --location global `
        --display-name "GitHub Actions"
}

if (-not (Test-GcloudExists @("iam", "workload-identity-pools", "providers", "describe", $workloadIdentityProvider, "--workload-identity-pool", $workloadIdentityPool, "--location", "global", "--project", $ProjectId))) {
    Invoke-Gcloud iam workload-identity-pools providers create-oidc $workloadIdentityProvider `
        --project $ProjectId `
        --location global `
        --workload-identity-pool $workloadIdentityPool `
        --display-name "GitHub Actions provider" `
        --issuer-uri "https://token.actions.githubusercontent.com" `
        --attribute-mapping "google.subject=assertion.sub,attribute.actor=assertion.actor,attribute.repository=assertion.repository,attribute.repository_owner=assertion.repository_owner" `
        --attribute-condition "attribute.repository=='$Repository'"
}

Invoke-Gcloud iam service-accounts add-iam-policy-binding $deployServiceAccount `
    --project $ProjectId `
    --role roles/iam.workloadIdentityUser `
    --member "principalSet://iam.googleapis.com/projects/$projectNumber/locations/global/workloadIdentityPools/$workloadIdentityPool/attribute.repository/$Repository"

$connectorPath = "projects/$ProjectId/locations/$Region/connectors/$VpcConnector"
$allowUnauthenticatedValue = if ($AllowUnauthenticated) { "true" } else { "false" }

$variables = @{
    GCP_PROJECT_ID = $ProjectId
    GCP_REGION = $Region
    GCP_WORKLOAD_IDENTITY_PROVIDER = $workloadIdentityProviderName
    GCP_DEPLOY_SERVICE_ACCOUNT = $deployServiceAccount
    ARTIFACT_REGISTRY_REPOSITORY = $ArtifactRepository
    CLOUD_RUN_SERVICE = $CloudRunService
    CLOUD_RUN_SERVICE_ACCOUNT = $runtimeServiceAccount
    MIGRATION_JOB = $MigrationJob
    MIGRATION_SERVICE_ACCOUNT = $migrationServiceAccount
    VPC_CONNECTOR = $connectorPath
    DB_CONNECTION_SECRET = $DbConnectionSecret
    MIGRATION_DB_CONNECTION_SECRET = $MigrationDbConnectionSecret
    CLOUD_RUN_ALLOW_UNAUTHENTICATED = $allowUnauthenticatedValue
}

foreach ($entry in $variables.GetEnumerator()) {
    Invoke-Gh variable set $entry.Key --repo $Repository --body $entry.Value
}

Write-Host ""
Write-Host "Provisioning completed."
Write-Host "AlloyDB cluster: $ClusterId"
Write-Host "AlloyDB primary instance: $InstanceId"
Write-Host "AlloyDB private IP: $privateIp"
Write-Host "Database in connection string: $DatabaseName"
Write-Host "GitHub repository variables updated for $Repository"
Write-Host ""
Write-Host "Next steps:"
Write-Host "1. Run the 'Migrate Production Database' GitHub workflow."
Write-Host "2. Run the 'Deploy Production' GitHub workflow."
