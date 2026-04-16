<#
.SYNOPSIS
    Builds a JSON index of all design-spec.md files in the planning directory.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'SilentlyContinue'
$planningRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

function Parse-DesignSpec {
    param([string]$SpecPath, [string]$FolderName)

    $content = Get-Content -Path $SpecPath -Raw -Encoding UTF8
    $lines = Get-Content -Path $SpecPath -Encoding UTF8
    $meta = @{}

    # Try YAML frontmatter
    if ($content -match '(?s)\A---\r?\n(.*?)\r?\n---') {
        $yamlBlock = $Matches[1]
        foreach ($line in $yamlBlock -split '\r?\n') {
            if ($line -match '^\s*(\w[\w_]*)\s*:\s*"?(.*?)"?\s*$') {
                $key = $Matches[1].ToLower()
                $val = $Matches[2].Trim('"').Trim("'")
                if ($val -ne '') { $meta[$key] = $val }
            }
        }
    }

    # Fallback: parse markdown bold patterns like **Status:** done
    foreach ($line in $lines) {
        if ($line -match '^\*\*(\w+)\*\*\s*:\s*(.+)$') {
            $key = $Matches[1].ToLower()
            $val = $Matches[2].Trim()
            if (-not $meta.ContainsKey($key)) { $meta[$key] = $val }
        }
    }

    # Infer type from folder name prefix
    $type = $meta['type']
    if (-not $type) {
        if ($FolderName -match '^(bug|feature|improvement|process)-') {
            $type = $Matches[1]
        }
    } else {
        $type = $type.ToLower()
    }

    # Infer title from first # heading
    $title = $meta['title']
    if (-not $title) {
        foreach ($line in $lines) {
            if ($line -match '^#\s+(.+)$') {
                $title = $Matches[1].Trim()
                break
            }
        }
    }
    # Strip common prefixes
    if ($title) {
        $title = $title -replace '^(Design Spec|Bug|Feature|Improvement):\s*', ''
    }

    $id = $meta['id']
    if (-not $id) { $id = $FolderName }

    $priority = if ($meta['priority']) { $meta['priority'].ToLower() } else { $null }
    $status   = if ($meta['status'])   { $meta['status'].ToLower() }   else { $null }
    $created  = if ($meta['created'])  { $meta['created'] }           else { $null }

    $researchPath = Join-Path (Split-Path $SpecPath -Parent) 'research.md'
    $hasResearch = Test-Path $researchPath

    return [PSCustomObject]@{
        id          = $id
        type        = $type
        priority    = $priority
        status      = $status
        created     = $created
        title       = $title
        hasResearch = $hasResearch
    }
}

function Get-SortKey {
    param([PSCustomObject]$Item)

    $typeOrder = @{ 'bug' = 0; 'feature' = 1; 'improvement' = 2; 'process' = 3 }
    $prioOrder = @{ 'critical' = 0; 'high' = 1; 'medium' = 2; 'low' = 3 }

    $t = if ($Item.type -and $typeOrder.ContainsKey($Item.type)) { $typeOrder[$Item.type] } else { 4 }
    $p = if ($Item.priority -and $prioOrder.ContainsKey($Item.priority)) { $prioOrder[$Item.priority] } else { 4 }

    return @($t, $p)
}

# Collect specs
$active = @()
$archived = @()

# Active: top-level folders
foreach ($dir in Get-ChildItem -Path $planningRoot -Directory | Where-Object { $_.Name -ne 'archived' }) {
    $spec = Join-Path $dir.FullName 'design-spec.md'
    if (Test-Path $spec) {
        $active += Parse-DesignSpec -SpecPath $spec -FolderName $dir.Name
    }
}

# Archived
$archivedDir = Join-Path $planningRoot 'archived'
if (Test-Path $archivedDir) {
    foreach ($dir in Get-ChildItem -Path $archivedDir -Directory) {
        $spec = Join-Path $dir.FullName 'design-spec.md'
        if (Test-Path $spec) {
            $archived += Parse-DesignSpec -SpecPath $spec -FolderName $dir.Name
        }
    }
}

# Sort
$active = @($active | Sort-Object { $k = Get-SortKey $_; $k[0] * 10 + $k[1] })
$archived = @($archived | Sort-Object { $k = Get-SortKey $_; $k[0] * 10 + $k[1] })

# Output
$result = [ordered]@{
    generated = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    active    = $active
    archived  = $archived
}

ConvertTo-Json -InputObject $result -Depth 4
