# Design Review — Gateway Phase 3

**Reviewer:** Leela (Lead / Architect)
**Date:** 2026-04-07
**Scope:** 10 commits since `91ba88e` — P0 remediation, P1 config tests, architecture gaps
**Tests:** 333 passing (71 AgentCore + 116 Gateway + 146 CodingAgent)

**Grade:** B+

**SOLID:**
- SRP: Pass
- OCP: Pass
- LSP: Pass (minor test harness concern noted)
- ISP: Pass
- DIP: Pass

---

## P0 Findings (must fix)

1. **Path traversal in SystemPromptFile resolution** — `FileAgentConfigurationSource.cs:110` resolves `systemPromptFile` relative to `configDirectory` via `Path.GetFullPath(Path.Combine(...))` but never validates the resolved path stays within `configDirectory`. An agent config with `"systemPromptFile": "../../../etc/passwd"` could read arbitrary files. **Fix:** Add a bounds check: `if (!resolvedPath.StartsWith(Path.GetFullPath(configDirectory), StringComparison.OrdinalIgnoreCase)) throw`.

---

## P1 Findings (fix this sprint)

1. **No cross-agent recursion guard** — `DefaultAgentCommunicator.CallSubAgentAsync` (line 36) builds nested session IDs (`parent::sub::child`) but has no depth limit. Agent A calling B calling A produces infinite recursion. **Fix:** Add a max nesting depth (e.g., 5) by counting `::sub::` segments in the parent session ID.

2. **Redundant instance lookup in Steer/FollowUp endpoints** — `ChatController.cs:51-55` and `GatewayWebSocketHandler.cs:204-214` call `GetInstance()` for validation then `GetOrCreateAsync()` for the handle. If the instance was null on the first call but created between calls, we silently get a new handle. If the instance existed on the first call, the second call is redundant. **Fix:** Use a single lookup that returns the handle or null.

3. **CopilotAgentHandle no-op for SteerAsync/FollowUpAsync** — `CopilotIntegrationTests.cs:214-215` implements Steer/FollowUp as `Task.CompletedTask` no-ops. While this is test code, any future extraction to a real Copilot proxy handle would silently swallow steering messages. **Fix:** Throw `NotSupportedException` to match the isolation stub pattern, or add a comment documenting the intentional no-op.

4. **WebUI event delegation incomplete** — `app.js` delegates click events on `#chat-messages` (line 1161) but session/agent list items still attach per-element listeners (lines 839-846). This doesn't scale for large session lists. **Fix:** Move session/agent list clicks to a delegated handler on their parent containers.

5. **WebUI reconnection has no max retry limit** — Reconnection uses exponential backoff (base 1s, max 30s) but retries indefinitely. After extended outages, this produces unnecessary network traffic. **Fix:** Add a configurable max retry count (e.g., 20) after which the UI shows a "reconnect manually" button.

---

## P2 Findings (backlog)

1. **No session cleanup for cross-agent/sub-agent calls** — `DefaultAgentCommunicator` creates scoped sessions (`cross::...`, `parent::sub::child`) that are never cleaned up. Over time, orphaned sessions accumulate in the session store. **Fix:** Implement session TTL or parent-child lifecycle tracking.

2. **`History` property remains publicly mutable** — `GatewaySession.History` is a `List<SessionEntry>` with `{ get; init; }` but the list itself is still publicly writable. Production callers correctly use `AddEntry`/`AddEntries`/`GetHistorySnapshot`, but nothing prevents new code from bypassing thread safety via direct list access. **Fix:** Make `History` private and expose only thread-safe methods. This is a breaking change so defer to next phase.

3. **Test code uses direct `session.History.Add()`** — `FileSessionStoreTests.cs` has 4 instances of direct History mutation. Non-critical (single-threaded test context) but inconsistent with production patterns. **Fix:** Migrate tests to use `AddEntry()` for consistency.

4. **WebUI global state complexity** — `app.js` manages 27+ global variables for connection, streaming, timers, and UI state. No centralized state machine. **Fix:** Refactor to a state-machine or class-based pattern when WebUI grows beyond prototype phase.

5. **No Providers dictionary validation in PlatformConfigLoader** — `PlatformConfig.Providers` dictionary is accepted without validating provider names or ProviderConfig contents. **Fix:** Add optional provider config validation in `Validate()`.

6. **CancellationToken ignored in InProcessAgentHandle Steer/FollowUp** — `InProcessIsolationStrategy.cs:214-224` accepts cancellation tokens but never passes them through. Minor since operations complete synchronously, but violates the pattern. **Fix:** Either pass tokens to underlying calls or document the intentional omission.

---

## Strengths

- **Thread-safety implementation is correct**: `Lock` usage with no async inside locks, proper defensive copy in `GetHistorySnapshot()`, all production callers migrated. Clean, minimal approach.
- **Isolation strategy pattern is well-designed**: `IIsolationStrategy` is lean (2 members), stubs use `NotSupportedException` with helpful messages, all registered via multicast DI. Ready for Phase 2 without modification.
- **Cross-agent session scoping is thoughtful**: Hierarchical IDs (`parent::sub::child`, `cross::source::target::guid`) provide clear audit trails and prevent session bleeding.
- **Config subsystem is extensible**: `IAgentConfigurationSource` abstraction with hot-reload via `FileSystemWatcher` and timer debouncing. New sources can be added without touching core.
- **Error handling patterns are consistent**: Validation-then-fail-fast in DI setup, graceful degradation for missing files, structured logging throughout.
- **Test coverage for new code is strong**: 37+ new tests covering config loading, validation, hosted service lifecycle, isolation registration, controller endpoints, and WebSocket handlers.
- **WebUI reconnection with exponential backoff** is production-grade.
- **XML documentation quality** is high across all new interfaces and methods.

---

## Recommendations

- **P0 path traversal must be fixed before any deployment** — it's the only security finding.
- **Add recursion guard before enabling multi-agent scenarios** — a 3-line depth check prevents runaway agent loops.
- **Consolidate the GetInstance/GetOrCreateAsync pattern** — the current two-step is a race condition waiting to happen. Consider a `TryGetHandle()` method that returns null if no active session exists.
- **Consider making `GatewaySession.History` private in Phase 4** — the thread-safe methods are correct, but the escape hatch remains open for new contributors.
- **WebUI is solid for a prototype** — the global state will need refactoring before production, but the security posture (DOMPurify, escapeHtml) is good.
- **The sprint delivered significant architectural surface area cleanly** — the extension model (isolation strategies, config sources, cross-agent calling) follows our established patterns and will scale.
