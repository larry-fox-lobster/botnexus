# Development Workflow Guide

Quick reference for building, testing, and deploying BotNexus during development.

## Table of Contents

1. [Dev-Loop Script](#dev-loop-script)
2. [Build Process](#build-process)
3. [Testing](#testing)
4. [Common Tasks](#common-tasks)

---

## Dev-Loop Script

The `scripts/dev-loop.ps1` script automates build + test + Gateway startup for rapid local development.

### Usage

```powershell
# Build + test + start gateway on default port 5005
.\scripts\dev-loop.ps1

# Use a custom port
.\scripts\dev-loop.ps1 -Port 5100

# Watch source changes and restart gateway automatically
.\scripts\dev-loop.ps1 -Watch

# Fast loop for iterative checks (skip rebuild/test when already done)
.\scripts\dev-loop.ps1 -SkipBuild -SkipTests
```

### What It Does

**3-Step Development Loop:**

1. **Build solution** — runs `dotnet build BotNexus.slnx --nologo --tl:off`
2. **Run Gateway tests** — runs `dotnet test tests/BotNexus.Gateway.Tests --nologo --tl:off`
3. **Start Gateway** — runs `scripts/start-gateway.ps1` (or `dotnet watch ... run` with `-Watch`)

### Output

```
🔧 Building full solution...
✅ Build succeeded
🧪 Running Gateway tests...
✅ Tests passed
🚀 Starting Gateway API
   URL: http://localhost:5005
   WebUI: http://localhost:5005/webui

Press Ctrl+C to stop.
```

### Configuration

- **Port:** Defaults to `5005`, configurable via `-Port`
- **Environment:** Sets `ASPNETCORE_ENVIRONMENT=Development`
- **Gateway URL:** Sets `ASPNETCORE_URLS=http://localhost:<port>`

### Environment Variables

| Variable | Purpose | Default |
|----------|---------|---------|
| `BOTNEXUS_HOME` | BotNexus home directory | `~/.botnexus` |
| `ASPNETCORE_ENVIRONMENT` | ASP.NET environment | `Development` (set by script) |
| `ASPNETCORE_URLS` | Gateway listen URL | `http://localhost:5005` (set by script) |

---

## Build Process

### Manual Build Steps

**1. Build the entire solution:**
```powershell
dotnet build BotNexus.slnx --nologo --tl:off
```

**2. Run tests:**
```powershell
dotnet test BotNexus.slnx --nologo --tl:off
```

**3. Start the Gateway:**
```powershell
.\scripts\start-gateway.ps1
```

### Using dev-loop.ps1 (Recommended)

For a single command that does build + test + run:

```powershell
.\scripts\dev-loop.ps1
```

---

## Testing

### Run All Tests

```powershell
dotnet test BotNexus.slnx --nologo --tl:off
```

### Run Tests for a Specific Project

```powershell
dotnet test tests\BotNexus.AgentCore.Tests --nologo --tl:off
```

### Run a Specific Test

```powershell
dotnet test BotNexus.slnx --nologo --tl:off --filter "FullyQualifiedName~MyTestName"
```

### Watch Mode (Auto-Rerun on File Changes)

```powershell
dotnet watch test --project tests\BotNexus.Gateway.Tests
```

---

## Common Tasks

### Start Development Environment

```powershell
# Full build + test + start (recommended)
.\scripts\dev-loop.ps1

# Then in another terminal, open WebUI
start http://localhost:5005/webui
```

### View Gateway Logs

```powershell
# View recent logs (daily log files)
Get-Content $env:USERPROFILE\.botnexus\logs\botnexus-*.log -Tail 50

# Follow logs in real-time (requires PowerShell 7+)
Get-Content $env:USERPROFILE\.botnexus\logs\botnexus-*.log -Tail 50 -Wait
```

### Stop the Gateway

Press `Ctrl+C` in the terminal where the Gateway is running, or:

```powershell
# Find the process
Get-Process -Name "dotnet" | Where-Object { $_.CommandLine -like "*BotNexus*" }

# Stop by PID
Stop-Process -Id <PID>
```

### Clean Build (Remove All Artifacts)

```powershell
dotnet clean BotNexus.slnx
dotnet build BotNexus.slnx --nologo --tl:off
```

### Export OpenAPI Spec

```powershell
.\scripts\export-openapi.ps1
# Saves to docs/api/openapi.json
```

### Install Pre-Commit Hook

```powershell
.\scripts\install-pre-commit-hook.ps1
```

This builds and runs Gateway tests before each commit. Bypass with `git commit --no-verify`.

### Reset Configuration

```powershell
# Backup current config
Copy-Item $env:USERPROFILE\.botnexus\config.json $env:USERPROFILE\.botnexus\config.json.backup

# Delete and let BotNexus recreate on next run
Remove-Item $env:USERPROFILE\.botnexus\config.json
.\scripts\start-gateway.ps1
```

---

## Troubleshooting

### Port Already in Use

If port 5005 is already in use:

```powershell
# Find process using port 5005
netstat -ano | findstr :5005

# Use a different port
.\scripts\start-gateway.ps1 -Port 8080
```

### Extensions Not Loading

```powershell
# Verify extensions are in correct location
Get-ChildItem $env:USERPROFILE\.botnexus\extensions\
```

### OAuth Token Expired

```powershell
# Remove expired token — next request triggers re-auth
Remove-Item $env:USERPROFILE\.botnexus\tokens\copilot.json
```

---

## Next Steps

- **[Dev Loop Reference](dev-loop.md)** — Full dev loop with live testing and configuration
- **[API Reference](api-reference.md)** — REST API endpoints for agents and sessions
- **[Configuration Guide](configuration.md)** — Detailed configuration options
- **[Extension Development](extension-development.md)** — Build custom tools and providers
