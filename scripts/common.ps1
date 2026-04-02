[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-Version {
    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
    if (-not [string]::IsNullOrWhiteSpace($env:BOTNEXUS_VERSION)) {
        return $env:BOTNEXUS_VERSION
    }

    $tag = git describe --tags --exact-match HEAD 2>$null
    if ($LASTEXITCODE -eq 0 -and $tag -match '^v(.+)$') {
        return $Matches[1]
    }

    $hash = (git rev-parse --short HEAD 2>$null)
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($hash)) {
        $hash = "unknown"
    }

    $status = git status --porcelain 2>$null
    $statusText = ($status | Out-String).Trim()
    $dirty = if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($statusText)) { ".dirty" } else { "" }
    return "0.0.0-dev.$hash$dirty"
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
}
