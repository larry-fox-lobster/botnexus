---
id: bug-session-switching-ui
title: "Session Switching Broken During Active Agent Work"
type: bug
priority: high
status: draft
created: 2026-04-10
updated: 2026-04-10
author: nova
tags: [webui, session, ux, multi-agent, critical-ux]
---

# Design Spec: Session Switching Broken During Active Agent Work

## Overview

The WebUI conversation canvas does not properly switch when clicking a different agent's session while another agent is actively working. The previous agent's conversation and loading state bleeds through, making session switching non-functional during active work.

## Bug Summary

**Steps to reproduce:**
1. Have Agent A (e.g., Nova) actively running tool calls
2. Click on Agent B's session in the sidebar
3. Observe: Canvas still shows Agent A's conversation with loading timeline
4. Expected: Canvas shows Agent B's conversation

## Requirements

### Must Fix
1. Clicking a session in the sidebar immediately switches the conversation canvas to that session
2. Previous session's streaming/loading state does not bleed into the new view
3. Works regardless of whether the previous session's agent is actively working
4. Loading indicators are per-session, not global

### Should Fix
5. Background agent continues working — switching away does NOT cancel its work
6. Switching back to the working agent shows its current state (including new messages generated while away)
7. Sidebar shows activity indicator on sessions with active work (so user knows agent is still working)

### Nice to Have
8. Smooth transition animation between sessions
9. "Agent is working" badge on sidebar session entries
10. Notification when a background agent completes while viewing a different session

## Root Cause Analysis (Probable)

The issue is almost certainly one of these patterns:

### Pattern A: Single Shared Stream Subscription
```
Problem:
  SignalR messages -> single handler -> renders to canvas
  Session switch doesn't change the handler's target

Fix:
  SignalR messages -> route by sessionId -> only render if sessionId matches active view
```

### Pattern B: Component State Not Reset
```
Problem:
  <ConversationCanvas> holds messages in state
  Switching sessions doesn't clear/replace the state
  New session messages append to old session's messages

Fix:
  Use sessionId as React key: <ConversationCanvas key={activeSessionId} />
  Or explicitly clear state on session change
```

### Pattern C: Global Loading State
```
Problem:
  isLoading = true (global)
  Session switch doesn't reset it
  Loading spinner persists

Fix:
  loadingState = Map<sessionId, boolean>
  UI reads: loadingState[activeSessionId]
```

## Proposed Fix

### Core Principle
**The conversation canvas is a pure function of the selected session ID.** All state (messages, loading, streaming) is keyed by session ID.

### Implementation

#### 1. Session-Scoped Message Rendering
```typescript
// Messages from SignalR should be routed to session-specific stores
function onMessage(sessionId: string, message: Message) {
  sessionMessages[sessionId].push(message);

  // Only trigger re-render if this is the active session
  if (sessionId === activeSessionId) {
    renderCanvas();
  }
}
```

#### 2. Canvas Keyed by Session
```tsx
// React pattern: key change forces full remount
<ConversationCanvas
  key={activeSessionId}
  sessionId={activeSessionId}
  messages={sessionMessages[activeSessionId]}
  isLoading={loadingState[activeSessionId]}
/>
```

#### 3. Session Switch Handler
```typescript
function switchSession(newSessionId: string) {
  // 1. Update active session ID
  activeSessionId = newSessionId;

  // 2. Load conversation history for new session (if not cached)
  if (!sessionMessages[newSessionId]) {
    sessionMessages[newSessionId] = await fetchSessionHistory(newSessionId);
  }

  // 3. Re-render canvas with new session's data
  renderCanvas();

  // NOTE: Do NOT cancel the previous session's agent work
  // It continues in the background
}
```

#### 4. Per-Session Loading State
```typescript
// Loading state is per-session
const loadingState: Record<string, boolean> = {};

function onAgentStarted(sessionId: string) {
  loadingState[sessionId] = true;
  if (sessionId === activeSessionId) renderLoadingIndicator();
}

function onAgentCompleted(sessionId: string) {
  loadingState[sessionId] = false;
  if (sessionId === activeSessionId) hideLoadingIndicator();
  else showSidebarCompletionBadge(sessionId);
}
```

## Testing Plan

1. **Basic switch**: Agent A working, switch to Agent B — verify canvas shows B
2. **Switch back**: Switch to A — verify it shows A's current state including new messages
3. **Multiple switches**: Rapidly switch between 3+ sessions — verify no state bleed
4. **Agent completes while away**: A finishes while viewing B — verify A's sidebar updates
5. **Switch to idle session**: Switch from active agent to an idle session — verify no loading indicator
6. **New messages while away**: A generates 5 messages while viewing B — verify all 5 appear when switching back to A

## Scope

- **Frontend only** — this is a WebUI rendering/state management bug
- No backend/gateway changes needed
- Agent work continues unaffected in background
