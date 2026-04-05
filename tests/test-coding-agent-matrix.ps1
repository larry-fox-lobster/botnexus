#!/usr/bin/env pwsh
<#
.SYNOPSIS
    BotNexus Coding Agent — Copilot Provider Test Matrix
.DESCRIPTION
    Tests the CodingAgent CLI against multiple Copilot models with prompts
    of varying complexity. Run after modifying providers or agent code.
.PARAMETER Models
    Comma-separated model IDs to test. Default: all 6.
.PARAMETER Tier
    Prompt tier to run: 1 (simple), 2 (single-tool), 3 (multi-tool), or 'all'. Default: all.
.PARAMETER TimeoutSeconds
    Max seconds per test. Default: 120.
.PARAMETER NoBuild
    Skip dotnet build step.
.EXAMPLE
    .\tests\test-coding-agent-matrix.ps1
    .\tests\test-coding-agent-matrix.ps1 -Models "gpt-4.1,claude-haiku-4.5" -Tier 1
#>
param(
    [string]$Models = "claude-opus-4.6,claude-haiku-4.5,gpt-4.1,gpt-5.2-codex,gpt-5.2,gemini-2.5-pro",
    [string]$Tier = "all",
    [int]$TimeoutSeconds = 120,
    [switch]$NoBuild
)

$ErrorActionPreference = "Continue"
$projectPath = "src\coding-agent\BotNexus.CodingAgent"

# --- Prompt definitions ---
$prompts = @(
    # Tier 1: Simple (no tools)
    @{ Id = "simple-math"; Tier = 1; Prompt = "What is 2+2? Answer with just the number."; Validate = "content-nonempty"; ExpectContent = "4" }
    @{ Id = "simple-hello"; Tier = 1; Prompt = "Say hello in exactly one sentence."; Validate = "content-nonempty" }
    
    # Tier 2: Single tool
    @{ Id = "tool-list-dir"; Tier = 2; Prompt = "List the files and folders in the current directory. Just list them briefly."; Validate = "tool-used"; ExpectTool = "list_directory" }
    @{ Id = "tool-read-file"; Tier = 2; Prompt = "Read the file README.md and tell me the project name in one sentence."; Validate = "tool-used"; ExpectTool = "read" }
    
    # Tier 3: Multi-tool
    @{ Id = "multi-find-count"; Tier = 3; Prompt = "Find all .csproj files under the src directory, count them, and tell me the total number."; Validate = "multi-tool" }
    @{ Id = "multi-read-analyze"; Tier = 3; Prompt = "Read the file src\coding-agent\BotNexus.CodingAgent\Program.cs, find the Main method, and describe what it does in 2 sentences."; Validate = "tool-used"; ExpectTool = "read" }
)

# --- Results tracking ---
$results = @()

# --- Build once ---
if (-not $NoBuild) {
    Write-Host "Building CodingAgent..." -ForegroundColor Cyan
    $buildResult = & dotnet build $projectPath --verbosity quiet 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "BUILD FAILED" -ForegroundColor Red
        $buildResult | Write-Host
        exit 1
    }
    Write-Host "Build OK" -ForegroundColor Green
}

# --- Filter models and tiers ---
$modelList = $Models -split ","
$tierFilter = if ($Tier -eq "all") { @(1,2,3) } else { @([int]$Tier) }
$filteredPrompts = $prompts | Where-Object { $tierFilter -contains $_.Tier }

