---
id: improvement-cli-multi-instance
title: "Improvement: CLI --home flag for multi-instance gateway management"
type: improvement
priority: medium
status: draft
created: 2026-04-28
author: rusty
---

# Improvement: CLI --home flag for multi-instance gateway management

**Type**: Improvement  
**Priority**: Medium  
**Status**: Draft

## Problem

The CLI's `gateway start` hardcodes `~/.botnexus` as the BotNexus home directory via `PlatformConfigLoader.DefaultConfigPath` and `GatewayProcessManager` (which writes `~/.botnexus/gateway.pid`). This means:

- Running two instances (prod + dev) from the same machine is not possible via the CLI alone
- The sync script (`botnexus-sync.ps1`) has to manage process launch directly rather than delegating to the CLI
- Developers and ops can't use `botnexus gateway status` to check a non-default instance

## Desired Behaviour

```bash
botnexus gateway start --home ~/.botnexus-dev --port 5006 --path ~/projects/botnexus
botnexus gateway stop  --home ~/.botnexus-dev
botnexus gateway status --home ~/.botnexus-dev
```

Each instance has its own `gateway.pid`, config, extensions, and logs — all scoped to the specified home.

## Impact

- `GatewayProcessManager` — PID file path must use the configured home, not a hardcoded constant
- `PlatformConfigLoader` — needs to accept a home path override
- `GatewayCommand` — expose `--home` option on all subcommands
- `ServeCommand.DeployExtensions` — must deploy to the specified home
- `BotNexusHome` class — already accepts a path in constructor, just needs to be wired up

## Migration

No breaking change — `--home` defaults to `~/.botnexus` if not specified. Existing users see no difference.

## Done When

- `botnexus gateway start --home <path> --port <n>` starts a gateway using that home
- `botnexus gateway stop --home <path>` stops it
- `botnexus gateway status --home <path>` reports its state
- `botnexus-sync.ps1` can be simplified to delegate entirely to the CLI for both instances
