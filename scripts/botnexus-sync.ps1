#!/usr/bin/env pwsh
# botnexus-sync.ps1
# Manages two BotNexus instances:
#   PROD: ~/botnexus-prod  (sytone/botnexus main) -> ~/.botnexus/     -> port 5005
#   DEV:  ~/projects/botnexus (fork main)          -> ~/.botnexus-dev/ -> port 5006
#
# Handles build, extension deploy, and process lifecycle directly.
# The CLI's `gateway start` cannot manage two instances yet because
# GatewayProcessManager uses a single hardcoded PID file (issue #36).
# Once #36 ships this becomes:
#   botnexus gateway start --source <repo> --target <home> --port <n>
#
# Runs every 15 min via cron. Logs to ~/logs/botnexus-sync.log

$ErrorActionPreference = 'Stop'

$env:PATH        = "$env:HOME/.dotnet:$env:HOME/.dotnet/tools:" + $env:PATH
$env:DOTNET_ROOT = "$env:HOME/.dotnet"
$env:DOTNET_SYSTEM_GLOBALIZATION_INVARIANT = '1'

$DOTNET = "$env:HOME/.dotnet/dotnet"
$LogDir  = "$env:HOME/logs"
$LogFile = "$LogDir/botnexus-sync.log"
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

function Write-Log {
    param([string]$Message)
    $line = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $Message"
    Write-Host $line
    Add-Content -Path $LogFile -Value $line
}

# Lock file — prevent concurrent runs
$LockFile   = '/tmp/botnexus-sync.lock'
$LockStream = $null
try {
    $LockStream = [System.IO.File]::Open($LockFile, 'OpenOrCreate', 'ReadWrite', 'None')
} catch {
    Write-Log "Already running — skipping"
    exit 0
}

function Test-GatewayRunning {
    param([string]$BnHome)
    $PidFile = "$BnHome/gateway.pid"
    if (-not (Test-Path $PidFile)) { return $false }
    $GwPid = [int](Get-Content $PidFile -Raw).Trim()
    try { return $null -ne (Get-Process -Id $GwPid -ErrorAction Stop) }
    catch { return $false }
}

function Stop-Gateway {
    param([string]$BnHome, [string]$Label)
    $PidFile = "$BnHome/gateway.pid"
    if (-not (Test-Path $PidFile)) { return }
    $GwPid = [int](Get-Content $PidFile -Raw).Trim()
    Write-Log "[$Label] Stopping gateway (pid $GwPid)..."
    Stop-Process -Id $GwPid -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Stop-Process -Id $GwPid -Force -ErrorAction SilentlyContinue
    Remove-Item $PidFile -Force -ErrorAction SilentlyContinue
}

function Start-Gateway {
    param([string]$Dll, [string]$BnHome, [int]$Port, [string]$Label)
    $PidFile = "$BnHome/gateway.pid"
    $GwLog   = "$LogDir/botnexus-gateway-$Label.log"
    Write-Log "[$Label] Starting gateway (port $Port)..."
    $env:BOTNEXUS_HOME = $BnHome
    # stdout and stderr go to separate files — Start-Process on Linux cannot
    # redirect both to the same path
    $proc = Start-Process -FilePath $DOTNET `
        -ArgumentList "$Dll --urls http://0.0.0.0:$Port" `
        -RedirectStandardOutput $GwLog `
        -RedirectStandardError  "$GwLog.err" `
        -NoNewWindow -PassThru
    $proc.Id | Set-Content $PidFile
    Start-Sleep -Seconds 4
    if (Test-GatewayRunning $BnHome) {
        Write-Log "[$Label] Started (pid $($proc.Id))"
    } else {
        Write-Log "[$Label] ERROR: Exited immediately — check $GwLog"
        Remove-Item $PidFile -Force -ErrorAction SilentlyContinue
        throw "Gateway failed to start for $Label"
    }
}

