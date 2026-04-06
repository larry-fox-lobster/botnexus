<#
.SYNOPSIS
    Exports the OpenAPI specification from the BotNexus Gateway API.

.DESCRIPTION
    Builds the Gateway API, starts it on a temporary port, fetches the
    OpenAPI spec from /swagger/v1/swagger.json, and saves it to docs/api/openapi.json.
    The API process is stopped automatically after export.

.PARAMETER Port
    The temporary port to run the API on during export. Default: 15099.

.PARAMETER OutputPath
    Where to save the exported spec. Default: docs/api/openapi.json (relative to repo root).

.PARAMETER SkipBuild
    Skip the build step if the API was already built.

.EXAMPLE
    ./scripts/export-openapi.ps1
    ./scripts/export-openapi.ps1 -Port 15200 -SkipBuild
#>
[CmdletBinding()]
param(
    [int]$Port = 15099,
    [string]$OutputPath,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'src\gateway\BotNexus.Gateway.Api\BotNexus.Gateway.Api.csproj'

if (-not $OutputPath) {
    $OutputPath = Join-Path $repoRoot 'docs\api\openapi.json'
}

$outputDir = Split-Path -Parent $OutputPath
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

# Build
if (-not $SkipBuild) {
    Write-Host "Building Gateway API..." -ForegroundColor Cyan
    dotnet build $projectPath --nologo --tl:off -v:q
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed."
        exit 1
    }
}

# Start the API on a temporary port.
# Override BotNexus:ConfigPath to an empty config so PlatformConfig.GetListenUrl()
# returns null and doesn't override the ASPNETCORE_URLS port binding.
$listenUrl = "http://localhost:$Port"
Write-Host "Starting Gateway API on $listenUrl..." -ForegroundColor Cyan

$savedUrls = $env:ASPNETCORE_URLS
$savedConfig = $env:BotNexus__ConfigPath
$env:ASPNETCORE_URLS = $listenUrl
$env:BotNexus__ConfigPath = (Join-Path $repoRoot '.openapi-export-empty-config.json')
'{}' | Set-Content $env:BotNexus__ConfigPath -Encoding UTF8

$process = Start-Process -FilePath 'dotnet' `
    -ArgumentList "run --project `"$projectPath`" --no-build" `
    -PassThru -NoNewWindow `
    -RedirectStandardOutput (Join-Path $repoRoot '.openapi-export-stdout.log') `
    -RedirectStandardError (Join-Path $repoRoot '.openapi-export-stderr.log')

# Restore env vars so they don't leak into the parent shell
$env:ASPNETCORE_URLS = $savedUrls
$env:BotNexus__ConfigPath = $savedConfig

try {
    # Wait for the API to become ready
    $maxAttempts = 30
    $attempt = 0
    $ready = $false

    while ($attempt -lt $maxAttempts -and -not $ready) {
        $attempt++
        Start-Sleep -Seconds 1
        try {
            $health = Invoke-RestMethod -Uri "$listenUrl/health" -TimeoutSec 3 -ErrorAction SilentlyContinue
            if ($health.status -eq 'ok') {
                $ready = $true
            }
        }
        catch {
            # API not ready yet
        }
    }

    if (-not $ready) {
        Write-Error "Gateway API did not start within $maxAttempts seconds."
        exit 1
    }

    Write-Host "API is ready. Fetching OpenAPI spec..." -ForegroundColor Cyan

    # Fetch the spec
    $specUrl = "$listenUrl/swagger/v1/swagger.json"
    $spec = Invoke-RestMethod -Uri $specUrl -TimeoutSec 10

    # Pretty-print and save
    $json = $spec | ConvertTo-Json -Depth 100
    [System.IO.File]::WriteAllText($OutputPath, $json, [System.Text.Encoding]::UTF8)

    Write-Host "OpenAPI spec exported to: $OutputPath" -ForegroundColor Green
}
finally {
    # Clean up: stop the API process
    if ($process -and -not $process.HasExited) {
        Write-Host "Stopping Gateway API..." -ForegroundColor Cyan
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        $process.WaitForExit(5000) | Out-Null
    }

    # Clean up temp files
    Remove-Item (Join-Path $repoRoot '.openapi-export-stdout.log') -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $repoRoot '.openapi-export-stderr.log') -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $repoRoot '.openapi-export-empty-config.json') -ErrorAction SilentlyContinue
}
