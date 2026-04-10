# Research: Session Resumption Not Working After Gateway Restart
## Problem Statement
When the BotNexus gateway is restarted, the agent (Nova) starts a fresh session instead of resuming the previous one. The conversation history and compaction summary are lost, requiring the user to re-explain context.
## Observed Behavior
1. Jon and Nova were in an active session (ID: `edd6b197b2a2...`, 48 messages)
2. Last conversation topic: researching compaction in OpenClaw and industry
3. Gateway was restarted (code changes deployed by the squad)
4. Nova woke up in a **new session** with no memory of prior conversation
5. When asked "what do you know from this session", Nova had zero in-context history
6. Session history API showed the old session existed with all messages intact
## Expected Behavior
After gateway restart:
1. Gateway should identify the most recent active session for the channel/agent
2. Load the session's compaction summary (or recent messages) into the new context
3. Agent should "remember" what was being discussed and continue naturally
4. User should not need to re-explain context
## Technical Analysis
### What the Session Store Has
- SQLite database at `C:\Users\jobullen\.botnexus\sessions.db`
- Session `edd6b197b2a2...` with 48 messages, status 0 (active)
- Session was last updated `2026-04-10T15:30:37Z`
- Compaction config exists: threshold 60%, max summary 16K chars, preserves 3 turns
### What Likely Went Wrong
Possible causes (needs squad investigation):
1. **New session created instead of resumed**: SignalR reconnection may create a new session rather than looking up the existing one for the agent+channel pair
2. **Session matching logic**: May not be matching on the right key (agent ID + channel type + user?) after restart
3. **No compaction summary persisted**: If the session never hit compaction threshold, there may be no summary to inject on resume
4. **History injection missing**: Even without compaction, the gateway could inject recent messages into context on resume
### Config Reference
```json
{
  "gateway": {
    "compaction": {
      "tokenThresholdRatio": 0.6,
      "maxSummaryChars": 16000,
      "contextWindowTokens": 128000,
      "summarizationModel": "gpt-4.1",
      "preservedTurns": 3
    }
  }
}
```
## Industry Reference
### Claude Code
- Uses checkpoints and session files for resumption
- `/resume` command to continue a previous session
- Compaction summary is persisted and re-injected
### OpenClaw (from source analysis)
- Session JSONL files persist full history
- Compaction summary written as a session entry type
- Post-compaction injects AGENTS.md "Session Startup" and "Red Lines" sections
- `readPostCompactionContext()` function handles context refresh after compaction
## Questions for the Squad
1. When SignalR reconnects after gateway restart, does it look up the previous session?
2. What is the session matching strategy? (agent ID + channel ID? session cookie?)
3. Is there a session resume endpoint or does the client always create new?
4. Where is the compaction summary stored? (in the session messages table? separate field?)
5. Is there a "session resume" code path that loads history/summary into context?

## Regression — 2026-04-10

Session resumption was reportedly implemented by the squad and confirmed working on 2026-04-10. However, on the next gateway restart (same day / next morning), Nova started a fresh session with no prior context again. This suggests one of:

- **A regression**: Something broke the fix between the confirmation and the next restart.
- **An incomplete fix**: The fix may only cover SignalR client reconnects (client reconnects to a still-running gateway process) but NOT a full gateway process cold start (process exits and is relaunched).
- **An uncovered scenario**: Cold start (process restart) may exercise a completely different code path than reconnect (SignalR connection drop/resume within a running process).

### Investigation Questions

1. **What exactly was fixed?** Was it the SignalR reconnect handler, the session startup path, or both? These are different code paths.
2. **Cold start vs reconnect**: Does the fix only work when the SignalR client reconnects to a running gateway? A full `botnexus gateway restart` (process exit + relaunch) is a cold start — the gateway has no in-memory state and must look up the session from the store. Was this path tested?
3. **Cron/heartbeat session interference**: Could a cron or heartbeat session be interfering with the matching logic? If cron sessions share the same `sessions` table with `status = active`, a naive "pick most recent active session for this agent" query might resume the wrong session — e.g., picking up a heartbeat session that ran 5 minutes ago instead of the main conversation session from an hour ago.
4. **Session type discrimination**: Is there a `session_type` or `source` column that distinguishes main interactive sessions from cron/heartbeat sessions? If not, the matching logic has no way to exclude ephemeral sessions.

### Hypothesis

The most likely explanation is that the fix covered **SignalR reconnect** (client reconnects to a running gateway) but **not gateway cold start** (process restart). These are architecturally different:

| Scenario           | Gateway process | In-memory state | Session lookup required? |
|--------------------|-----------------|-----------------|--------------------------|
| SignalR reconnect  | Still running   | Preserved       | No (already in memory)   |
| Gateway cold start | New process     | Empty           | Yes (must query store)   |

If the fix relies on in-memory session state surviving across reconnects, it would explain why it worked initially (reconnect) but failed on the next restart (cold start).

## Key Design Principle — Channel-Scoped Sessions

Sessions are scoped to `agent_id + channel_type`, **NOT** to individual client connections.

A **channel** (e.g., SignalR, Telegram, Discord) is a logical communication pipe. Multiple clients can connect to the same channel simultaneously. The session belongs to the channel, not to any individual client.

### Examples

- **Telegram**: A user has Telegram open on their phone, desktop app, and laptop web client. All three should see and continue the **same conversation** with the agent. There is one session for the `nova + telegram` pair, not three.
- **WebUI / SignalR**: A user has the BotNexus WebUI open on their desktop browser and their laptop browser. Both should show the same conversation and stay in sync. Messages sent from one should appear on the other.
- **Discord**: Multiple users in a Discord channel all see the same conversation. The session is the channel, not any individual user's view of it.

### The Chat-Room Model

Think of a session as a **chat room**:

- The session **IS** the room. It exists independently of who's connected.
- Clients **come and go**. A client connecting is like entering the room; disconnecting is like leaving. The room (session) persists.
- Everyone in the room sees the same thing. Messages are shared state.
- A **late-joining client** must catch up — load conversation history so it appears identical to what other clients already see.

### Implication for Session Matching

Session matching must **NEVER** use `client_id` or `connection_id` as part of the match key. The only key is:

```
agent_id + channel_type
```

If the matching logic includes any client-specific identifier, it will:
1. Create separate sessions for each device/browser/client — breaking continuity
2. Fail to resume on cold start (new process = new connection IDs)
3. Make multi-device usage impossible

This is a fundamental architectural constraint, not an optimization.
