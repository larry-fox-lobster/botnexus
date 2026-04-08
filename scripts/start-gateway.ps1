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
$gatewayDll = Join-Path $repoRoot "src\gateway\BotNexus.Gateway.Api\bin\Release\net10.0\BotNexus.Gateway.Api.dll"
$gatewayUrl = "http://localhost:$Port"
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

function Build-Gateway {
    Write-Host "🔧 Building Gateway API project (Release)..."
    dotnet build $gatewayProject -c Release --nologo --tl:off
    if ($LASTEXITCODE -ne 0) {
        throw "Gateway API build failed."
    }
    if (-not (Test-Path $gatewayDll)) {
        throw "Release build output not found at $gatewayDll."
    }
}

function Wait-ForRestartOrAbort {
    param([int]$Seconds = 5)

    Write-Host ""
    Write-Host "🔄 Gateway will restart in $Seconds seconds. Press 'q' to quit instead." -ForegroundColor Yellow

    for ($i = $Seconds; $i -gt 0; $i--) {
        Write-Host "`r   Restarting in $i... " -NoNewline
        $deadline = [DateTime]::UtcNow.AddSeconds(1)
        while ([DateTime]::UtcNow -lt $deadline) {
            if ([Console]::KeyAvailable) {
                $key = [Console]::ReadKey($true)
                if ($key.KeyChar -eq 'q' -or $key.KeyChar -eq 'Q') {
                    Write-Host "`r   Quit requested. Exiting.       "
                    return $false
                }
            }
            Start-Sleep -Milliseconds 50
        }
    }
    Write-Host "`r   Restarting now...       "
    return $true
}

# --- Initial startup ---

if (-not (Test-PortAvailable -Port $Port)) {
    throw "Port $Port is already in use. Stop the existing process or choose a different port (for example: -Port 5007)."
}

if (-not $SkipBuild) {
    Build-Gateway
}
elseif (-not (Test-Path $gatewayDll)) {
    throw "Release build output not found at $gatewayDll. Run without -SkipBuild first."
}

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = $gatewayUrl

# --- Run loop ---

while ($true) {
    Write-Host ""
    Write-Host "🚀 Starting Gateway API"
    Write-Host "   URL:         $gatewayUrl"
    Write-Host "   Environment: $($env:ASPNETCORE_ENVIRONMENT)"
    Write-Host ""
    Write-Host "Press Ctrl+C to stop the gateway."

    try {
        dotnet $gatewayDll
    }
    catch {
        Write-Host "⚠️  Gateway exited with error: $($_.Exception.Message)" -ForegroundColor Red
    }

    Write-Host ""
    Write-Host "⏹️  Gateway process exited." -ForegroundColor Cyan

    if (-not (Wait-ForRestartOrAbort -Seconds 5)) {
        break
    }

    # Rebuild before restarting
    try {
        Build-Gateway
    }
    catch {
        Write-Host "❌ Build failed: $($_.Exception.Message)" -ForegroundColor Red
        if (-not (Wait-ForRestartOrAbort -Seconds 5)) {
            break
        }
    }
}
