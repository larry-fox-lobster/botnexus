[CmdletBinding()]
param(
    [string]$InstallPath = (Join-Path $HOME ".botnexus\app"),
    [string]$PackagesPath = (Join-Path (Split-Path -Path $PSScriptRoot -Parent) "artifacts")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$resolvedInstallPath = [System.IO.Path]::GetFullPath($InstallPath)
$resolvedPackagesPath = [System.IO.Path]::GetFullPath($PackagesPath)
$installScriptPath = Join-Path $PSScriptRoot "install.ps1"

$gatewayProcesses = @(
    Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" |
        Where-Object { $_.CommandLine -and $_.CommandLine -match "BotNexus\.Gateway" }
)

$wasRunning = $gatewayProcesses.Count -gt 0
if ($wasRunning) {
    Write-Host "Stopping running gateway process(es): $($gatewayProcesses.ProcessId -join ', ')"
    foreach ($process in $gatewayProcesses) {
        Stop-Process -Id $process.ProcessId -Force
    }
}

& $installScriptPath -InstallPath $resolvedInstallPath -PackagesPath $resolvedPackagesPath

$restartProcessId = $null
if ($wasRunning) {
    $gatewayDir = Join-Path $resolvedInstallPath "gateway"
    $gatewayDll = Join-Path $gatewayDir "BotNexus.Gateway.dll"
    if (-not (Test-Path -LiteralPath $gatewayDll)) {
        throw "Gateway restart requested, but $gatewayDll was not found."
    }

    $started = Start-Process -FilePath "dotnet" -ArgumentList "`"$gatewayDll`"" -WorkingDirectory $gatewayDir -PassThru
    $restartProcessId = $started.Id
}

Write-Host ""
Write-Host "Update complete."
Write-Host "InstallPath: $resolvedInstallPath"
Write-Host "PackagesPath: $resolvedPackagesPath"
if ($wasRunning) {
    Write-Host "Gateway was running before update: yes"
    Write-Host "Gateway restarted with PID: $restartProcessId"
}
else {
    Write-Host "Gateway was running before update: no"
}