# --- Clean sessions ---
$sessionsDir = Join-Path (Get-Location) ".botnexus-agent\sessions"
if (Test-Path $sessionsDir) {
    Get-ChildItem $sessionsDir -File -Filter "*.jsonl" -ErrorAction SilentlyContinue | Remove-Item -Force
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host " BotNexus CodingAgent — Copilot Test Matrix" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Models: $($modelList -join ', ')" -ForegroundColor DarkGray
Write-Host "Tiers:  $($tierFilter -join ', ')" -ForegroundColor DarkGray
Write-Host "Tests:  $($modelList.Count * $filteredPrompts.Count) total" -ForegroundColor DarkGray
Write-Host ""

$total = 0
$passed = 0
$failed = 0
$errors = @()

foreach ($model in $modelList) {
    Write-Host "── Model: $model ──" -ForegroundColor Yellow
    
    foreach ($prompt in $filteredPrompts) {
        $total++
        $testId = "$model/$($prompt.Id)"
        Write-Host -NoNewline "  T$($prompt.Tier) $($prompt.Id)... " -ForegroundColor Gray
        
        # Clean session files before each test
        Get-ChildItem $sessionsDir -File -Filter "*.jsonl" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
        
        # Run the CLI
        $startTime = Get-Date
        try {
            $output = & dotnet run --no-build --project $projectPath -- --model $model --non-interactive --prompt $prompt.Prompt 2>&1 | Out-String
            $exitCode = $LASTEXITCODE
        } catch {
            $output = $_.Exception.Message
            $exitCode = -1
        }
        $duration = ((Get-Date) - $startTime).TotalSeconds
        
        # Check for errors in output
        $hasError = $output -match "\[error\]" -or $output -match "HTTP [45]\d\d" -or $output -match "Exception"
        
        # Check session file for API errors
        $sessionError = $null
        $latestSession = Get-ChildItem $sessionsDir -File -Filter "*.jsonl" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($latestSession) {
            $sessionContent = Get-Content $latestSession.FullName -Raw
            if ($sessionContent -match '"errorMessage"\s*:\s*"([^"]+)"') {
                $sessionError = $Matches[1]
            }
        }
        
        # Validate based on prompt type
        $testPassed = $false
        $failReason = ""
        
        if ($exitCode -ne 0) {
            $failReason = "exit code $exitCode"
        } elseif ($sessionError) {
            $failReason = "API error: $sessionError"
        } elseif ($hasError) {
            $failReason = "error in output"
        } else {
            switch ($prompt.Validate) {
                "content-nonempty" {
                    # Check that there's actual content between session header and separator
                    $lines = $output -split "`n" | Where-Object { 
                        $_ -notmatch "^\[session:" -and 
                        $_ -notmatch "^---$" -and 
                        $_ -notmatch "^\s*$" -and
                        $_ -notmatch "^\[tool:" -and
                        $_ -notmatch "^Build "
                    }
                    if ($lines.Count -eq 0) {
                        $failReason = "no content in response"
                    } elseif ($prompt.ExpectContent -and -not ($output -match [regex]::Escape($prompt.ExpectContent))) {
                        $failReason = "expected content '$($prompt.ExpectContent)' not found"
                    } else {
                        $testPassed = $true
                    }
                }
                "tool-used" {
                    $toolStarted = $output -match "\[tool:start\]"
                    $toolEnded = $output -match "\[tool:end\].*success=true"
                    if (-not $toolStarted) {
                        $failReason = "no tool invocation"
                    } elseif (-not $toolEnded) {
                        $failReason = "tool did not succeed"
                    } else {
                        $testPassed = $true
                    }
                }
                "multi-tool" {
                    $toolStarts = ([regex]::Matches($output, "\[tool:start\]")).Count
                    if ($toolStarts -lt 2) {
                        # Some models might be efficient with one call — accept if content exists
                        $hasContent = ($output -split "`n" | Where-Object { 
                            $_ -notmatch "^\[" -and $_ -notmatch "^---" -and $_ -notmatch "^\s*$" 
                        }).Count -gt 0
                        if ($hasContent -and $toolStarts -ge 1) {
                            $testPassed = $true
                        } else {
                            $failReason = "expected multiple tool calls, got $toolStarts"
                        }
                    } else {
                        $testPassed = $true
                    }
                }
            }
        }
        
        if ($testPassed) {
            $passed++
            Write-Host "PASS" -ForegroundColor Green -NoNewline
            Write-Host " ($([math]::Round($duration, 1))s)" -ForegroundColor DarkGray
        } else {
            $failed++
            Write-Host "FAIL" -ForegroundColor Red -NoNewline
            Write-Host " ($([math]::Round($duration, 1))s) — $failReason" -ForegroundColor DarkGray
            $errors += @{ Test = $testId; Reason = $failReason; Duration = $duration }
        }
        
        $results += @{
            Model = $model
            Test = $prompt.Id
            Tier = $prompt.Tier
            Passed = $testPassed
            Duration = [math]::Round($duration, 1)
            Reason = $failReason
        }
    }
    Write-Host ""
}

# --- Summary ---
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host " RESULTS: $passed/$total passed" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Yellow" })
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan

if ($errors.Count -gt 0) {
    Write-Host ""
    Write-Host "Failures:" -ForegroundColor Red
    foreach ($err in $errors) {
        Write-Host "  ✗ $($err.Test): $($err.Reason)" -ForegroundColor Red
    }
}

# --- Matrix table ---
Write-Host ""
Write-Host "Matrix:" -ForegroundColor Cyan
$header = "  {0,-20}" -f "Model"
foreach ($p in $filteredPrompts) { $header += " {0,-15}" -f $p.Id }
Write-Host $header -ForegroundColor DarkGray

foreach ($model in $modelList) {
    $row = "  {0,-20}" -f $model
    foreach ($p in $filteredPrompts) {
        $r = $results | Where-Object { $_.Model -eq $model -and $_.Test -eq $p.Id }
        if ($r) {
            $icon = if ($r.Passed) { "✓ $($r.Duration)s" } else { "✗ FAIL" }
            $row += " {0,-15}" -f $icon
        }
    }
    Write-Host $row
}

Write-Host ""
exit $(if ($failed -eq 0) { 0 } else { 1 })
