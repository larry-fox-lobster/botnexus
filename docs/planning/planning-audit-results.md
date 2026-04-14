# Planning Audit — Post-DDD & WebUI Refactor
**Date**: 2026-04-12
**Assessor**: Nova
**Supersedes**: ASSESSMENT-2026-04-12.md (written pre-refactor, several conclusions now wrong)

## Summary

| Folder | Verdict | Action |
|--------|---------|--------|
| bug-session-resumption | **still-valid** | Keep — core continuity problem persists |
| bug-session-switching-ui | **needs-update** | Keep — NOT obsolete, new bug found (channel mismatch). Update spec |
| bug-steering-delivery-latency | **still-valid** | Keep |
| bug-steering-message-visibility | **still-valid** | Keep |
| feature-agent-delay-tool | **obsolete** | Archive — `delay` tool is implemented and live |
| feature-context-visibility | **still-valid** | Keep |
| feature-file-watcher-tool | **obsolete** | Archive — `watch_file` tool is implemented and live |
| feature-infinite-scrollback | **obsolete** | Archive — implemented (ChannelHistoryController + IntersectionObserver + cursor pagination) |
| feature-multi-session-connection | **obsolete** | Archive — status already says IMPLEMENTED, subscribe-all model is live |
| feature-planning-pipeline | **still-valid** | Keep — process convention, always relevant |
| feature-session-visibility | **obsolete** | Archive — status already says implemented, SessionType filtering is live |
| feature-subagent-ui-visibility | **needs-update** | Keep — still not implemented, but spec references old JoinSession patterns |
| improvement-agent-trust-paths | **still-valid** | Keep — not implemented, still relevant |
| improvement-datetime-awareness | **partially-obsolete** | Archive — `buildTimeSection` in SystemPromptBuilder injects timezone. Shell TZ fix is minor |
| improvement-eager-session-rehydration | **needs-update** | Keep — SessionWarmupService exists but doesn't inject history into agent context |
| improvement-memory-lifecycle | **still-valid** | Keep — pre-compaction flush not implemented |
| message-queue-injection-timing | **still-valid** | Keep — mid-turn message injection still not implemented |

**Archive: 6 items** | **Keep: 11 items**

## Details

### bug-session-resumption
**Verdict:** still-valid
**Assessment:** Session resumption across gateway restarts is still the core continuity problem. `SessionWarmupService` preloads session summaries but does NOT inject history into agent LLM context. The spec is accurate for the remaining work.

### bug-session-switching-ui
**Verdict:** needs-update
**Assessment:** The previous assessment marked this "likely obsolete" because the subscribe-all model eliminated JoinSession/LeaveSession race conditions. However, we just found a NEW session switching bug: channel type mismatch between write path ("signalr") and read path ("web chat") causes history to disappear on switch/refresh. The old root cause (stale currentSessionId) IS fixed, but the spec should be updated to document the new channel mismatch bug. See `channel-mismatch-bug.md` in Nova's workspace.

### bug-steering-delivery-latency
**Verdict:** still-valid
**Assessment:** Steering message timing (delivery between tool calls, not just between turns) is an infrastructure timing issue orthogonal to the DDD/UI refactor. Still needs investigation and implementation.

### bug-steering-message-visibility
**Verdict:** still-valid
**Assessment:** Steering messages still don't appear inline in the conversation timeline. The subscribe-all model doesn't change this — it's a rendering/UX gap.

### feature-agent-delay-tool
**Verdict:** obsolete
**Action:** Archive
**Assessment:** `DelayTool` exists at `src/gateway/BotNexus.Gateway/Tools/DelayTool.cs` and is registered. Nova's tool list includes `delay`. Fully implemented.

### feature-context-visibility
**Verdict:** still-valid
**Assessment:** No `/context` command or token count visibility exists in the platform. The two-layer approach (agent-side estimate + platform-side actual) is still the right plan.

### feature-file-watcher-tool
**Verdict:** obsolete
**Action:** Archive
**Assessment:** `FileWatcherTool` exists at `src/gateway/BotNexus.Gateway/Tools/FileWatcherTool.cs`. Nova's tool list includes `watch_file`. Fully implemented.

### feature-infinite-scrollback
**Verdict:** obsolete
**Action:** Archive
**Assessment:** Fully implemented. `ChannelHistoryController` provides cursor-based cross-session pagination. Client has `setupScrollbackObserver()` using `IntersectionObserver` with sentinel elements, session boundary dividers, and scroll position preservation. This was built as part of the channel-centric history refactor.

### feature-multi-session-connection
**Verdict:** obsolete
**Action:** Archive
**Assessment:** Status already says "IMPLEMENTED". Subscribe-all model is live — `SubscribeAll` on connect, `switchView()` shows/hides per-channel containers, no JoinSession/LeaveSession round-trips.

### feature-planning-pipeline
**Verdict:** still-valid
**Assessment:** Process convention document. Always applicable.

### feature-session-visibility
**Verdict:** obsolete
**Action:** Archive
**Assessment:** Status already says "implemented". SessionType filtering for sidebar visibility is live — only UserAgent sessions appear.

### feature-subagent-ui-visibility
**Verdict:** needs-update
**Assessment:** Sub-agent sessions are still not visible in the WebUI sidebar. The feature is still needed, but the spec references old JoinSession patterns and needs updating for the subscribe-all model and channel-centric sidebar.

### improvement-agent-trust-paths
**Verdict:** still-valid
**Assessment:** No trusted path configuration exists. The `read` tool still restricts to workspace (I hit "Access denied" reading design specs today). Agents still fall back to `bash cat` as workaround.

### improvement-datetime-awareness
**Verdict:** partially-obsolete
**Action:** Archive
**Assessment:** `SystemPromptBuilder.buildTimeSection()` injects timezone into the system prompt. The Runtime line in my context shows the provider/model/channel. The core requirement (agent knows current time in user's timezone) is implemented. Only minor items remain (shell TZ fix) which don't warrant a planning item.

### improvement-eager-session-rehydration
**Verdict:** needs-update
**Assessment:** `SessionWarmupService` exists as a hosted service and preloads session summaries into a cache. `GetAvailableSessionsAsync` is used by `ResolveOrCreateSessionAsync` to find existing sessions. But the spec's core gap — injecting session history into the agent's LLM context on resume — is NOT done. The spec should be updated to reflect what warmup already provides vs what remains.

### improvement-memory-lifecycle
**Verdict:** still-valid
**Assessment:** No pre-compaction memory flush, no session-end flush, no dreaming. Compaction infrastructure exists (`CompactionOptions`, `CompactionResult`) but the memory persistence triggers described in this spec are not implemented.

### message-queue-injection-timing
**Verdict:** still-valid
**Assessment:** Mid-turn message injection (flushing queued user messages between tool calls, not just between turns) is still not implemented. The `delay` tool now exists which makes this even more relevant for review-loop workflows.
