---
id: bug-blazor-autoscroll
title: "Blazor UI Auto-Scroll Not Working on New Messages"
type: bug
priority: high
status: delivered
created: 2025-07-18
tags: [bug, blazor, webui, ux, chat, regression]
---

# Bug: Blazor UI Auto-Scroll Not Working on New Messages

**Status:** Delivered
**Priority:** high
**Created:** 2025-07-18

## Problem

When the AI assistant responds in the Blazor Web UI, new message content is rendered below the current viewport but the chat does not automatically scroll down to show it. The user must manually scroll to see the response after every message, making it appear as if the assistant hasn't responded at all.

This is a **regression** of the previously delivered `improvement-blazor-chat-autoscroll` (Apr '26). The original fix either broke during subsequent changes or doesn't cover all current rendering paths.

## Impact

- **High** — directly impacts usability for every user of the Web UI
- Users think the assistant hasn't responded when it has
- Requires manual scroll after every single message
- Degrades trust in the system ("is it working?")

## Reproduction

1. Open the Blazor Web UI
2. Send any message to an agent
3. Wait for the assistant response
4. Observe: the response renders below the visible viewport — no auto-scroll occurs
5. Manually scroll down to see the response

Reproduces consistently on every message exchange.

## Expected Behavior

The chat view should automatically scroll to the bottom when new messages are received or rendered, matching standard chat UI conventions (Teams, Discord, Slack):

- **Auto-scroll to bottom** when new messages arrive and the user is already at or near the bottom
- **Do not force-scroll** if the user has manually scrolled up to read history (only auto-scroll if within a threshold of the bottom, e.g. ~100px)
- **Handle streaming responses** — scroll should follow token-by-token streaming, not just complete messages
- **Handle rapid messages** — multiple messages arriving in quick succession should not break scroll behavior
- **Scroll on send** — always scroll to bottom when the user sends a message

## Edge Cases

| Scenario | Expected Behavior |
|----------|-------------------|
| User is at bottom, new message arrives | Auto-scroll to show new message |
| User has scrolled up to read history | Do **not** force-scroll; leave viewport where user placed it |
| User scrolled up, then scrolls back to bottom | Re-enable auto-scroll for subsequent messages |
| Long streaming response (token by token) | Smoothly follow the growing message content |
| Multiple rapid messages (e.g. tool calls) | Scroll to latest; no jitter or missed scrolls |
| Session switch | Scroll to bottom of new session |
| Initial page load | Scroll to bottom of active session |

## Likely Cause

The original auto-scroll JS interop (from `improvement-blazor-chat-autoscroll`) may have been:

- Broken by changes to the message rendering component or container structure
- Not wired into the streaming/incremental render path (only fires on complete messages)
- Disconnected during a Blazor component refactor (e.g. container element ID changed)
- Race condition: scroll fires before DOM update completes

## Investigation Steps

1. Check if the original JS interop function still exists and is called
2. Verify the scroll container element reference is still valid
3. Check the message rendering lifecycle — does it invoke scroll after streaming updates?
4. Test with `console.log` in the scroll JS to confirm it fires (or doesn't)

## Notes

- Regression of: [`improvement-blazor-chat-autoscroll`](../../archived/improvement-blazor-chat-autoscroll/design-spec.md) (delivered Apr '26)
- No backend changes expected — this is a front-end/JS interop issue
- Small scope once root cause is identified
