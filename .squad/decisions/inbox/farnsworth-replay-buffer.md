# Decision: Session replay extraction boundary

- **Date:** 2026-04-06
- **Owner:** Farnsworth

## Decision

Replay sequencing and replay-window storage belong to a dedicated `SessionReplayBuffer` abstraction, while `GatewaySession` remains responsible for session history and lifecycle metadata.

## Rationale

`GatewaySession` had separate lock domains (history + replay), indicating two independent responsibilities. Extracting replay behavior keeps lock ownership local to replay operations and leaves `GatewaySession` focused on conversation/session concerns.

## Compatibility

Keep `GatewaySession` replay-facing APIs as wrappers so existing persistence and call sites continue to work while newer paths can call `session.ReplayBuffer` directly.
