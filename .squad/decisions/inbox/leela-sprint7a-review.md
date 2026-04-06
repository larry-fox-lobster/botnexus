### 2026-04-06T03:00Z: Sprint 7A Design Review
**By:** Leela
**Grade:** A-
**What:** Design review of Sprint 7A implementations (session reconnection, suspend/resume, pagination, depth limits, timeout, queuing, steering, session store config, OpenAPI)

## Scores
| Area | Grade | Notes |
|------|-------|-------|
| SOLID Compliance | A | DIP fix delivered (IGatewayWebSocketChannelAdapter). SRP well-maintained across GatewayHost, DefaultAgentCommunicator, SessionsController. Options pattern used correctly for configurable limits. Minor: GatewaySession accumulates reconnect replay concern alongside history — acceptable given thread-safety requirements but watch for further growth. |
| Extension Model | A | New features follow existing extension patterns. Session store selection via DI Replace(). Channel adapter contracts extended cleanly via IGatewayWebSocketChannelAdapter. Configurable options bound through IOptions<T>. PayloadMutator delegate pattern in WebSocketChannelAdapter is elegant. |
| API Design | A- | REST endpoints well-structured: PATCH for suspend/resume (idempotent state transitions), GET for pagination with bounded limits (cap at 200). Conflict (409) used correctly for invalid state transitions. WS reconnect protocol is clean (reconnect message → replay → reconnect_ack). Minor deduction: SessionHistoryResponse record defined in controller file — should live in Models/Abstractions for reuse. |
| Thread Safety | A | GatewaySession uses separate Lock objects for history and stream replay — good granularity. AllocateSequenceId is properly atomic. BoundedChannel for session queuing with SingleReader=true ensures sequential per-session processing. ConcurrentDictionary used correctly in GatewayWebSocketHandler for connection tracking. AsyncLocal call chain tracking in DefaultAgentCommunicator is correct for async flow. |
| Test Quality | A- | 39 new tests covering all Sprint 7A features. Thread-safety tests with 500 concurrent writers. Reconnect replay tested with window boundaries. Depth limit tested for exceed, within-limit, and recovery-after-failure. Timeout tested with both TimeoutException and caller cancellation propagation. Minor: TUI tests use Task.Delay(200) for timing — fragile on slow CI but acceptable for TUI adapter. |

## Findings
### P0 (Must Fix)
None.

### P1 (Should Fix)
1. **SessionHistoryResponse location** — `SessionHistoryResponse` record is defined at the bottom of `SessionsController.cs` (line 101). It's a response model that could be needed by clients, SDK generators, or other controllers. Move to `BotNexus.Gateway.Abstractions.Models` namespace alongside `GatewaySession` and `SessionEntry`.

2. **GatewaySession responsibility growth** — `GatewaySession` now owns both conversation history (with thread-safe locking) and WebSocket reconnect replay state (sequence IDs, stream event log, separate lock). This is two concerns in one class. Not blocking today, but if any more state is added (e.g., rate limit counters, presence tracking), extract replay state into a dedicated `SessionReplayBuffer` class to preserve SRP. Flag for Sprint 7B monitoring.

3. **SequenceAndPersistPayloadAsync serialization round-trip** — In `GatewayWebSocketHandler`, the method serializes the payload to JSON, deserializes back to `Dictionary<string,object?>`, adds `sequenceId`, then re-serializes. This double serialization works but is wasteful for high-throughput streams. Consider using `JsonNode` or a wrapper type to inject the sequence ID without the round-trip. Low priority but worth noting for performance-sensitive paths.

4. **Reconnect replay skips payloadMutator** — In `HandleReconnectAsync`, replayed events are sent as raw `PayloadJson` bytes directly to the socket, bypassing the `payloadMutator` pipeline. This is likely intentional (events are already sequenced), but it means replayed payloads won't go through any future middleware added to the mutator pipeline. Add a comment documenting this design choice.

### P2 (Informational)
1. **Consistent use of IOptions<T> constructor overloads** — Both `DefaultAgentCommunicator` and `GatewayWebSocketHandler` provide backward-compatible constructors that create default `Options.Create(...)` instances. Good pattern for test ergonomics. Consistent across the codebase.

2. **FileSessionStore now persists stream replay state** — The `SessionMeta` record includes `NextSequenceId` and `StreamEvents`, and `LoadFromFileAsync` calls `SetStreamReplayState`. This means reconnect replay survives gateway restarts for file-backed sessions. Well done — this wasn't explicitly required but is architecturally correct.

3. **Session queue cleanup** — `CleanupQueueIfClosedSessionAsync` drains the queue when a session is closed. The `CompleteSessionQueuesAsync` on shutdown is clean. Note that orphaned queue workers for idle sessions will persist until the next message arrives or shutdown occurs — acceptable for current scale.

4. **TUI steering uses hardcoded session ID** — The TUI adapter dispatches steer messages with `SessionId = "tui-console"`. This works for single-user local mode but won't support multi-session TUI if that ever becomes a requirement. Fine for now.

5. **PlatformConfigLoader.ValidateSessionStore** — Clean validation for InMemory/File types with actionable error messages. Ready for future store types (SQLite planned for Sprint 7C).

6. **Auth bypass (Path.HasExtension) and StreamAsync task leak** — Carried from Phase 5/6. Not addressed in Sprint 7A (correctly scoped out). Remain P1 for Sprint 7B.

## Recommendations
1. Move `SessionHistoryResponse` to abstractions (P1, Sprint 7B).
2. Monitor `GatewaySession` size — extract replay buffer if it grows further.
3. Add inline comment in `HandleReconnectAsync` explaining why replayed events bypass payloadMutator.
4. Address carried Phase 5/6 findings (auth bypass, task leak) in Sprint 7B.
5. Overall: Sprint 7A is solid work. Clean architecture, good test coverage, correct thread-safety patterns. The team delivered 8 features in 4+4+2+1 commits with zero regressions and 39 new tests. Grade: **A-** (minor structural nits prevent a clean A).
