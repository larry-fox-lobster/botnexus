# Leela decision inbox — auto-update design review

## Decision

For issue #91 auto-update:
- place `UpdateCheckService` in `BotNexus.Gateway`, not `BotNexus.Gateway.Api`
- expose update state via a dedicated `GET /api/gateway/update/status` endpoint
- require explicit `gateway.autoUpdate.cliPath` and `gateway.autoUpdate.sourcePath` config when enabled
- keep `target` resolved from `BOTNEXUS_HOME` / `BotNexusHome`
- return `202 Accepted`, then delay `StopApplication()` by 2 seconds after spawning updater

## Rationale

This keeps runtime concerns in the gateway core, keeps the API assembly thin, and avoids brittle path inference for self-update. Separate update status polling is cleaner than overloading `/api/gateway/info`, because info is currently loaded once while update state is dynamic.

## Important finding

Current `BotNexus.Cli` `update` command does **not** wait on the gateway health endpoint to drop before continuing. It builds first, then uses PID-based `StopAsync`. Wave 1 can still ship without CLI changes because the updater reaches stop late enough that the API-triggered delayed shutdown should already be happening or complete.
