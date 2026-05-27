param(
    [Parameter(Mandatory = $true)]
    [string]$Password,

    [string]$ConnectionString,

    [switch]$ResetPasswords,

    [switch]$ApplyMigrations,

    [switch]$SeedSampleProducts,

    [switch]$SeedSellerFlowDemo
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptRoot "..")
$projectPath = Join-Path $scriptRoot "Swyftly.DevSeed\Swyftly.DevSeed.csproj"

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $ConnectionString = $env:ConnectionStrings__DefaultConnection
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $appSettingsPath = Join-Path $repoRoot "backend\src\Swyftly.Api\appsettings.Development.json"
    if (Test-Path $appSettingsPath) {
        $settings = Get-Content $appSettingsPath -Raw | ConvertFrom-Json
        $ConnectionString = $settings.ConnectionStrings.DefaultConnection
    }
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    throw "No connection string was provided. Pass -ConnectionString or set ConnectionStrings__DefaultConnection."
}

$seedArgs = @(
    "--connection", $ConnectionString,
    "--password", $Password
)

if ($ResetPasswords) {
    $seedArgs += "--reset-passwords"
}

if ($ApplyMigrations) {
    $seedArgs += "--apply-migrations"
}

if ($SeedSampleProducts) {
    $seedArgs += "--seed-sample-products"
}

if ($SeedSellerFlowDemo) {
    $seedArgs += "--seed-seller-flow-demo"
}

$assetsPath = Join-Path $scriptRoot "Swyftly.DevSeed\obj\project.assets.json"
if (Test-Path $assetsPath) {
    dotnet run --no-restore --project $projectPath -- @seedArgs
}
else {
    dotnet run --project $projectPath -- @seedArgs
}

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
