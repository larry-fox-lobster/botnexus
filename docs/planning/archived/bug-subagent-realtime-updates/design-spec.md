---
id: bug-subagent-realtime-updates
title: "Bug: Sub-Agent Real-Time Updates Not Reaching WebUI"
type: bug
priority: medium
status: done
created: 2026-07-26
---

# Bug: Sub-Agent Real-Time Updates Not Reaching WebUI

**Status:** done
**Priority:** medium
**Created:** 2026-07-26

## Problem

Sub-agents spawned by an agent did not appear in the WebUI sidebar until the page was refreshed. The JS client had correct handlers (`SubAgentSpawned`, `SubAgentCompleted`, etc.) but no server-side component was sending those SignalR events.

## Root Cause

`DefaultSubAgentManager` published lifecycle events to `IActivityBroadcaster` (the activity WebSocket feed), but nothing bridged those events to SignalR groups where the web UI listens.

## Fix

Added `SubAgentSignalRBridge` — a `BackgroundService` that:
1. Subscribes to `IActivityBroadcaster`
2. Filters for `SubAgentSpawned`/`Completed`/`Failed`/`Killed` activity types
3. Forwards them via `IHubContext<GatewayHub>` to `session:{parentSessionId}` group

**Commit:** `2d8db1c`
**Files:** `BotNexus.Gateway/Hubs/SubAgentSignalRBridge.cs`, `GatewayServiceCollectionExtensions.cs`

Zero client-side changes required — the JS handlers were already correct.
