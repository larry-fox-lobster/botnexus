[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Path $PSScriptRoot -Parent
$commonScript = Join-Path $PSScriptRoot "common.ps1"
. $commonScript
$artifactsRoot = Join-Path $repoRoot "artifacts"
$stagingRoot = Join-Path $artifactsRoot "_staging"
$packageWorkRoot = Join-Path $artifactsRoot "_package"
$packageVersion = Resolve-Version

$components = @(
    @{ Id = "BotNexus.Gateway"; Project = "src\BotNexus.Gateway\BotNexus.Gateway.csproj" },
    @{ Id = "BotNexus.Cli"; Project = "src\BotNexus.Cli\BotNexus.Cli.csproj" },
    @{ Id = "BotNexus.Providers.Copilot"; Project = "src\BotNexus.Providers.Copilot\BotNexus.Providers.Copilot.csproj" },
    @{ Id = "BotNexus.Providers.OpenAI"; Project = "src\BotNexus.Providers.OpenAI\BotNexus.Providers.OpenAI.csproj" },
    @{ Id = "BotNexus.Providers.Anthropic"; Project = "src\BotNexus.Providers.Anthropic\BotNexus.Providers.Anthropic.csproj" },
    @{ Id = "BotNexus.Channels.Discord"; Project = "src\BotNexus.Channels.Discord\BotNexus.Channels.Discord.csproj" },
    @{ Id = "BotNexus.Channels.Slack"; Project = "src\BotNexus.Channels.Slack\BotNexus.Channels.Slack.csproj" },
    @{ Id = "BotNexus.Channels.Telegram"; Project = "src\BotNexus.Channels.Telegram\BotNexus.Channels.Telegram.csproj" },
    @{ Id = "BotNexus.Tools.GitHub"; Project = "src\BotNexus.Tools.GitHub\BotNexus.Tools.GitHub.csproj" }
)

if (-not (Test-Path -LiteralPath $artifactsRoot)) {
    New-Item -ItemType Directory -Path $artifactsRoot | Out-Null
}

foreach ($tempPath in @($stagingRoot, $packageWorkRoot)) {
    if (Test-Path -LiteralPath $tempPath) {
        Remove-Item -LiteralPath $tempPath -Recurse -Force
    }
    New-Item -ItemType Directory -Path $tempPath | Out-Null
}

$results = New-Object System.Collections.Generic.List[object]

foreach ($component in $components) {
    $componentId = [string]$component.Id
    $projectPath = Join-Path $repoRoot ([string]$component.Project)
    $publishPath = Join-Path $stagingRoot $componentId
    $workPath = Join-Path $packageWorkRoot $componentId
    $nupkgPath = Join-Path $artifactsRoot "$componentId.nupkg"
    $zipPath = Join-Path $artifactsRoot "$componentId.zip"

    Write-Host "Publishing $componentId..."
    dotnet publish $projectPath --configuration Release --output $publishPath --nologo --verbosity minimal --tl:off /p:Version=$packageVersion /p:InformationalVersion=$packageVersion
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $componentId"
    }

    if (Test-Path -LiteralPath $workPath) {
        Remove-Item -LiteralPath $workPath -Recurse -Force
    }
    New-Item -ItemType Directory -Path $workPath | Out-Null

    Copy-Item -Path (Join-Path $publishPath "*") -Destination $workPath -Recurse -Force

    $nuspecContent = @"
<?xml version="1.0"?>
<package>
  <metadata>
    <id>$componentId</id>
    <version>$packageVersion</version>
    <authors>BotNexus</authors>
    <description>Published binaries for $componentId.</description>
  </metadata>
</package>
"@
    Set-Content -LiteralPath (Join-Path $workPath "$componentId.nuspec") -Value $nuspecContent -Encoding UTF8

    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }
    if (Test-Path -LiteralPath $nupkgPath) {
        Remove-Item -LiteralPath $nupkgPath -Force
    }

    Compress-Archive -Path (Join-Path $workPath "*") -DestinationPath $zipPath -Force
    Move-Item -LiteralPath $zipPath -Destination $nupkgPath -Force

    $packageInfo = Get-Item -LiteralPath $nupkgPath
    $results.Add([pscustomobject]@{
            Package = $packageInfo.Name
            SizeMB  = [Math]::Round($packageInfo.Length / 1MB, 2)
            Path    = $packageInfo.FullName
        })
}

Write-Host ""
Write-Host "Created $($results.Count) package(s) in $artifactsRoot"
$results | Sort-Object Package | Format-Table -AutoSize

foreach ($tempPath in @($stagingRoot, $packageWorkRoot)) {
    if (Test-Path -LiteralPath $tempPath) {
        Remove-Item -LiteralPath $tempPath -Recurse -Force
    }
}