function Deploy-Extensions {
    # Mirrors what the CLI's ServeCommand.DeployExtensions does:
    # publish extensions, publish Blazor client, copy to BnHome/extensions/
    param([string]$Repo, [string]$BnHome, [string]$Label)
    Write-Log "[$Label] Deploying extensions..."

    # Publish Blazor client (wwwroot static assets)
    & $DOTNET publish "$Repo/src/extensions/BotNexus.Extensions.Channels.SignalR.BlazorClient" `
        -c Release --nologo 2>&1 | Add-Content $LogFile

    $BlazorPublish = "$Repo/src/extensions/BotNexus.Extensions.Channels.SignalR.BlazorClient/bin/Release/net10.0/publish/wwwroot"
    if (Test-Path $BlazorPublish) {
        Remove-Item "$BnHome/extensions/botnexus-signalr/blazor" -Recurse -Force -ErrorAction SilentlyContinue
        Copy-Item $BlazorPublish "$BnHome/extensions/botnexus-signalr/blazor" -Recurse -Force
    }

    # Copy extension DLLs — published alongside the gateway build
    $ExtSrc = "$Repo/src/extensions"
    foreach ($extDir in (Get-ChildItem $ExtSrc -Directory)) {
        $publishDir = "$($extDir.FullName)/bin/Release/net10.0/publish"
        if (-not (Test-Path $publishDir)) { continue }
        $extName = $extDir.Name.ToLower() -replace 'botnexus\.', 'botnexus-'
        $extDest = "$BnHome/extensions/$extName"
        New-Item -ItemType Directory -Force -Path $extDest | Out-Null
        Copy-Item "$publishDir/*" $extDest -Recurse -Force -ErrorAction SilentlyContinue
    }

    Write-Log "[$Label] Extensions deployed."
}

function Sync-Instance {
    param([string]$Repo, [string]$Remote, [string]$BnHome, [int]$Port, [string]$Label)

    # Release build — same as CLI gateway start
    $Dll = "$Repo/src/gateway/BotNexus.Gateway.Api/bin/Release/net10.0/BotNexus.Gateway.Api.dll"
    Set-Location $Repo

    & git fetch $Remote main 2>&1 | Add-Content $LogFile
    $LocalSha  = (git rev-parse HEAD).Trim()
    $RemoteSha = (git rev-parse "$Remote/main").Trim()

    if ($LocalSha -eq $RemoteSha) {
        Write-Log "[$Label] Up to date ($LocalSha)."
        if (-not (Test-GatewayRunning $BnHome)) {
            Write-Log "[$Label] Gateway not running — starting..."
            Deploy-Extensions $Repo $BnHome $Label
            Start-Gateway $Dll $BnHome $Port $Label
        }
        return
    }

    Write-Log "[$Label] New commits: $LocalSha -> $RemoteSha — pulling..."
    & git pull $Remote main 2>&1 | Add-Content $LogFile

    Write-Log "[$Label] Building (Release)..."
    & $DOTNET build "$Repo/BotNexus.slnx" -c Release --nologo --tl:off 2>&1 | Add-Content $LogFile
    if ($LASTEXITCODE -ne 0) { Write-Log "[$Label] ERROR: Build failed — aborting."; return }

    Write-Log "[$Label] Running tests..."
    & $DOTNET test "$Repo/BotNexus.slnx" --nologo --tl:off 2>&1 | Add-Content $LogFile
    if ($LASTEXITCODE -ne 0) { Write-Log "[$Label] ERROR: Tests failed — aborting restart."; return }
    Write-Log "[$Label] Tests passed."

    Deploy-Extensions $Repo $BnHome $Label

    if (Test-GatewayRunning $BnHome) { Stop-Gateway $BnHome $Label }
    Start-Gateway $Dll $BnHome $Port $Label
}

try {
    Write-Log "=== BotNexus sync started ==="

    # PROD: sytone/botnexus (--source) -> ~/.botnexus (--target) -> port 5005
    Sync-Instance "$env:HOME/botnexus-prod" "origin" "$env:HOME/.botnexus" 5005 "prod"

    # DEV: fork (--source) -> ~/.botnexus-dev (--target) -> port 5006
    Sync-Instance "$env:HOME/projects/botnexus" "fork" "$env:HOME/.botnexus-dev" 5006 "dev"

    Write-Log "=== Done ==="
} finally {
    if ($LockStream) { $LockStream.Dispose() }
    Remove-Item $LockFile -Force -ErrorAction SilentlyContinue
}
