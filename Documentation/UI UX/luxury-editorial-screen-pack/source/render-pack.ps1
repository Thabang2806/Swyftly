param(
    [string]$ChromePath = "C:\Program Files\Google\Chrome\Application\chrome.exe",
    [int]$DesktopWidth = 1440,
    [int]$DesktopHeight = 1100,
    [int]$MobileWidth = 390,
    [int]$MobileHeight = 920,
    [switch]$ContactSheetsOnly
)

$ErrorActionPreference = "Stop"

$sourceRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$packRoot = Split-Path -Parent $sourceRoot
$repoRoot = Resolve-Path (Join-Path $packRoot "..\..\..")
$routeFile = Join-Path $repoRoot "frontend\mabuntle-web\src\app\app.routes.ts"
$mockupHtml = Join-Path $sourceRoot "mockup.html"
$desktopDir = Join-Path $packRoot "desktop"
$mobileDir = Join-Path $packRoot "mobile"
$contactDir = Join-Path $packRoot "contact-sheets"

if (-not (Test-Path $ChromePath)) {
    throw "Chrome not found at '$ChromePath'. Pass -ChromePath with the local browser executable."
}

New-Item -ItemType Directory -Path $desktopDir, $mobileDir, $contactDir -Force | Out-Null

$routes = @()
foreach ($line in Get-Content $routeFile) {
    if ($line -match "path:\s*'([^']*)'") {
        $value = $Matches[1]
        if ($value -ne "**") {
            $routes += $value
        }
    }
}

function ConvertTo-Slug([string]$route) {
    if ([string]::IsNullOrWhiteSpace($route)) {
        return "home"
    }

    $slug = $route.ToLowerInvariant()
    $slug = $slug -replace ":", ""
    $slug = $slug -replace "[^a-z0-9]+", "-"
    $slug = $slug.Trim("-")
    if ([string]::IsNullOrWhiteSpace($slug)) {
        return "home"
    }

    return $slug
}

function Invoke-ChromeScreenshot([string]$route, [string]$outputPath, [int]$width, [int]$height) {
    $fileUri = (New-Object System.Uri($mockupHtml)).AbsoluteUri
    $encodedRoute = [System.Uri]::EscapeDataString($route)
    $url = "$fileUri`?route=$encodedRoute"
    $args = @(
        "--headless=new",
        "--disable-gpu",
        "--hide-scrollbars",
        "--disable-extensions",
        "--no-first-run",
        "--no-default-browser-check",
        "--window-size=$width,$height",
        "--screenshot=$outputPath",
        $url
    )
    & $ChromePath @args | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Chrome failed for route '$route' with exit code $LASTEXITCODE."
    }
}

$routeIndexPath = Join-Path $packRoot "route-index.json"
$routeIndex = @()
if ($ContactSheetsOnly) {
    $routeIndex = Get-Content $routeIndexPath -Raw | ConvertFrom-Json
} else {
    foreach ($route in $routes) {
        $slug = ConvertTo-Slug $route
        $desktopFile = Join-Path $desktopDir "$slug.png"
        $mobileFile = Join-Path $mobileDir "$slug.png"

        Invoke-ChromeScreenshot -route $route -outputPath $desktopFile -width $DesktopWidth -height $DesktopHeight
        Invoke-ChromeScreenshot -route $route -outputPath $mobileFile -width $MobileWidth -height $MobileHeight

        $area = if ($route -like "admin*") { "admin" } elseif ($route -like "seller/*" -and $route -ne "seller/:storeSlug") { "seller" } elseif ($route -eq "seller") { "seller" } elseif ($route -like "account*") { "account" } elseif ($route -like "checkout*") { "checkout" } elseif ($route -eq "login" -or $route -like "register/*" -or $route -eq "access-denied") { "auth" } else { "buyer-public" }
        $routeIndex += [pscustomobject][ordered]@{
            route = if ([string]::IsNullOrWhiteSpace($route)) { "/" } else { "/$route" }
            slug = $slug
            area = $area
            desktop = "desktop/$slug.png"
            mobile = "mobile/$slug.png"
            status = "Generated"
            visualReview = "Pending manual review"
        }
    }

    $routeIndex | ConvertTo-Json -Depth 5 | Set-Content -Path $routeIndexPath -Encoding UTF8
}

function New-ContactSheet([string]$area, [object[]]$items, [string]$viewport) {
    $imagesDir = if ($viewport -eq "desktop") { "desktop" } else { "mobile" }
    $htmlPath = Join-Path $contactDir "$area-$viewport.html"
    $pngPath = Join-Path $contactDir "$area-$viewport.png"
    $width = if ($viewport -eq "desktop") { 1440 } else { 1200 }
    $height = if ($viewport -eq "desktop") { 1800 } else { 1800 }
    $cardWidth = if ($viewport -eq "desktop") { "420px" } else { "220px" }

    $cards = ($items | ForEach-Object {
        $routeLabel = $_.route
        $image = "../$imagesDir/$($_.slug).png"
        "<article><img src='$image' alt='$routeLabel'><strong>$routeLabel</strong></article>"
    }) -join "`n"

    $html = @"
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>Mabuntle $area $viewport contact sheet</title>
<style>
body{margin:0;padding:28px;background:#f7f1eb;color:#181114;font-family:Segoe UI,Arial,sans-serif}
h1{font-family:Georgia,serif;font-weight:400;letter-spacing:.03em;margin:0 0 20px}
.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax($cardWidth,1fr));gap:18px}
article{background:#fffaf5;border:1px solid #d8c2ad;padding:10px;box-shadow:0 16px 40px rgba(42,20,37,.1)}
img{display:block;width:100%;height:auto;border:1px solid rgba(216,194,173,.7)}
strong{display:block;margin-top:8px;font-size:11px;letter-spacing:.08em;text-transform:uppercase}
</style>
</head>
<body>
<h1>Mabuntle $area - $viewport</h1>
<div class="grid">$cards</div>
</body>
</html>
"@

    Set-Content -Path $htmlPath -Value $html -Encoding UTF8
    $fileUri = (New-Object System.Uri($htmlPath)).AbsoluteUri
    $args = @(
        "--headless=new",
        "--disable-gpu",
        "--hide-scrollbars",
        "--disable-extensions",
        "--no-first-run",
        "--no-default-browser-check",
        "--window-size=$width,$height",
        "--screenshot=$pngPath",
        $fileUri
    )
    & $ChromePath @args | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Chrome failed contact sheet '$area-$viewport' with exit code $LASTEXITCODE."
    }
}

$areas = $routeIndex | Group-Object area
foreach ($group in $areas) {
    New-ContactSheet -area $group.Name -items $group.Group -viewport "desktop"
    New-ContactSheet -area $group.Name -items $group.Group -viewport "mobile"
}

$totalPngs = (Get-ChildItem -Path $desktopDir, $mobileDir -Filter *.png | Measure-Object).Count
$contactPngs = (Get-ChildItem -Path $contactDir -Filter *.png | Measure-Object).Count
Write-Host "Available $totalPngs route screenshots and $contactPngs contact sheets."
