param(
    [string]$ConnectionString,

    [switch]$SkipNetworkCheck
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptRoot "..")
$checks = New-Object System.Collections.Generic.List[object]

function Add-Check {
    param(
        [string]$Name,
        [string]$Status,
        [string]$Message,
        [bool]$Critical = $false
    )

    $checks.Add([pscustomobject]@{
        Name = $Name
        Status = $Status
        Critical = $Critical
        Message = $Message
    }) | Out-Null
}

function Test-CommandAvailable {
    param([string]$Name)

    return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

function Test-TcpPort {
    param(
        [string]$HostName,
        [int]$Port,
        [int]$TimeoutMilliseconds = 2500
    )

    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $async = $client.BeginConnect($HostName, $Port, $null, $null)
        if (-not $async.AsyncWaitHandle.WaitOne($TimeoutMilliseconds, $false)) {
            return $false
        }

        $client.EndConnect($async)
        return $true
    }
    catch {
        return $false
    }
    finally {
        $client.Close()
    }
}

function Get-ConnectionValue {
    param(
        [string]$Connection,
        [string[]]$Keys
    )

    foreach ($part in ($Connection -split ";")) {
        $separator = $part.IndexOf("=")
        if ($separator -le 0) {
            continue
        }

        $key = $part.Substring(0, $separator).Trim()
        $value = $part.Substring($separator + 1).Trim()
        foreach ($candidate in $Keys) {
            if ($key.Equals($candidate, [System.StringComparison]::OrdinalIgnoreCase)) {
                return $value
            }
        }
    }

    return $null
}

if (Test-CommandAvailable "dotnet") {
    $dotnetVersion = (& dotnet --version).Trim()
    if ($dotnetVersion.StartsWith("9.")) {
        Add-Check ".NET SDK" "Pass" "Found .NET SDK $dotnetVersion."
    }
    else {
        Add-Check ".NET SDK" "Fail" "Expected .NET SDK 9.x but found $dotnetVersion." $true
    }
}
else {
    Add-Check ".NET SDK" "Fail" "dotnet was not found on PATH." $true
}

if (Test-CommandAvailable "dotnet") {
    $nugetSources = & dotnet nuget list source
    $hasNugetOrg = ($nugetSources | Select-String -Pattern "https://api.nuget.org/v3/index.json" -Quiet)
    if ($hasNugetOrg) {
        Add-Check "NuGet source" "Pass" "nuget.org is registered as a package source."
    }
    else {
        Add-Check "NuGet source" "Fail" "nuget.org is not registered. Add it without committing credentials: dotnet nuget add source https://api.nuget.org/v3/index.json -n nuget.org." $true
    }

    if (-not $SkipNetworkCheck) {
        if (Test-TcpPort "api.nuget.org" 443) {
            Add-Check "NuGet network" "Pass" "api.nuget.org:443 is reachable."
        }
        else {
            Add-Check "NuGet network" "Fail" "api.nuget.org:443 is not reachable. Restore/build will fail until firewall/proxy/network policy allows access or a complete local package source is configured." $true
        }
    }
}

$apiAssetsPath = Join-Path $repoRoot "backend\src\Swyftly.Api\obj\project.assets.json"
$seedAssetsPath = Join-Path $repoRoot "scripts\Swyftly.DevSeed\obj\project.assets.json"
if ((Test-Path $apiAssetsPath) -and (Test-Path $seedAssetsPath)) {
    Add-Check "Restore assets" "Pass" "Backend API and dev seed project.assets.json files exist."
}
else {
    Add-Check "Restore assets" "Warning" "One or more project.assets.json files are missing. Run dotnet restore backend\Swyftly.sln and dotnet restore scripts\Swyftly.DevSeed\Swyftly.DevSeed.csproj after NuGet access is fixed."
}

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
    Add-Check "PostgreSQL connection" "Fail" "No connection string found. Pass -ConnectionString or set ConnectionStrings__DefaultConnection." $true
}
else {
    $dbHost = Get-ConnectionValue $ConnectionString @("Host", "Server", "Data Source")
    $dbPortText = Get-ConnectionValue $ConnectionString @("Port")
    if ([string]::IsNullOrWhiteSpace($dbHost)) {
        $dbHost = "localhost"
    }

    $dbPort = 5432
    if (-not [string]::IsNullOrWhiteSpace($dbPortText)) {
        [int]::TryParse($dbPortText, [ref]$dbPort) | Out-Null
    }

    if (Test-TcpPort $dbHost $dbPort) {
        Add-Check "PostgreSQL connection" "Pass" "TCP connection to $dbHost`:$dbPort succeeded."
    }
    else {
        Add-Check "PostgreSQL connection" "Fail" "Could not connect to $dbHost`:$dbPort. Start PostgreSQL or pass a reachable -ConnectionString." $true
    }
}

if (Test-CommandAvailable "node") {
    Add-Check "Node.js" "Pass" "Found Node.js $((& node --version).Trim())."
}
else {
    Add-Check "Node.js" "Fail" "node was not found on PATH." $true
}

if (Test-CommandAvailable "npm") {
    Add-Check "npm" "Pass" "Found npm $((& npm --version).Trim())."
}
else {
    Add-Check "npm" "Fail" "npm was not found on PATH." $true
}

$frontendRoot = Join-Path $repoRoot "frontend\swyftly-web"
if (Test-Path (Join-Path $frontendRoot "node_modules")) {
    Add-Check "Frontend dependencies" "Pass" "frontend\swyftly-web\node_modules exists."
}
else {
    Add-Check "Frontend dependencies" "Warning" "frontend\swyftly-web\node_modules is missing. Run cmd /c npm install in frontend\swyftly-web."
}

if (Test-Path (Join-Path $frontendRoot "karma.conf.js")) {
    Add-Check "Karma config" "Pass" "karma.conf.js exists."
}
else {
    Add-Check "Karma config" "Fail" "karma.conf.js is missing." $true
}

$chromeCandidates = @()
if (-not [string]::IsNullOrWhiteSpace($env:CHROME_BIN)) {
    $chromeCandidates += $env:CHROME_BIN
}

$chromeCandidates += @(
    (Join-Path $env:ProgramFiles "Google\Chrome\Application\chrome.exe"),
    (Join-Path ${env:ProgramFiles(x86)} "Google\Chrome\Application\chrome.exe"),
    (Join-Path $env:LOCALAPPDATA "Google\Chrome\Application\chrome.exe")
)

$chromePath = $chromeCandidates | Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and (Test-Path $_) } | Select-Object -First 1
if ($chromePath) {
    Add-Check "Chrome/Karma browser" "Pass" "Found Chrome at $chromePath."
}
else {
    Add-Check "Chrome/Karma browser" "Warning" "Chrome was not found in common locations. Set CHROME_BIN before running npm run test:ci if Karma cannot launch ChromeHeadlessCI."
}

Write-Host ""
Write-Host "Swyftly development environment preflight"
Write-Host ""
$checks | Format-Table -AutoSize

$failedCritical = @($checks | Where-Object { $_.Status -eq "Fail" -and $_.Critical }).Count
if ($failedCritical -gt 0) {
    Write-Host ""
    Write-Host "$failedCritical critical preflight check(s) failed." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Preflight completed without critical failures." -ForegroundColor Green
