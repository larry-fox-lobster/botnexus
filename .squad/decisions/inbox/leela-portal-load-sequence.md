# Decision: Portal load sequence refactor

**Author:** Leela (Lead/Architect)  
**Date:** 2026-04-30  
**Issue:** #79  
**Status:** Delivered

## Decision

The Blazor portal must switch from **SignalR-first reactive loading** to **REST-first explicit loading**.

Final startup order:

1. Block UI with loading spinner
2. REST `GET /api/agents`
3. REST `GET /api/conversations?agentId=` for each agent in parallel
4. Select initial agent + conversation
5. REST `GET /api/conversations/{id}/history?limit=50` for the selected conversation
6. Connect SignalR
7. `SubscribeAll`
8. Mark `IsReady = true`

## Core architectural call

Split the current `AgentSessionManager` god class into exactly four focused responsibilities:

- `IGatewayRestClient` — owns all REST calls
- `IClientStateStore` — owns in-memory portal state
- `IGatewayEventHandler` — maps SignalR events to state mutations
- `IPortalLoadService` — owns startup sequencing and selection-triggered loads

Keep `GatewayHubConnection`.

Do **not** add more services than this unless implementation proves a real need.

## Why

The current failures are caused by sequence, not timing:

- SignalR connects before base data exists
- REST loads happen indirectly from hub callbacks
- the UI renders before state is complete
- polling/retry logic compensates for missing ownership

That is why first page visit history has been unreliable even after fixes #66 and #73.

## State model decision

Replace the current agent-root history model with a conversation-root model.

### Old shape problems
- agent-level `HistoryLoaded`
- separate `ConversationHistoryLoaded` set
- agent-level stream buffers
- computed `Messages` tied to active conversation

### New shape
- portal root has `IsReady`, `IsInitializing`, `InitializationError`
- each agent owns its conversation dictionary
- each conversation owns:
  - `Messages`
  - `HasLoadedLatestPage`
  - `IsLoadingHistory`
  - `HasMoreHistory`
  - `NextBeforeCursor`
  - `CurrentStreamBuffer`
  - `ThinkingBuffer`

This removes split state and makes pagination possible without hacks.

## Event routing decision

SignalR remains for deltas, not initial truth.

### Rules
- stream events mutate the active conversation transcript via state store methods
- `ConversationUpdated` is a sidebar invalidation signal, not transcript content
- sidebar refresh happens via REST after the event
- event handler knows state + REST, not UI

## Pagination decision

History becomes cursor-paged.

### REST contract
- `GET /api/conversations/{id}/history?limit=50`
- `GET /api/conversations/{id}/history?limit=50&before=<cursor>`

### UI behavior
- conversation select loads latest 50 and scrolls to bottom
- scroll-to-top loads older page and prepends it
- prepending must preserve scroll anchor
- paged history must never overwrite live streamed messages already in memory

## What to delete

Once the new design lands:

### Delete entire files
- `Services/AgentSessionManager.cs`
- `Services/AgentSessionState.cs`

### Delete behaviors/hacks
- polling loop in `SetActiveAgentAsync`
- `Task.Run(() => LoadConversationsAsync(...))` from `HandleConnected`
- loading gateway info only after `ApiBaseUrl` appears in `MainLayout.HandleStateChanged`
- state-change-triggered conversation loading
- agent-root history flags and message-store indirection helpers

## Wave breakdown

### Wave 1
`IGatewayRestClient` + `IPortalLoadService`
- implement explicit startup sequence
- add `IsReady` gate
- remove startup polling / fire-and-forget loading

### Wave 2
`IClientStateStore` + state migration
- move history and streaming buffers to conversation nodes
- replace `AgentSessionState`

### Wave 3
`IGatewayEventHandler` + code deletion
- move all hub event handling out of the manager
- delete `AgentSessionManager`
- make `ConversationUpdated` refresh sidebar state through REST

### Wave 4
Pagination
- add cursor-based history fetch
- prepend older messages on scroll-to-top
- preserve scroll anchor

## Risk callouts

1. **Over-abstraction risk**  
   Mitigation: stop at four services; delete aggressively.

2. **Session-to-conversation mapping risk**  
   Mitigation: maintain explicit session->agent and conversation->activeSession mappings in the state store.

3. **Reconnect drift risk**  
   Mitigation: refresh summaries on reconnect for affected agents; do not reload all transcripts by default.

## Bottom line

This refactor is approved because it simplifies behavior:

- explicit startup instead of reactive loading
- explicit readiness instead of timing guesses
- explicit state ownership instead of one class doing everything

The standard for implementation is not “more abstractions”. It is “less magic, fewer hacks, and less code.”
