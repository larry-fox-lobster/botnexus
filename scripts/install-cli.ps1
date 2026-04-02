[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Path $PSScriptRoot -Parent
$toolsPath = Join-Path $repoRoot "artifacts\tools"

dotnet pack (Join-Path $repoRoot "src\BotNexus.Cli") -c Release -o $toolsPath

$resolvedToolsPath = [System.IO.Path]::GetFullPath($toolsPath)
$isInstalled = dotnet tool list --global | Select-String -Pattern '^\s*botnexus\.cli\s+'

if ($null -ne $isInstalled) {
    dotnet tool update --global --add-source $resolvedToolsPath BotNexus.Cli
}
else {
    dotnet tool install --global --add-source $resolvedToolsPath BotNexus.Cli
}

Write-Host "✅ botnexus CLI installed. Run 'botnexus --help' to get started."
