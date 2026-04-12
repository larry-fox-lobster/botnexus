# Planning Specs Assessment - Post DDD Refactoring & WebUI Simplification

**Date**: 2026-04-12  
**Assessor**: Copilot CLI  
**Context**: Major changes completed:
- DDD refactoring: BotNexus.Domain with value objects (AgentId, SessionId, ChannelKey, MessageRole, etc.)
- SessionStatus renamed Closed→Sealed, SessionType added, Participants model
- Gateway.Abstractions split into Domain + Contracts
- Session split into Session (domain) + Runtime (infra)
- WebUI simplified: subscribe-all model, no JoinSession/LeaveSession, channel-centric sidebar

---

## Summary

| Status | Count | Specs |
|--------|-------|-------|
| ✅ Implemented | 2 | feature-multi-session-connection, feature-session-visibility |
| ⚠️ Likely Obsolete | 1 | bug-session-switching-ui |
| 📝 Updated for DDD | 6 | bug-session-resumption, feature-infinite-scrollback, improvement-eager-session-rehydration, feature-subagent-ui-visibility, bug-steering-delivery-latency, bug-steering-message-visibility |
| ✓ Still Applicable | 9 | feature-agent-delay-tool, feature-context-visibility, feature-file-watcher-tool, feature-planning-pipeline, improvement-agent-trust-paths, improvement-datetime-awareness, improvement-memory-lifecycle, message-queue-injection-timing |

---

## Detailed Assessment

### ✅ Implemented / Done

#### feature-multi-session-connection
**Status**: IMPLEMENTED  
**Action**: Marked as implemented (subscribe-all model is live)  
**Notes**: The architecture proposal defined the subscribe-all connection model where clients subscribe to all sessions once on connect, and session switching is a pure client-side DOM operation. This has been implemented in the WebUI.

#### feature-session-visibility
**Status**: IMPLEMENTED  
**Action**: Marked as implemented  
**Notes**: SessionType filtering for sidebar visibility was implemented as part of the DDD refactoring. Only SessionType.UserAgent sessions appear in sidebar, internal sessions (Soul, Cron, AgentSelf, AgentSubAgent, AgentAgent) are filtered out.

---

### ⚠️ Likely Obsolete (Verification Needed)

#### bug-session-switching-ui
**Status**: LIKELY OBSOLETE  
**Action**: Marked for verification  
**Notes**: The root cause (race conditions during JoinSession/LeaveSession, version counters, stale event delivery) should be eliminated by the subscribe-all model. The original problems were architectural - caused by session switching being a server-side operation. Now that switching is client-side only, these bugs should not occur. **Verification needed**: Run the E2E tests described in the spec to confirm the bug is fixed.

---

### 📝 Updated for DDD Types and New Architecture

#### bug-session-resumption
**Status**: REOPENED → Updated  
**Action**: Updated references to use DDD types  
**Changes**:
- Uses `AgentId`, `SessionId`, `ChannelKey` value objects
- Uses `SessionStatus.Active`, `SessionType.UserAgent` smart enums
- Updated session lookup query to filter by `SessionType.UserAgent` (excludes Soul, Cron, etc.)
- Updated API endpoint to use value objects in query params
- Still applicable - session resumption across gateway restarts needs implementation

#### feature-infinite-scrollback
**Status**: DRAFT → Updated  
**Action**: Updated type references and ISessionStore methods  
**Changes**:
- Uses `Session`, `SessionId`, `AgentId`, `ChannelKey`, `SessionStatus` from BotNexus.Domain
- Updated cross-session logic to use `SessionStatus.Active` and `SessionStatus.Sealed` smart enums
- Added namespace references: `ISessionStore` from BotNexus.Gateway.Contracts
- Updated method signature for `ListByChannelAsync` to use value object parameters
- Still applicable - infinite scrollback feature not yet implemented

#### improvement-eager-session-rehydration
**Status**: PROPOSED → Updated  
**Action**: Updated type references throughout  
**Changes**:
- Uses `Session` domain model instead of `GatewaySession`
- Updated `FindRehydrationCandidatesAsync` to return `IReadOnlyList<Session>`
- Uses `SessionType.UserAgent` for filtering (replaces session_type column check)
- Updated session discovery API to use `AgentId` and `ChannelKey` value objects
- Uses `Session.History` for prior conversation context
- Still applicable and needed

