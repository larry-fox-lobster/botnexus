---
id: bug-subagent-spawn-path
title: "Sub-Agent Spawn Fails on Windows Due to :: in Workspace Path"
type: bug
priority: high
status: draft
created: 2026-04-12
updated: 2026-04-12
author: nova
tags: [sub-agent, windows, path, isolation]
ddd_types: [AgentId, SubAgentArchetype]
---

# Design Spec: Sub-Agent Spawn Fails on Windows Due to :: in Workspace Path

**Type**: Bug
**Priority**: High (blocks all sub-agent delegation - core workflow)
**Status**: Draft
**Author**: Nova (via Jon)

## Problem

Spawning a sub-agent fails immediately with a Windows filesystem error. The sub-agent workspace path is constructed using `::` as a separator, which is illegal in Windows paths.

### Error

```
The filename, directory name, or volume label syntax is incorrect.
: 'C:\Users\jobullen\.botnexus\agents\nova::subagent::writer::935cf6fc55f249b6ac45daf51d457c3a'
```

### Repro Steps

1. From Nova's main session, call `spawn_subagent` with any task
2. Observe immediate failure before the sub-agent starts

### Observed Pattern

The path appears to be constructed as:

```
{agent_workspace_root}::{role}::{archetype}::{guid}
```

For example:
```
C:\Users\jobullen\.botnexus\agents\nova::subagent::writer::935cf6fc55f249b6ac45daf51d457c3a
C:\Users\jobullen\.botnexus\agents\nova::subagent::writer::620bc4295c3649c4b9f91304631eda08
```

Windows does not allow `:` in file or directory names (except for drive letter prefixes like `C:`). This would likely work on Linux/macOS where `:` is legal in paths.

## Impact

- **All sub-agent spawning is broken** on Windows hosts
- Nova's core workflow depends on delegating complex/code work to sub-agents (per Jon's preference)
- Forces Nova to do everything inline, which is slower and burns main session context

## Requirements

### Must Have

1. Sub-agent workspace paths must be valid on all supported platforms (Windows, Linux, macOS)
2. Path separator must not use characters illegal in any OS (`::` is illegal on Windows)
3. Existing sub-agent workspace data (if any) should be migrated or gracefully handled

### Should Have

4. Sub-agent workspace paths should be human-readable (easy to find in file explorer)
5. Cleanup of sub-agent workspaces after completion (or configurable retention)

## Proposed Implementation

Replace `::` separator with a filesystem-safe alternative. Options:

### Option A: Nested directories (recommended)

```
C:\Users\jobullen\.botnexus\agents\nova\subagents\writer-935cf6fc\
```

Advantages:
- Clean hierarchy, easy to browse
- `subagents/` folder keeps them organized under the parent agent
- Short guid suffix (first 8 chars) keeps paths manageable

### Option B: Dash separator

```
C:\Users\jobullen\.botnexus\agents\nova--subagent--writer--935cf6fc55f249b6ac45daf51d457c3a\
```

Advantages: minimal code change (string replace)
Disadvantages: flat directory, long names, harder to browse

### Option C: Underscore separator

```
C:\Users\jobullen\.botnexus\agents\nova_subagent_writer_935cf6fc\
```

Similar trade-offs to Option B.

**Recommendation**: Option A - nested directories. It's the cleanest and most maintainable.

## Where to Fix

The path construction likely lives in the sub-agent spawning logic - wherever `spawn_subagent` resolves the workspace directory for the child agent. Look for string concatenation or interpolation that produces the `::` pattern.

Likely candidates (based on gateway architecture):
- `InProcessIsolationStrategy` or equivalent sub-agent isolation code
- Wherever `SubAgentArchetype` is used to construct workspace paths
- The `spawn_subagent` tool handler itself

## Edge Cases

| Case                                | Behavior                                                    |
|-------------------------------------|-------------------------------------------------------------|
| Multiple sub-agents same archetype  | GUID suffix ensures uniqueness                              |
| Sub-agent spawns its own sub-agent  | Path nesting should work naturally with Option A            |
| Cleanup on completion               | Separate concern, but nested dirs make cleanup easier       |
| Path length limit (Windows MAX_PATH)| Option A is shorter than current approach, so no regression |

## Testing Plan

1. Spawn a sub-agent with archetype `writer` on Windows - verify workspace created successfully
2. Spawn a sub-agent with archetype `researcher` - verify different workspace
3. Spawn two sub-agents with same archetype - verify unique workspaces
4. Verify sub-agent can read/write files in its workspace
5. Verify sub-agent completion event fires correctly
6. Test on Linux if available - verify no regression on platforms where `::` was working

## Success Criteria

1. `spawn_subagent` succeeds on Windows without filesystem errors
2. Sub-agent gets a valid, writable workspace directory
3. No regression on Linux/macOS

## Repro History

| Date       | Session                          | Details                                                                                              |
|------------|----------------------------------|------------------------------------------------------------------------------------------------------|
| 2026-04-12 | 5b0cea38454f4910a46fedf1ffcc845f | Two consecutive spawn attempts failed. Archetypes: writer. GUIDs: 935cf6fc..., 620bc429... Both same error. |
