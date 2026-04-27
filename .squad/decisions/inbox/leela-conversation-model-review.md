# Decision: Conversation Model Final Design Review

**Date:** 2026-04-27  
**Author:** Leela (Lead/Architect)  
**Status:** Approved with implementation clarifications  
**Spec:** `docs/planning/feature-conversation-topics/design-spec.md`

## Summary

The Conversation Model feature is approved to move into implementation. The product direction is correct: BotNexus needs a durable user-facing `Conversation` above runtime `Session` to support omnichannel continuity, default per-agent conversations, and conversation-first portal UX.

The review found **no blockers**, but did identify several critical clarifications that must shape implementation:

1. `ConversationId` already exists in `BotNexus.Domain.Primitives` and must be reused.
2. `IConversationStore` belongs in `BotNexus.Gateway.Contracts`; implementations belong in `BotNexus.Gateway.Sessions`.
3. Conversation persistence should ship with `InMemory`, `File`, and `Sqlite` implementations.
4. `GatewayHub.ResolveOrCreateSessionAsync()` must be replaced with conversation-first routing.
5. `Session.ConversationId` should remain nullable; existing sessions are legacy-compatible and do not require eager migration.
6. `ISessionWarmupService` should remain session-scoped; conversations need a separate catalog/warmup concern.
7. Assistant reply fan-out must be centralized in a gateway-level `IConversationRouter`, not pushed into each channel adapter.

## Key Architectural Decisions

### D1. Conversation is the user-visible identity; Session remains the runtime/history segment

This feature should not weaken the current session model. Session still owns:
- runtime execution
- compaction/reset boundary
- replay buffer scope
- live streaming scope
- persisted message history segment

Conversation owns:
- stable omnichannel identity
- default conversation per agent
- channel bindings
- active session pointer
- title/archive metadata

### D2. Reuse existing `ConversationId`

Do not create a new primitive. The existing type at `src/domain/BotNexus.Domain/Primitives/ConversationId.cs` is the correct home.

### D3. Put persistence beside session persistence

Conversation store implementations should live in `BotNexus.Gateway.Sessions`, not a new project. The storage/configuration problem is the same as session persistence.

### D4. Routing must become conversation-first

Current hub logic resolves a session from `(agentId, channelType)`. That is the main architectural mismatch.

New rule:
- inbound resolves conversation first
- conversation resolves/creates active session
- dispatch remains session-level after that

### D5. Keep `Session.ConversationId` nullable

Legacy sessions without a conversation link are valid old data, not corruption. Populate the field opportunistically when a session becomes conversation-managed.

### D6. Do not overload `ISessionWarmupService`

Conversation discovery/default creation needs its own service (`IConversationCatalogService` or similar). Session warmup should stay focused on sessions.

### D7. Fan-out belongs in a conversation router

Adapters should send transport-specific messages. They should not own binding lookup or cross-channel fan-out policy.

## Contracts to Implement

### Domain
- `Conversation` → `BotNexus.Domain.Conversations`
- `ChannelBinding` → `BotNexus.Domain.Conversations`
- `ConversationStatus`, `BindingMode`, `ThreadingMode` → same namespace
- `Session.ConversationId` additive change

### Gateway Contracts
- `IConversationStore`
- `ConversationSummary`
- `IConversationRouter`
- `IConversationHistoryService`
- conversation history DTOs/cursor types

### Gateway/Runtime
- `DefaultConversationRouter`
- `ConversationHistoryService`
- `InMemoryConversationStore`
- `FileConversationStore`
- `SqliteConversationStore`
- conversation-first `GatewayHub` methods
- conversation REST controller

## Wave Plan

### Wave 1 — Foundation (Farnsworth)
**Scope:** domain model, contracts, storage, DI wiring  
**Tests:** ~18-24

### Wave 2 — Routing (Bender)
**Scope:** inbound resolution, active session management, outbound fan-out  
**Tests:** ~16-22

### Wave 3 — API + History (Fry)
**Scope:** conversation REST/hub APIs, history assembly with session boundaries  
**Tests:** ~12-18

### Wave 4 — Portal UX (Amy)
**Scope:** conversation-first sidebar and merged transcript rendering  
**Tests:** ~10-14

### Wave 5 — Compatibility + Docs (Hermes/Kif)
**Scope:** regression, migration coverage, architecture/training docs  
**Tests:** ~20-28

## Top Risks

1. **Routing duality** — old session routing and new conversation routing both try to send messages  
   **Mitigation:** centralize fan-out in `IConversationRouter`

2. **Legacy session ambiguity** — historical sessions have no conversation linkage  
   **Mitigation:** keep them readable but exclude from conversation assembly until linked

3. **Portal/session stream mismatch** — portal is conversation-first but live stream is session-first  
   **Mitigation:** maintain conversation → active session mapping and refresh subscription on active-session change

4. **Storage divergence** — conversation store grows a different persistence/config model than session store  
   **Mitigation:** implement in `BotNexus.Gateway.Sessions` and mirror session store configuration patterns

5. **Channel fan-out surprises** — duplicate outbound or wrong binding behavior  
   **Mitigation:** adapters render/send only; router owns policy

## Outcome

The feature is **approved for implementation**. Start with storage/contracts/routing. Do not lead with UI work.

The high-risk seam is not the portal — it is replacing session-first routing with conversation-first routing without duplicating or regressing delivery behavior.
