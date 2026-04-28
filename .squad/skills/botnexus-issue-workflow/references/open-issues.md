# Open Issues — sytone/botnexus

Last updated: 2026-04-28

| # | Area | Title | Type | Status |
|---|---|---|---|---|
| #23 | Sessions | SQLite session store global lock blocks multi-agent concurrency | bug | open |
| #24 | Gateway | No tool execution timeout or stuck-turn recovery | bug | open |
| #25 | Portal | NO_REPLY sentinel visible as literal text in Blazor UI | bug | open |
| #26 | Gateway | Steering messages not visible in conversation flow | bug | open |
| #27 | Gateway | Message queue injection timing | bug | open |
| #28 | Agents | ask_user tool — interactive mid-turn user input | enhancement | open |
| #29 | Agents | Prompt templates — named parameterised prompt library | enhancement | open |
| #31 | Config | Dynamic configuration reload without gateway restart | enhancement | open |
| #32 | Memory | Memory persistence lifecycle | enhancement | open |
| #34 | Portal | Dynamic agent list in sidebar | enhancement | open |
| #36 | CLI | Global --target option for all commands | enhancement | closed (merged #42) |
| #37 | Portal | Conversation-first sidebar and chat UI | enhancement | in progress (wt-37) |
| #41 | Portal | Show gateway build info — restart time and commit SHA | enhancement | PR #43 open |

## Active PRs

| PR | Issue | Description | Status |
|---|---|---|---|
| #43 | #41 | Portal gateway build info | Open — CI green |
| #3 | — | Audio transcription test coverage | Open |

## Active Worktrees

| Path | Branch | Issue |
|---|---|---|
| ~/projects/botnexus | main | — |
| ~/projects/botnexus-wt-36 | improvement/36-cli-global-target | #36 (merged, cleanup pending) |
| ~/projects/botnexus-wt-37 | feat/37-portal-conversation-ui | #37 |
| ~/projects/botnexus-wt-41 | feat/41-gateway-build-info | #41 |
