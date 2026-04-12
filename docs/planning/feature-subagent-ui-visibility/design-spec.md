---
id: feature-subagent-ui-visibility
title: Sub-Agent Session Visibility in WebUI
type: feature
priority: medium
status: draft
created: 2026-04-10
updated: 2026-04-12
author: nova
depends_on: [bug-session-switching-ui]
tags: [webui, subagent, session, ux]
ddd_types: [Session, SessionId, SessionType, AgentId]
---

## Post-DDD Architecture Note (2026-04-12)

This spec now uses domain types from BotNexus.Domain:
- `SessionType.AgentSubAgent` - sub-agent sessions are identified by this enum value
- `Session` domain model with `ParentSessionId` property
- Subscribe-all connection model means sub-agent sessions are automatically subscribed if visible

# Design Spec: Sub-Agent Session Visibility in WebUI

Type: Feature
Priority: Medium (blocked by bug-session-switching-ui which is HIGH)
Status: Draft
Author: Nova (via Jon)

## Overview
Surface sub-agent sessions in the BotNexus WebUI so users can see running/completed sub-agents and click into their sessions to observe progress in real-time.

## Requirements

### Must Have
1. Sub-agent sessions appear in the WebUI sessions/channels panel
2. Sub-agent sessions are visually distinguished from main sessions (icon, label, or nesting)
3. Clicking a sub-agent session opens its conversation in the main canvas
4. Sub-agent status is visible: running, completed, failed
5. Real-time message streaming - as the sub-agent works, output appears live

### Should Have
6. Sub-agents grouped/nested under their parent session
7. Completed sub-agents can be collapsed but remain browsable
8. Sub-agent name (from spawn parameters) displayed as the session label
9. Duration and token usage shown for completed sub-agents

### Nice to Have
10. Ability to send steering messages to a running sub-agent directly from its session view
11. Ability to kill a sub-agent from the UI (not just via the parent agent tools)
12. Sub-agent session search/filter in the sessions panel
13. Badge/notification on the parent session when a sub-agent completes

## Proposed Implementation

### Session Panel Updates
- Query sessions via `ISessionStore` for sessions where `SessionType == SessionType.AgentSubAgent`
- Group sub-agent sessions under their parent session using `Session.ParentSessionId`
- Show status via `SessionStatus` (Active, Sealed)
- Show sub-agent name from `Session` metadata

### Data Model

**Already in DDD refactoring:**
```csharp
public record Session(
    SessionId SessionId,
    AgentId AgentId,
    SessionType SessionType,      // SessionType.AgentSubAgent for sub-agents
    SessionStatus Status,
    SessionId? ParentSessionId,   // Links to parent session
    // ... other properties
);
```

No schema changes needed - this is part of the domain model.

### SignalR Integration
- With the subscribe-all model, the client is already subscribed to all visible sessions
- Sub-agent sessions can be filtered in/out of visibility via `SessionType` rules
- Messages stream in real-time to the client's per-session stores
- No additional subscription logic needed beyond visibility filtering

### Multi-Client Considerations
Per the channel-scoped session model (see bug-session-resumption spec), if the WebUI is open on multiple devices, sub-agent sessions should appear on all of them. The fan-out mechanism for the parent session should extend to sub-agent session visibility updates.

## Edge Cases
1. Parent session archived while sub-agent running: Sub-agent should complete normally; session still visible under orphaned group
2. Multiple sub-agents from same parent: All shown as siblings under the parent
3. Deeply nested sub-agents: If a sub-agent spawns its own sub-agent, maintain the tree hierarchy (but cap display depth at 2-3 levels)
4. Rapid sub-agent completion: Sub-agent finishes before user clicks into it - session should still be browsable with full history
5. Sub-agent session during gateway restart: Sub-agent sessions should also survive gateway restart (same resumption logic as main sessions, but lower priority)
6. Sub-agent spawned from cron session: Should still be visible but grouped differently (parent is a cron job, not a main session)

## Testing Plan
1. Spawn a sub-agent from main session. Verify it appears in the sessions panel
2. Click into the sub-agent session while it is running. Verify messages stream live
3. Wait for sub-agent to complete. Verify status updates to completed
4. Spawn multiple sub-agents. Verify all appear grouped under parent
5. Restart gateway with running sub-agent. Verify session state is preserved (after bug-session-resumption is fixed)
6. Open WebUI on two browsers. Verify both see the sub-agent sessions
7. Kill a running sub-agent from the parent session. Verify UI updates status to failed/killed

## Success Criteria
- User can see all active and recent sub-agents in the UI without asking the parent agent
- Clicking into a sub-agent session shows its full conversation with real-time updates
- Sub-agents are clearly distinguishable from main sessions
- No performance degradation from sub-agent session subscriptions

## Additional Requirement: Sub-Agent Tool Call Attribution (2026-04-10)

### New Must Have
11. Tool calls in the UI must be attributed to the correct agent/session. When a sub-agent runs a bash command, the UI must show it as the sub-agent's action, not the parent agent's.

### Updated Implementation Order
The original spec assumed sub-agent sessions were already in the database. They are NOT. Updated prerequisite chain:

Phase 0 (Backend - prerequisite):
- Persist sub-agent sessions to sessions table with parent_session_id and session_type = subagent
- Persist sub-agent conversation history to session_history table
- Ensure tool calls are recorded against the sub-agent session_id, not the parent

Phase 1 (Backend + UI):
- Fix bug-session-switching-ui (existing blocker)
- Add sub-agent sessions to the sessions panel query

Phase 2 (UI):
- Original spec requirements (visibility, real-time streaming, status indicators, etc.)

### New Bug: Tool Call Misattribution
When sub-agents run tool calls (bash, read, write, etc.), the WebUI currently shows these as the parent agent's actions. This is either:
a) Sub-agent tool calls leaking into parent session display, or
b) The UI showing host-level process activity without session context
Either way, this needs fixing as part of Phase 0.
