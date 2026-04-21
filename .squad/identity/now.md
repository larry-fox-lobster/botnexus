---
updated_at: 2026-04-20T23:30:00Z
focus_area: Sub-Agent Completion Wakeup Bug Fix Delivered
active_issues: []
status: subagent_wake_fix_delivered
---

# What We're Focused On

**Sub-Agent Completion Wakeup Bug Fix delivered (2026-04-20 23:30Z).** Fixed two root causes preventing sub-agent completion signals from waking parent session: (1) InternalChannelAdapter stream event delivery via `SendStreamEventAsync`, (2) IsRunning race condition in `DefaultSubAgentManager.OnCompletedAsync` eliminated by removing timing-dependent branching and always dispatching via gateway queue. 5-wave delivery: Design Review (Leela) → Tests (Hermes) → Adapter Integration (Farnsworth) → Race Fix (Bender) → Consistency Review (Nibbler). 6 files changed, 309 insertions, 46 deletions. 3 new reproducing tests + 5 existing tests updated. 2,584 total tests passing, zero failures.

**Previous:** Read-Only Sub-Agent Session View delivered (2026-04-20 19:06Z). Users can click sub-agent sessions in sidebar to view full conversation history, tool calls, and streaming output in read-only mode. 92 BlazorClient tests passing (+22 new), 0 code issues.

## Deferred

- Config validation (warn on typo keys) — follow-up improvement
- FileAccess deep merge alignment — separate improvement
