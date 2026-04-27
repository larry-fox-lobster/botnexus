---
id: feature-conversation-topics
title: "Feature: Conversation Topics for Omnichannel Continuity"
type: feature
priority: high
status: draft
created: 2026-04-27
author: rusty
tags: [sessions, channels, signalr, portal, omnichannel, architecture]
related:
  - archived/feature-multi-session-connection/architecture-proposal.md
  - archived/feature-session-visibility/design-spec.md
  - bug-blazor-session-history-loss/design-spec.md
---

# Design Spec: Conversation Topics for Omnichannel Continuity

**Type**: Feature  
**Priority**: High  
**Status**: Draft  
**Author**: Rusty (via Jon)

## Overview

Introduce a new user-facing container above `Session` called **ConversationTopic** (working name: *topic*). A topic represents the conversation a user has with an agent, regardless of channel. Sessions remain the runtime/history segments within that topic.

This allows BotNexus to support the intended interaction model:
- each agent has a default conversation when it comes online,
- the portal shows that default conversation immediately,
- the portal can deliberately create second/third/fourth independent conversations with the same agent,
- external channels like Telegram and iMessage bind to a topic rather than directly to a session,
- the same conversation can continue across channels,
- agent replies can be delivered to all subscribed channels on the topic,
- session operations like compact/reset happen inside the topic without collapsing the topic itself.

## Problem

Today, `Session` is overloaded. It acts as:
- runtime unit,
- persistence unit,
- history segment,
- user-visible conversation,
- channel routing target.

That leads to several product problems:

1. **No true omnichannel continuity**  
   A conversation started in Telegram cannot naturally continue in iMessage and then in the portal as the same conversation.

2. **Portal usability is weak by default**  
   When an agent appears in the portal, there is no durable default conversation object that the user can immediately open and use.

3. **Multi-conversation UX is built on the wrong primitive**  
   The portal can show multiple sessions, but the user really wants multiple conversations, not multiple raw runtime segments.

4. **Channel routing semantics are wrong**  
   External channels should attach to a durable conversation, not whichever active session happens to exist.

5. **Session lifecycle operations are mixed with conversation identity**  
   Compaction, reset, sealing, and rollover are runtime concerns, but today they implicitly redefine the visible conversation.

## Goals

### Must Have

- Add a new durable parent container above session: `ConversationTopic`
- Each agent has one default topic available for the user by default
- A topic can contain one or more sessions over time
- Portal UI becomes topic-first
- Portal allows deliberate creation of additional topics per agent
- External channels bind to a topic
- Inbound channel messages route to the topic's active session
- Topic can survive session compaction/reset/sealing/rollover
- Topic history can be reconstructed from topic sessions
- Existing session-based infrastructure remains usable during migration

### Should Have

- Agent replies fan out to all active channel bindings on the topic
- User replies from one external channel are **not** mirrored into other external channels as if sent by the user
- Topic metadata supports title, created/updated timestamps, and a default flag
- Portal can switch between topics cleanly
- Session list becomes a detail view inside a topic rather than the main sidebar abstraction

### Nice to Have

- Per-binding notification modes (`interactive`, `notify-only`, `muted`)
- Topic rename/archive support
- Topic search/filter in the portal
- Topic-level analytics and summaries
- Explicit "move channel to another topic" workflows in UI

## Non-Goals

- Full multi-user permissions model
- Cross-agent shared topics
- Automatic semantic splitting of one conversation into multiple topics
- Cross-topic search/analytics in this first iteration
- Immediate parity across every channel UI from day one

## Proposed Domain Model

### New Entity: ConversationTopic

```csharp
public sealed record ConversationTopic
{
    public TopicId TopicId { get; set; }
    public AgentId AgentId { get; set; }
    public string Title { get; set; } = "New conversation";
    public bool IsDefault { get; set; }
    public TopicStatus Status { get; set; } = TopicStatus.Active;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public SessionId? ActiveSessionId { get; set; }
    public List<TopicChannelBinding> ChannelBindings { get; set; } = [];
    public Dictionary<string, object?> Metadata { get; set; } = [];
}
```

### New Value/Object Model: TopicChannelBinding

```csharp
public sealed record TopicChannelBinding
{
    public string BindingId { get; set; } = Guid.NewGuid().ToString("N");
    public ChannelKey ChannelType { get; set; }
    public string ExternalAddress { get; set; } = string.Empty; // e.g. telegram:5067802539
    public BindingMode Mode { get; set; } = BindingMode.Interactive;
    public DateTimeOffset BoundAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastInboundAt { get; set; }
    public DateTimeOffset? LastOutboundAt { get; set; }
}
```

### New Enum: TopicStatus

```csharp
public enum TopicStatus
{
    Active,
    Archived
}
```

### New Enum: BindingMode

