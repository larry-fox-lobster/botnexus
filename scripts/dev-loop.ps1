#!/usr/bin/env pwsh

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateRange(1, 65535)]
    [int]$Port = 5005,

    [Parameter()]
    [switch]$Watch,

    [Parameter()]
    [switch]$SkipBuild,

    [Parameter()]
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repoRoot "BotNexus.slnx"
$gatewayTestsPath = Join-Path $repoRoot "tests\BotNexus.Gateway.Tests"
$gatewayProject = Join-Path $repoRoot "src\gateway\BotNexus.Gateway.Api\BotNexus.Gateway.Api.csproj"
$startScript = Join-Path $PSScriptRoot "start-gateway.ps1"
$gatewayUrl = "http://localhost:$Port"

Push-Location $repoRoot
try {
    if ($SkipBuild) {
        Write-Host "⏭️  Skipping solution build (-SkipBuild)."
    }
    else {
        Write-Host "🔧 Building full solution..."
        dotnet build $solutionPath --nologo --tl:off
        if ($LASTEXITCODE -ne 0) {
            throw "Solution build failed."
        }
    }

    if ($SkipTests) {
        Write-Host ""
        Write-Host "⏭️  Skipping Gateway tests (-SkipTests)."
    }
    else {
        Write-Host ""
        Write-Host "🧪 Running Gateway tests..."
        dotnet test $gatewayTestsPath --nologo --tl:off
        if ($LASTEXITCODE -ne 0) {
            throw "Gateway tests failed. Not starting Gateway."
        }
    }

    if ($Watch) {
        $env:ASPNETCORE_ENVIRONMENT = "Development"
        $env:ASPNETCORE_URLS = $gatewayUrl

        Write-Host ""
        Write-Host "👀 Starting Gateway in watch mode at $gatewayUrl"
        Write-Host "Open WebUI: $gatewayUrl/webui"
        dotnet watch --project $gatewayProject run --no-launch-profile
    }
    else {
        & $startScript -Port $Port -SkipBuild
    }
}
catch {
    Write-Error "❌ Dev loop failed: $($_.Exception.Message)"
    exit 1
}
finally {
    Pop-Location
}

if (-not $Watch) {
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "✅ Dev loop completed."
    }
}
