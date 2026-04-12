# Decision: Per-Agent File Permission Model — Design Review

**Date:** 2026-04-11
**Author:** Leela (Lead/Architect)
**Status:** Approved for implementation
**Design Spec:** [design-spec.md](../../../docs/planning/feature-tool-permission-model/design-spec.md)

## Context

Jon reported that agents can't read project files outside their workspace directory. All file tools use `PathUtils.ResolvePath()` which enforces strict containment to `~/.botnexus/agents/{name}/workspace`. Meanwhile, the shell tool has zero path validation — `cat /etc/passwd` works. We need per-agent, config-driven file access control.

## Key Design Decisions

### 1. Deny Overrides Allow — Always

Deny rules take absolute precedence. If a path matches both an allow rule and a deny rule, access is denied. No ambiguity.

### 2. Read/Write Separation

`FileAccessPolicy` has separate `AllowedReadPaths` and `AllowedWritePaths`. An agent can read a repository without being able to write to it. This is the most common real-world need (code review agents).

### 3. No Wildcards in v1

Only exact directory paths. Wildcards (`Q:\repos\*`) deferred to v2. Rationale: simpler to audit, harder to misconfigure, covers all current use cases.

### 4. Shell Tool Is Unsandboxable

Shell commands can access anything the OS user can. We set working directory, inject env vars (`BOTNEXUS_ALLOWED_READ`, `BOTNEXUS_ALLOWED_WRITE`), and document the limitation. True sandboxing requires container isolation (future work).

### 5. Zero Breaking Changes

`FileAccessPolicy` is nullable on `AgentDescriptor`. Null means workspace-only (current behavior). Existing tool constructors get an overload — no signature breaks.

## Wave Plan

### Wave 1: Abstractions + Config → Farnsworth

- `FileAccessPolicy` record in `BotNexus.Gateway.Abstractions/Security/`
- `FileAccessMode` enum in `BotNexus.Gateway.Abstractions/Security/`
- `IPathValidator` interface in `BotNexus.Gateway.Abstractions/Security/`
- `DefaultPathValidator` implementation in `BotNexus.Tools/Security/`
- `FileAccessPolicy?` property on `AgentDescriptor`
- Config deserialization for `fileAccess` JSON block

**Branch:** `feature/permission-model-abstractions`

### Wave 2: Tool Integration → Bender

Depends on: Wave 1 merged

- Add `IPathValidator` to all file tool constructors (read, write, edit, glob, grep, ls, watch_file)
- Replace `PathUtils.ResolvePath()` calls with `IPathValidator.ValidateAndResolve()`
- Update `DefaultAgentToolFactory.CreateTools()` to accept and pass `FileAccessPolicy`
- Update `InProcessIsolationStrategy.CreateAsync()` to wire `descriptor.FileAccessPolicy`
- Shell tool: inject allowed-path env vars into `ProcessStartInfo.Environment`

**Branch:** `feature/permission-model-tools`

### Wave 3: Tests → Hermes

Depends on: Wave 2 merged

- `DefaultPathValidator` unit tests: allow, deny, deny-overrides-allow, default workspace fallback, ~ expansion, relative path resolution, symlink traversal
- Per-tool integration tests: each tool rejects denied paths, allows permitted paths
- Config round-trip tests: `FileAccessPolicy` serializes/deserializes correctly
- End-to-end isolation test: agent with policy can read allowed repo, cannot write to it

**Branch:** `feature/permission-model-tests`

## Risk Assessment

| Risk | Severity | Mitigation |
|------|----------|------------|
| Shell bypasses all file permissions | Medium | Documented limitation; env vars for cooperative scripts; container isolation in v2 |
| Misconfigured deny rules lock agent out | Low | Default policy (null) preserves current workspace-only behavior |
| Symlink traversal bypasses checks | Low | Reuses existing `ResolveFinalTargetPath` — paths checked after resolution |
| Breaking existing tool constructors | Low | Overloaded constructors — existing signatures preserved |

## Acceptance Criteria

1. Agent with `allowedReadPaths: ["Q:\\repos\\botnexus"]` can read files in that repo
2. Same agent without `allowedWritePaths` for that repo cannot write to it
3. Agent with `deniedPaths: [".env"]` cannot read `.env` even if parent directory is allowed
4. Agent with no `fileAccess` config behaves identically to current behavior
5. All existing tests pass without modification