```csharp
public enum BindingMode
{
    Interactive, // inbound + outbound
    NotifyOnly,  // outbound only
    Muted        // retained binding, no outbound fan-out
}
```

## Relationship to Existing Session Model

`Session` remains and continues to represent:
- agent runtime,
- stored history segment,
- compaction/reset boundary,
- replay buffer scope,
- execution lifetime.

New relationship:

```text
Agent
  -> ConversationTopic
      -> Session (1..n over time)
      -> TopicChannelBinding (0..n)
```

### Required Session Changes

Minimal additive change:

```csharp
public sealed record Session
{
    ...
    public TopicId? TopicId { get; set; }
}
```

This preserves backward compatibility while letting sessions be grouped under a topic.

## Conceptual Behavior

### 1. Default Topic per Agent

When an agent becomes visible/available to the system, BotNexus ensures a default topic exists.

Rules:
- one default topic per `AgentId`
- portal shows this topic as the main conversation for that agent
- if no topic exists yet, create it lazily on first contact or eagerly during warmup (configurable)

### 2. Additional Topics Created Deliberately

The portal exposes **New Conversation** for an agent.

That creates a new topic:
- `IsDefault = false`
- no channel bindings initially (or optionally binds the portal view implicitly)
- title can start as `Conversation 2`, `New conversation`, or first-user-message derived

### 3. Channel Binding Rules

External channels bind to a topic, not directly to a session.

For v1:
- Telegram/iMessage default to the agent's default topic
- later, user may explicitly rebind a channel to another topic
- multiple channels may bind to the same topic

### 4. Inbound Routing

When an inbound message arrives:
1. resolve `(AgentId, ChannelType, ExternalAddress)` to a topic binding
2. if none exists, bind the channel to the default topic for that agent
3. resolve or create the topic's active session
4. dispatch message into that session

### 5. Outbound Routing

When the agent emits an assistant/user-visible response in the topic's active session:
- deliver to all channel bindings on the topic where mode allows outbound delivery
- portal subscribers viewing that topic always receive the update

Important rule:
- **Do not mirror a human user's inbound message from Telegram into iMessage/portal as if that user sent it there.**
- Only assistant/system outputs are fanned out cross-channel.

### 6. Session Lifecycle Under a Topic

A topic may accumulate multiple sessions over time.

Examples:
- topic starts with session A
- user compacts session A -> same topic continues on compacted A or new session B depending implementation
- user resets conversation -> session A sealed, session B created, topic unchanged
- agent crash/restart -> runtime session replaced, topic unchanged

The visible conversation identity is stable even if the runtime segment changes.

## Portal UX Model

### Primary Sidebar

Portal sidebar should show **topics**, not raw sessions.

Per agent:
- show default topic immediately
- show any additional user-created topics
- each topic appears as a separate chat/conversation

### Topic Detail View

Inside a topic:
- current conversation transcript is shown as the merged history of the topic's sessions
- session boundaries can optionally appear as dividers (`Compacted`, `Restarted`, `New runtime`) 
- controls for compact/reset operate on the active session in the topic context
- advanced panel may show session history/segments for diagnostics

### Session Detail

Raw session list remains valuable, but as a secondary/diagnostic UI:
- active session id
- prior sealed sessions in the topic
- channel bindings attached to the topic

## API and Storage Impact

### New Store Contract

Add `ITopicStore`:

```csharp
public interface ITopicStore
{
    Task<ConversationTopic?> GetAsync(TopicId topicId, CancellationToken ct = default);
    Task<IReadOnlyList<ConversationTopic>> ListAsync(AgentId? agentId = null, CancellationToken ct = default);
    Task<ConversationTopic> GetOrCreateDefaultAsync(AgentId agentId, CancellationToken ct = default);
    Task<ConversationTopic> CreateAsync(ConversationTopic topic, CancellationToken ct = default);
    Task SaveAsync(ConversationTopic topic, CancellationToken ct = default);
    Task ArchiveAsync(TopicId topicId, CancellationToken ct = default);
    Task<ConversationTopic?> ResolveByBindingAsync(AgentId agentId, ChannelKey channelType, string externalAddress, CancellationToken ct = default);
}
```

### New Summary DTOs

```csharp
public sealed record TopicSummary(
    string TopicId,
    string AgentId,
    string Title,
    bool IsDefault,
    string Status,
    string? ActiveSessionId,
    int SessionCount,
    int BindingCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
```

### New Gateway/REST Endpoints

Potential endpoints:
- `GET /api/topics?agentId=assistant`
- `POST /api/topics`
- `GET /api/topics/{topicId}`
- `GET /api/topics/{topicId}/history`
- `POST /api/topics/{topicId}/bind-channel`
- `POST /api/topics/{topicId}/sessions/reset`
- `POST /api/topics/{topicId}/sessions/compact`

### SignalR Additions

