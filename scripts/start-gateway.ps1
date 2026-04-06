#!/usr/bin/env pwsh

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateRange(1, 65535)]
    [int]$Port = 5005,

    [Parameter()]
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$gatewayProject = Join-Path $repoRoot "src\gateway\BotNexus.Gateway.Api\BotNexus.Gateway.Api.csproj"
$gatewayUrl = "http://localhost:$Port"
$webUiUrl = "$gatewayUrl/webui"
$tcpAddress = [System.Net.IPAddress]::Loopback

function Test-PortAvailable {
    param(
        [Parameter(Mandatory = $true)]
        [int]$Port
    )

    $listener = [System.Net.Sockets.TcpListener]::new($tcpAddress, $Port)
    try {
        $listener.Server.ExclusiveAddressUse = $true
        $listener.Start()
        return $true
    }
    catch {
        return $false
    }
    finally {
        try { $listener.Stop() } catch { }
    }
}

if (-not (Test-PortAvailable -Port $Port)) {
    throw "Port $Port is already in use. Stop the existing process or choose a different port (for example: -Port 5007)."
}

if ($SkipBuild) {
    Write-Host "⏭️  Skipping Gateway build (-SkipBuild)."
}
else {
    Write-Host "🔧 Building Gateway API project..."
    dotnet build $gatewayProject --nologo --tl:off
    if ($LASTEXITCODE -ne 0) {
        throw "Gateway API build failed."
    }
}

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = $gatewayUrl

Write-Host ""
Write-Host "🚀 Starting Gateway API"
Write-Host "   URL:        $gatewayUrl"
Write-Host "   WebUI:      $webUiUrl"
Write-Host "   Environment: $($env:ASPNETCORE_ENVIRONMENT)"
Write-Host ""
Write-Host "Press Ctrl+C to stop."

try {
    dotnet run --project $gatewayProject --no-build
}
catch {
    throw "Gateway failed to start at $gatewayUrl. $($_.Exception.Message)"
}
