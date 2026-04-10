# Research: Steering Messages Not Visible in Conversation Flow

## Problem Statement

When Jon sends a "steering message" (a follow-up message while Nova is mid-response or mid-tool-execution), the message:
- **Shows above the input bar** (as a queued/pending indicator)
- **Does NOT appear inline in the conversation** where it was actually injected

This means Jon can't see where his steering message landed relative to Nova's responses and tool calls. It breaks the conversational flow — you can't tell if the agent saw your message before or after a particular action.

## Observed Behavior

1. Nova is working (running tools, generating response)
2. Jon types a steering/follow-up message and sends it
3. Message appears above the input bar (queued indicator)
4. Message is injected into the conversation (Nova receives it)
5. **Bug**: The message never renders in the main conversation timeline
6. Jon can't see the message in context with Nova's responses

## Expected Behavior

1. Steering message should appear inline in the conversation at the point it was injected
2. Possibly with a visual indicator that it was a mid-turn injection (e.g., subtle styling, "sent while responding" label)
3. The queued indicator above the bar is fine as a transient state, but the message should "land" in the conversation once delivered

## Scope

This is a **SignalR WebUI client bug** — the message is being delivered to the agent (Nova receives and acts on it), but the frontend doesn't render it in the conversation timeline.

## Industry Reference

### Claude Code
- Queued messages show inline with a "queued" badge
- Once delivered, they appear normally in the conversation flow
- The `/btw` command explicitly handles side-questions mid-conversation

### Windsurf (Cascade)
- Has explicit "Queued Messages" feature
- "While you are waiting for Cascade to finish its current task, you can queue up new messages"
- Queued messages appear in the conversation when processed

### ChatGPT
- Messages sent while assistant is responding appear inline immediately
- Assistant may acknowledge or incorporate the new message

## Possible Causes

1. **WebSocket rendering logic**: The client may only render messages from the standard message flow, not from the steering/injection path
2. **Message type mismatch**: Steering messages may use a different event type that the renderer doesn't handle
3. **Optimistic rendering missing**: Client shows the queued indicator but never transitions it to a rendered message
4. **Session history gap**: The message may be stored in the session but the client doesn't refresh/append it to the visible conversation

## Questions for the Squad

1. What event type does the SignalR client use for steering messages vs normal messages?
2. Is the steering message stored in the session message history?
3. Does the WebUI client re-render from session state, or does it only render from real-time events?
4. Is this a known limitation of the current WebUI implementation?