Potential hub methods:
- `GetTopics(agentId)`
- `CreateTopic(agentId, title?)`
- `OpenTopic(topicId)`
- `SendTopicMessage(topicId, content)`
- `ResetTopic(topicId)`
- `CompactTopic(topicId)`

New events:
- `TopicCreated`
- `TopicUpdated`
- `TopicBindingsChanged`
- `TopicSessionChanged`

## History Model

### Topic History Endpoint

`GET /api/topics/{topicId}/history`

Behavior:
- returns merged chronological entries across all sessions linked to the topic
- includes boundary markers between sessions
- supports pagination

Example response concept:

```json
{
  "topicId": "t_123",
  "entries": [
    { "kind": "message", "sessionId": "s1", "role": "user", "content": "hey" },
    { "kind": "message", "sessionId": "s1", "role": "assistant", "content": "hi" },
    { "kind": "boundary", "reason": "compacted", "sessionId": "s1" },
    { "kind": "message", "sessionId": "s2", "role": "user", "content": "continue" }
  ]
}
```

This lets the portal show one continuous conversation while preserving runtime segmentation.

## Migration Strategy

### Phase 1 — Planning + additive model

- Add `TopicId` primitive
- Add `ConversationTopic` + `TopicChannelBinding`
- Add `ITopicStore`
- Add optional `Session.TopicId`
- Introduce default topic creation for agents
- No UI change yet

### Phase 2 — routing layer

- Change inbound channel routing to resolve topic first
- Add topic binding persistence
- Route inbound messages to topic active session
- Fan out assistant replies to topic bindings
- Preserve current session APIs for compatibility

### Phase 3 — portal topic-first UX

- Portal sidebar lists topics instead of raw sessions
- Default topic visible immediately
- New conversation creates a new topic
- Topic history endpoint used by portal
- Session list moved into advanced/details panel

### Phase 4 — lifecycle polish

- reset/compact operate in topic context
- explicit rebind/move channel workflows
- archive topic
- optional topic rename and summaries

## Compatibility Notes

- Existing session store remains valid.
- Existing channel adapters can keep writing to sessions during transition if routing shims resolve topic -> active session.
- Existing Blazor history/session code can be adapted gradually by swapping session list endpoints for topic list endpoints.
- Existing `SubscribeAll` session model may remain as a lower-level transport while the portal becomes topic-oriented.

## Risks

### 1. Conceptual duplication

Having both topics and sessions may confuse contributors unless responsibilities are documented clearly.

**Mitigation:** make topic user-facing, session runtime-facing in all docs/API naming.

### 2. History reconstruction complexity

Merged topic history across sessions may complicate pagination and scrollback.

**Mitigation:** introduce explicit boundary entries and keep session-scoped history endpoints intact.

### 3. Channel fan-out surprises

Cross-channel outbound delivery could annoy users if too aggressive.

**Mitigation:** binding modes and conservative default behavior.

### 4. Migration churn in UI and tests

The Blazor client recently moved toward session-first logic.

**Mitigation:** additive rollout; first add topics alongside sessions, then flip UI abstraction later.

### 5. Future multi-user scope

Current design assumes a single effective user per agent/channel context.

**Mitigation:** keep `Topic` model extensible for future owner/participant metadata.

## Testing Plan

### Domain / Store Tests

- creating default topic for an agent is idempotent
- creating additional topics does not affect default topic
- resolving channel binding returns the correct topic
- archived topics are excluded from active lists
- topic summaries include active session and binding counts

### Routing Tests

- first Telegram message binds to default topic
- subsequent Telegram message routes to same topic even if session rolled over
- iMessage can bind to same topic and continue context
- assistant message fans out to all interactive/notify bindings
- inbound user message on one channel is not replayed as user input to other channels

### Portal Tests

- default topic is visible when agent loads
- new conversation creates second topic
- switching topics changes visible merged history
- compact/reset inside topic preserves topic identity
- topic history shows session boundaries correctly

### Compatibility Tests

- existing session APIs still work when topic support is enabled
- session history endpoint remains correct for a topic's active session
- old clients can still send messages through session routing shim

## Open Questions

1. Final name: `Topic`, `Conversation`, or `Thread`?
2. Should default topic creation be eager (agent startup) or lazy (first contact)?
3. Should the portal itself create an explicit channel binding, or be treated as an implicit subscriber?
4. Are channel bindings scoped to exact external address only, or can one provider/channel family bind broadly?
5. When resetting a topic, should a new session always be created immediately?
6. Should topic title come from user input, auto-summary, or both?
7. Do we need a distinct topic event stream, or is session stream + lookup enough initially?

## Recommendation

Proceed with a **topic-first architectural iteration**.

The key product correction is:
- **sessions are runtime segments**,
- **topics are the user-visible omnichannel conversations**.

That matches the mental model Jon described and gives BotNexus a cleaner long-term foundation for portal UX, channel routing, and agent continuity across transports.
