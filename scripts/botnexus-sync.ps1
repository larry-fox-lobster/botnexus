#!/usr/bin/env pwsh
# botnexus-sync.ps1
# Manages two BotNexus instances:
#   PROD: ~/botnexus-prod  (sytone/botnexus main) -> ~/.botnexus/     -> port 5005
#   DEV:  ~/projects/botnexus (fork main)          -> ~/.botnexus-dev/ -> port 5006
#
# Runs every 15 min via cron. Logs to ~/logs/botnexus-sync.log

$ErrorActionPreference = 'Stop'

$env:PATH = "$env:HOME/.dotnet:$env:HOME/.dotnet/tools:" + $env:PATH
$env:DOTNET_ROOT = "$env:HOME/.dotnet"
$DOTNET = "$env:HOME/.dotnet/dotnet"

$LogDir = "$env:HOME/logs"
$LogFile = "$LogDir/botnexus-sync.log"
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

function Write-Log {
    param([string]$Message)
    $line = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $Message"
    Write-Host $line
    Add-Content -Path $LogFile -Value $line
}

# Lock file — prevent concurrent runs
$LockFile = '/tmp/botnexus-sync.lock'
$LockStream = $null
try {
    $LockStream = [System.IO.File]::Open($LockFile, 'OpenOrCreate', 'ReadWrite', 'None')
} catch {
    Write-Log "Already running — skipping"
    exit 0
}

function Test-GatewayRunning {
    param([string]$PidFile)
    if (-not (Test-Path $PidFile)) { return $false }
    $GwPid = [int](Get-Content $PidFile -Raw).Trim()
    try {
        $proc = Get-Process -Id $GwPid -ErrorAction Stop
        return $proc -ne $null
    } catch {
        return $false
    }
}

function Stop-Gateway {
    param([string]$PidFile, [string]$Label)
    if (-not (Test-Path $PidFile)) { return }
    $GwPid = [int](Get-Content $PidFile -Raw).Trim()
    Write-Log "[$Label] Stopping gateway (pid $GwPid)..."
    try {
        Stop-Process -Id $GwPid -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        Stop-Process -Id $GwPid -Force -ErrorAction SilentlyContinue
    } catch { }
    Remove-Item $PidFile -Force -ErrorAction SilentlyContinue
}

