# Research: Session Switching Broken in WebUI During Active Agent Work

## Problem Statement

When an agent (Nova) is actively working (running multiple tool calls), clicking on a different agent's session in the WebUI sidebar does NOT properly switch the conversation canvas. Instead:

1. The conversation canvas continues showing Nova's conversation
2. A loading timeline/spinner persists from Nova's in-progress work
3. The selected agent's conversation is never rendered
4. The UI appears "stuck" on the previous agent's active session

## Observed Behavior

1. Nova is running (executing multiple tool calls in sequence)
2. User clicks on a different agent's session in the sidebar
3. **Expected**: Canvas switches to show the other agent's conversation
4. **Actual**: Canvas still shows Nova's conversation with loading/progress indicators
5. UI is effectively broken — cannot view other sessions while any agent is working

## Severity

This is a **high severity UX bug**:
- Users cannot multitask across agents (the whole point of having multiple agents)
- No workaround other than waiting for the active agent to finish
- Blocks the use case of monitoring multiple agents
- Makes the multi-agent experience feel broken/unreliable

## Likely Causes

### 1. Shared Streaming State
The WebUI likely has a single "active stream" or "current response" state that is bound to the component rather than the session. When switching sessions, the streaming connection from the previous session continues writing to the canvas.

### 2. Canvas Not Re-rendering on Session Switch
The conversation canvas component may not properly unmount/remount or clear its state when the selected session changes. The React/component state from the previous session bleeds through.

### 3. SignalR Connection Not Scoped to Session
If the SignalR connection delivers messages globally (not scoped to the active view), session switch may not stop the rendering of incoming messages from the previous session.

### 4. Loading/Progress State Not Reset
The loading timeline indicator is likely tied to a global "agent is working" flag rather than a per-session flag. When switching sessions, this flag is not reset, so the loading state persists.

## Industry Reference

### Standard Chat App Pattern
Every multi-conversation chat app (Slack, Teams, Discord, iMessage) handles this:
- Switching channels/conversations immediately renders the new conversation
- Background channels continue receiving messages but don't render to the active view
- Loading states are per-conversation, not global
- The active view is fully determined by the selected conversation

### Claude Code
- Single session, no session switching (not applicable)

### ChatGPT
- Sidebar conversation list, click to switch
- Switches immediately even if previous conversation had a streaming response
- Previous stream is abandoned visually (continues in background)

## Key Design Principle

**The conversation canvas should be a pure function of the selected session.** Switching sessions = full canvas replacement. No bleed-through from other sessions.
