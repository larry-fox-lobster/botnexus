---
id: improvement-memory-indexing
title: "Improvement: Memory Indexing & Backfill"
type: improvement
priority: high
status: done
created: 2026-07-26
---

# Improvement: Memory Indexing & Backfill

**Status:** done
**Priority:** high
**Created:** 2026-07-26

## Problem

Memory infrastructure (SqliteMemoryStore, MemoryIndexer, FTS5 search tools) was fully built but `MemoryIndexer` was never registered as a hosted service. All agents had empty memory databases.

## Fix

### 1. Hosted Service Registration
Added `services.AddHostedService<MemoryIndexer>()` to `GatewayServiceCollectionExtensions.cs`.

**Commit:** `9ac04f6`

### 2. CLI Backfill Command
Added `botnexus memory backfill [--agent <id>]` command that iterates all sessions in the store and indexes them using the same logic as the live indexer. Idempotent — safe to run alongside the live service.

**Commit:** `23f4ae7`
**Files:** `BotNexus.Cli/Commands/MemoryCommands.cs`

## Verification

Event pipeline confirmed correct:
- `StreamingSessionHelper` fires `Closed` after each completed agent response
- `SessionCleanupService` fires `Expired` on idle timeout
- `MemoryIndexer` subscribes to both, indexes user/assistant turn pairs
- Deduplication via `indexedTurns` tracking per session