function Start-Gateway {
    param(
        [string]$Dll,
        [string]$BnHome,
        [int]$Port,
        [string]$PidFile,
        [string]$LogFile,
        [string]$Label
    )
    Write-Log "[$Label] Starting gateway (port $Port)..."
    $env:BOTNEXUS_HOME = $BnHome
    $env:DOTNET_SYSTEM_GLOBALIZATION_INVARIANT = '1'
    # On Linux, Start-Process cannot redirect stdout and stderr to the same file.
    # Use a background job with redirection instead.
    $proc = Start-Process -FilePath $DOTNET `
        -ArgumentList "$Dll --urls http://0.0.0.0:$Port" `
        -RedirectStandardOutput $GwLog `
        -RedirectStandardError "$GwLog.err" `
        -NoNewWindow `
        -PassThru
    $proc.Id | Set-Content $PidFile
    Start-Sleep -Seconds 4
    if (Test-GatewayRunning $PidFile) {
        Write-Log "[$Label] Started (pid $($proc.Id))"
    } else {
        Write-Log "[$Label] ERROR: Exited immediately — check $LogFile"
        Remove-Item $PidFile -Force -ErrorAction SilentlyContinue
        throw "Gateway failed to start for $Label"
    }
}

function Sync-Instance {
    param(
        [string]$Repo,
        [string]$Remote,
        [string]$BnHome,
        [int]$Port,
        [string]$Label
    )

    $PidFile  = "$BnHome/gateway.pid"
    $Dll      = "$Repo/src/gateway/BotNexus.Gateway.Api/bin/Debug/net10.0/BotNexus.Gateway.Api.dll"
    $GwLog    = "$LogDir/botnexus-gateway-$Label.log"

    Set-Location $Repo

    & git fetch $Remote main 2>&1 | Add-Content $LogFile
    $LocalSha  = (git rev-parse HEAD).Trim()
    $RemoteSha = (git rev-parse "$Remote/main").Trim()

    if ($LocalSha -eq $RemoteSha) {
        Write-Log "[$Label] Up to date ($LocalSha)."
        if (-not (Test-GatewayRunning $PidFile)) {
            Write-Log "[$Label] Gateway not running — starting..."
            Start-Gateway $Dll $BnHome $Port $PidFile $GwLog $Label
        }
        return
    }

    Write-Log "[$Label] New commits: $LocalSha -> $RemoteSha — pulling..."
    & git pull $Remote main 2>&1 | Add-Content $LogFile

    Write-Log "[$Label] Building..."
    & $DOTNET build BotNexus.slnx --nologo --tl:off 2>&1 | Add-Content $LogFile
    if ($LASTEXITCODE -ne 0) {
        Write-Log "[$Label] ERROR: Build failed — aborting."
        return
    }

    Write-Log "[$Label] Running tests..."
    & $DOTNET test BotNexus.slnx --nologo --tl:off 2>&1 | Add-Content $LogFile
    if ($LASTEXITCODE -ne 0) {
        Write-Log "[$Label] ERROR: Tests failed — aborting restart."
        return
    }
    Write-Log "[$Label] Tests passed."

    # Deploy extensions
    Write-Log "[$Label] Deploying extensions..."
    $env:BOTNEXUS_HOME = $BnHome
    & $DOTNET "$Repo/src/gateway/BotNexus.Cli/bin/Debug/net10.0/BotNexus.Cli.dll" `
        gateway start --path $Repo --dev 2>&1 | Add-Content $LogFile

    # Copy extensions to the right home if different from ~/.botnexus
    if ($BnHome -ne "$env:HOME/.botnexus" -and (Test-Path "$env:HOME/.botnexus/extensions")) {
        New-Item -ItemType Directory -Force -Path "$BnHome/extensions" | Out-Null
        Copy-Item "$env:HOME/.botnexus/extensions/*" "$BnHome/extensions/" -Recurse -Force
    }

    # Publish Blazor client
    & $DOTNET publish "$Repo/src/extensions/BotNexus.Extensions.Channels.SignalR.BlazorClient" `
        -c Release --nologo 2>&1 | Add-Content $LogFile
    $BlazorPublish = "$Repo/src/extensions/BotNexus.Extensions.Channels.SignalR.BlazorClient/bin/Release/net10.0/publish/wwwroot"
    if (Test-Path $BlazorPublish) {
        Remove-Item "$BnHome/extensions/botnexus-signalr/blazor" -Recurse -Force -ErrorAction SilentlyContinue
        Copy-Item $BlazorPublish "$BnHome/extensions/botnexus-signalr/blazor" -Recurse -Force
    }
    Write-Log "[$Label] Extensions deployed."

    if (Test-GatewayRunning $PidFile) {
        Stop-Gateway $PidFile $Label
    }
    Start-Gateway $Dll $BnHome $Port $PidFile $GwLog $Label
}

try {
    Write-Log "=== BotNexus sync started ==="

    # PROD: sytone/botnexus -> ~/.botnexus -> port 5005
    Sync-Instance "$env:HOME/botnexus-prod" "origin" "$env:HOME/.botnexus" 5005 "prod"

    # DEV: fork -> ~/.botnexus-dev -> port 5006
    Sync-Instance "$env:HOME/projects/botnexus" "fork" "$env:HOME/.botnexus-dev" 5006 "dev"

    Write-Log "=== Done ==="
} finally {
    if ($LockStream) { $LockStream.Dispose() }
    Remove-Item $LockFile -Force -ErrorAction SilentlyContinue
}
