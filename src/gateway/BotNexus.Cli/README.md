# BotNexus.Cli

> BotNexus platform CLI for local and remote configuration validation.

## Overview

This package is a command-line tool for managing BotNexus configuration, agents, and platform settings. It provides an alternative to editing `~/.botnexus/config.json` manually. The CLI can be run from source or installed as a .NET global tool.

## Running the CLI

### From Source

```powershell
dotnet run --project src/gateway/BotNexus.Cli -- <command>
```

### As a Global Tool

```bash
# Install
dotnet tool install --global --add-source <nupkg-path> BotNexus.Cli

# Run
botnexus <command>
```

## Commands

### `botnexus init`

Initialize `~/.botnexus/` directory structure with default configuration.

```powershell
botnexus init
```

Creates:
- `config.json` — Platform configuration with default agent and provider
- `auth.json` — Empty credentials store (filled on first OAuth)
- `agents/` — Agent workspace directories
- `sessions/` — Session history directory
- `logs/` — Daily log files directory

### `botnexus validate`

Validate `config.json` against the schema.

```powershell
# Validate local config
botnexus validate

# Validate config at a running gateway
botnexus validate --remote http://localhost:5005
```

Checks for:
- Valid JSON syntax
- Required fields (gateway, agents, providers)
- Agent-provider references
- Valid provider configurations

### `botnexus agent list`

List all configured agents.

```powershell
botnexus agent list
```

Output:

```
Agent ID: assistant
  Provider: copilot
  Model: gpt-4.1
  Enabled: True

Agent ID: coder
  Provider: openai
  Model: gpt-4-turbo
  Enabled: True
```

### `botnexus agent add`

Add a new agent to the configuration.

```powershell
botnexus agent add my-agent \
  --provider copilot \
  --model gpt-4.1 \
  --enabled
```

Options:

| Option | Description | Default |
|--------|-------------|---------|
| `--provider` | LLM provider name | (required) |
| `--model` | Model identifier | (required) |
| `--enabled` | Enable agent immediately | off |

### `botnexus agent remove`

Remove an agent from the configuration.

```powershell
botnexus agent remove my-agent
```

The agent is deleted immediately from `config.json`.

### `botnexus config get`

Get a configuration value by dotted path.

```powershell
botnexus config get gateway.listenUrl
# Output: http://localhost:5005

botnexus config get agents.assistant.model
# Output: gpt-4.1
```

### `botnexus config set`

Set a configuration value by dotted path.

```powershell
botnexus config set gateway.listenUrl http://localhost:8080

botnexus config set agents.assistant.enabled true
```

The configuration is saved immediately to `config.json` and watched by running gateways (hot-reload).

### Global Options

All commands support:

| Option | Description |
|--------|-------------|
| `--verbose` | Print detailed output and stack traces on error |
| `--home` | Override home directory (default: `~/.botnexus`) |

Example:

```powershell
botnexus agent list --verbose
```

## Environment Variables

| Variable | Description |
|----------|-------------|
| `BOTNEXUS_HOME` | Override home directory (same as `--home`) |

Example:

```powershell
$env:BOTNEXUS_HOME = "C:\custom\botnexus"
botnexus init
```

## Common Workflows

### 1. Initial Setup

```powershell
# Initialize home directory
botnexus init

# Add Copilot provider (requires GitHub Copilot subscription)
botnexus config set providers.copilot.apiKey auth:copilot
botnexus config set providers.copilot.baseUrl https://api.githubcopilot.com

# Create an agent
botnexus agent add assistant --provider copilot --model gpt-4.1 --enabled

# Validate
botnexus validate
```

### 2. Add a New Model

```powershell
# Add OpenAI provider
botnexus config set providers.openai.apiKey auth:openai
botnexus config set providers.openai.baseUrl https://api.openai.com/v1

# Create an agent using OpenAI
botnexus agent add gpt4 --provider openai --model gpt-4-turbo --enabled

# Verify
botnexus agent list
botnexus validate
```

### 3. Test Configuration at Running Gateway

```powershell
# Start gateway in one terminal
.\scripts\start-gateway.ps1

# In another terminal, validate against running gateway
botnexus validate --remote http://localhost:5005

# Or check specific config values
botnexus config get gateway.listenUrl
botnexus agent list
```

### 4. Reconfigure Dynamically

```powershell
# Change an agent's model
botnexus config set agents.assistant.model gpt-4-turbo

# Disable an agent
botnexus config set agents.coder.enabled false

# Gateway watches and picks up changes (no restart needed for these)
```

## Error Handling

The CLI returns exit codes:

- `0` — Success
- `1` — General error (config not found, invalid JSON, etc.)
- `2` — Validation error (schema violation, missing required fields)
- `3` — Command error (wrong arguments, unknown agent, etc.)

Example:

```powershell
botnexus validate
if ($LASTEXITCODE -ne 0) {
    Write-Error "Configuration is invalid!"
}
```

## Exit Code Reference

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | File not found or I/O error |
| 2 | Configuration validation failed |
| 3 | Command parsing or execution failed |

## Troubleshooting

### "Home directory not found"

**Solution:** Run `botnexus init` to create the directory structure.

### "Config is invalid"

**Solution:** Run `botnexus validate --verbose` to see detailed errors, then fix manually in `config.json` or use `botnexus config set` to correct values.

### "Agent not found"

**Solution:** List available agents with `botnexus agent list`, then use the correct agent ID.

### "Provider not configured"

**Solution:** Add the provider config with `botnexus config set` or edit `config.json` manually.

## Further Reading

- [Development Loop](../../docs/dev-loop.md) — Using the CLI in development workflow
- [Configuration Guide](../../docs/configuration.md) — Full config.json reference
- [Getting Started](../../docs/getting-started-dev.md) — First-time setup
