### 2026-04-06: WebUI Production Enhancement — Activity WebSocket Separation

**Decision:** Activity feed now connects to a dedicated `ws://host/ws/activity` WebSocket endpoint rather than multiplexing activity events over the main chat WebSocket via `subscribe` messages.

**Rationale:** Separating activity from the chat connection provides cleaner reconnection semantics — the activity feed can reconnect independently without affecting an active chat session, and the main WebSocket stays focused on the streaming protocol. This also aligns with the Gateway's `/ws/activity` endpoint design.

**Impact:**
- **Gateway team (Farnsworth):** The `/ws/activity` endpoint must be available and serve activity events independently of the main `/ws` endpoint. If it doesn't exist yet, the activity feed will silently fail and retry.
- **No breaking changes:** The main WebSocket no longer sends `subscribe` messages, but the server should gracefully ignore unknown message types anyway.

**Files:** `src/BotNexus.WebUI/wwwroot/app.js` — `connectActivityWs()` / `disconnectActivityWs()` functions

**Also added:** `follow_up` message type support (Client→Server). The WebUI now sends `{"type": "follow_up", "content": "..."}` when users queue messages during streaming. The Gateway/runtime needs to handle this alongside the existing `steer` type.
