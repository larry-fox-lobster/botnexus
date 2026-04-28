# Decision: Portal conversation-first UI contracts

**Author:** Leela (Lead/Architect)  
**Date:** 2026-04-28  
**Issue:** #37  
**Status:** Delivered

## Decision

The Blazor portal will move to a **conversation-first UI** while keeping the existing `AgentSessionManager` / `AgentSessionState` architecture.

### Core call

- **Do not** introduce a separate top-level `ConversationState` object graph for v1.
- **Do** make `AgentSessionState` conversation-aware.
- **Do** load conversations and conversation history from REST.
- **Do** keep SignalR session routing unchanged for live streaming.
- **Do** render session boundaries inline inside the chat timeline.

## Why

1. `ChatPanel.razor` already depends on `AgentSessionState` for streaming state, tool calls, connection state, and sub-agent metadata.
2. Live updates still arrive by `sessionId`; the existing manager already owns that routing problem.
3. A new parallel conversation state model would duplicate selection, unread, and history state and force reconciliation between two trees.
4. Conversation-first UX is a sidebar/history concern, not a reason to replace the current panel contract.

## Contracts finalized

### Services
`AgentSessionState` grows:
- `Conversations`
- `ActiveConversationId`
- `ConversationsLoaded`
- `IsLoadingConversations`
- `ActiveConversationTitle`

`AgentSessionManager` grows:
- `LoadConversationsAsync(agentId)`
- `SelectConversationAsync(agentId, conversationId)`
- `CreateConversationAsync(agentId, title, select)`
- `RefreshConversationsAsync(agentId)`
- `MarkConversationRead(agentId, conversationId)`

### DTOs
Create new client REST DTO file:
- `Services/ConversationContracts.cs`

Do not add conversation REST DTOs to `HubContracts.cs`.

### Sidebar
Replace the current per-agent session list with a conversation list under the selected agent.
Required UI elements:
- conversation row
- active highlight
- default badge
- unread dot
- `+ New` button

### Chat panel
`ChatPanel.razor` keeps taking `AgentSessionState`.
The panel renders one timeline list containing:
- normal messages
- inline session boundary rows

### Session divider
Exact label format:
- `Session · Apr 27 14:32 · s_abc123`

Exact root class:
- `.session-boundary`

## Wave split

### Fry
- DTOs
- state/service changes
- sidebar markup
- history mapping
- boundary rendering
- conversation unread plumbing

### Amy
- sidebar visuals
- active/default/unread visuals
- `+ New` button styling
- session divider styling
- responsive polish

## Jon input needed

One confirmation only:
- whether the first send into a selected conversation guarantees immediate conversation→active-session linkage from the current backend flow

If not, Fry can refresh conversations after first send. Not a blocker.

## Deliverable

Full design review written to:
- `docs/planning/feature-conversation-topics/portal-design-review.md`