#### feature-subagent-ui-visibility
**Status**: DRAFT → Updated  
**Action**: Updated for DDD types and subscribe-all model  
**Changes**:
- Uses `SessionType.AgentSubAgent` from domain model (no schema changes needed)
- Uses `Session.ParentSessionId` property (already in domain model)
- Updated SignalR integration section: subscribe-all model means no explicit subscription needed
- Removed references to database schema changes (already part of domain model)
- Still applicable - sub-agent sessions not yet visible in UI

#### bug-steering-delivery-latency
**Status**: DRAFT  
**Action**: Minor - spec is generally good, no type updates needed  
**Notes**: Steering message delivery timing issue persists. Spec is about infrastructure timing, not session types, so minimal DDD impact.

#### bug-steering-message-visibility
**Status**: DRAFT  
**Action**: Minor - spec is generally good  
**Notes**: UI visibility for steering messages. The simplified connection model may improve this, but the core requirement (showing steering messages in conversation timeline) is still valid.

---

### ✓ Still Applicable (No Major Changes Needed)

#### feature-agent-delay-tool
**Status**: DRAFT  
**Applicability**: YES  
**Notes**: Well-designed spec using modern patterns. Implementation paths are current. No DDD type dependencies.

#### feature-context-visibility
**Status**: DRAFT  
**Applicability**: YES  
**Notes**: Context visibility `/context` command. Agent-side implementation, minimal platform dependencies.

#### feature-file-watcher-tool
**Status**: DRAFT  
**Applicability**: YES  
**Notes**: Comprehensive spec for file watching. No session/domain type dependencies.

#### feature-planning-pipeline
**Status**: ACTIVE  
**Applicability**: YES (Process Convention)  
**Notes**: Process documentation, not code. No changes needed.

#### improvement-agent-trust-paths
**Status**: DRAFT  
**Applicability**: YES  
**Notes**: Configurable file path trust. Security/permissions feature, orthogonal to DDD refactoring.

#### improvement-datetime-awareness
**Status**: DRAFT  
**Applicability**: YES  
**Notes**: DateTime/timezone awareness for agents. System prompt injection pattern, no domain type dependencies.

#### improvement-memory-lifecycle
**Status**: DRAFT  
**Applicability**: YES  
**Notes**: Memory persistence triggers (pre-compaction flush, session-end flush, dreaming). Still needed.

#### message-queue-injection-timing
**Status**: PLANNING  
**Applicability**: YES  
**Notes**: Message queue timing issue. Still needs investigation and spec completion.

---

## Actions Completed

1. ✅ Marked feature-multi-session-connection as IMPLEMENTED
2. ✅ Marked feature-session-visibility as IMPLEMENTED  
3. ✅ Marked bug-session-switching-ui as LIKELY_OBSOLETE with verification note
4. ✅ Updated bug-session-resumption for DDD types (AgentId, SessionId, ChannelKey, SessionStatus, SessionType)
5. ✅ Updated feature-infinite-scrollback for DDD types and ISessionStore references
6. ✅ Updated improvement-eager-session-rehydration for Session domain model and value objects
7. ✅ Updated feature-subagent-ui-visibility for SessionType.AgentSubAgent and subscribe-all model
8. ✅ Committed all changes in conventional commits

---

## Recommendations

### Immediate
1. **Run E2E tests for bug-session-switching-ui** to verify it's truly fixed by subscribe-all model
2. **Archive to done folder** if confirmed: feature-multi-session-connection, feature-session-visibility, bug-session-switching-ui (if verified)

### Short Term
3. Prioritize implementation of **bug-session-resumption** (critical continuity feature)
4. Implement **improvement-datetime-awareness** (quick win, high value)
5. Implement **feature-agent-delay-tool** (well-specified, ready to build)

### Medium Term
6. Implement **improvement-eager-session-rehydration** (depends on session resumption)
7. Implement **feature-subagent-ui-visibility** (after session switching verification)
8. Implement **improvement-memory-lifecycle** (pre-compaction flush)

### Backlog
9. Complete spec for **message-queue-injection-timing**
10. Implement **feature-infinite-scrollback** (nice UX improvement)
11. Implement **feature-file-watcher-tool** (reactive workflows)
12. Implement **improvement-agent-trust-paths** (security improvement)

---

## Notes

All specs have been reviewed and updated where necessary. The DDD refactoring and WebUI simplification have:
- **Solved**: Multi-session connection complexity, session visibility filtering
- **Likely Solved**: Session switching UI bugs
- **Simplified**: Sub-agent session tracking (types already in domain model)
- **No Impact**: Tool implementations, agent-side features, process conventions
- **Positive Impact**: Cleaner type system, better separation of concerns, more maintainable specs
