# Design Review: bug-blazor-autoscroll

**Reviewed by:** Leela (Lead / Architect)
**Date:** 2025-07-18
**Spec:** `docs/planning/bug-blazor-autoscroll/design-spec.md`
**Regression of:** `improvement-blazor-chat-autoscroll` (Apr '26, archived)

---

## Root Cause Assessment

The JS interop functions and container references are intact — no renames, no missing IDs. The bug is a **race condition between scroll execution and markdown rendering** inside `OnAfterRenderAsync`.

### Sequence of Failure

In `ChatPanel.razor` lines 367–417, `OnAfterRenderAsync` does three things in order:

1. **Scrolls** — calls `chatScroll.forceScrollToBottom` or `chatScroll.scrollToBottom` via JS interop
2. **Renders markdown** — iterates `State.Messages`, calls `BotNexus.renderMarkdown` for un-cached messages, populates `_markdownCache`
3. **Triggers re-render** — calls `StateHasChanged()` if any markdown was rendered

The problem: step 1 scrolls to the current `scrollHeight`, but step 3 triggers a second `OnAfterRenderAsync` cycle where new DOM content (the rendered markdown replacing raw text) has changed the layout. The `forceScrollToBottom` from step 1 used `requestAnimationFrame` which may execute *between* render cycles with a stale `scrollHeight`. The second cycle runs the *smart* scroll (`scrollToBottom`) which checks whether the user is "near bottom" — but the DOM grew from markdown rendering, so the threshold check fails and no scroll happens.

### Contributing Factor — Streaming Path

During streaming, `Home.razor` line 42 wires `Manager.OnStateChanged += HandleStateChanged` which calls `InvokeAsync(StateHasChanged)` on every token. Each token triggers `OnAfterRenderAsync` → smart scroll. If the DOM update from one token pushes the viewport beyond the 100px threshold before the next scroll fires, auto-scroll silently disengages. This creates intermittent scroll failures during streaming that get worse as message content grows.

### What the Original Fix Missed

The archived spec (`improvement-blazor-chat-autoscroll`) describes the *what* (JS interop + near-bottom threshold) but not the *when*. It didn't account for the Blazor render lifecycle interaction with markdown caching. The original implementation worked when markdown rendering was absent or trivial, but broke once content-heavy responses triggered re-render cycles.

---

## Fix Contracts

### Contract 1: Separate Scroll from Markdown Rendering

`OnAfterRenderAsync` must scroll **after** all DOM-mutating work is complete, not before. The fix inverts the current order:

```
OnAfterRenderAsync:
  1. Render markdown (populate _markdownCache, do NOT call StateHasChanged yet)
  2. If markdown was rendered, call StateHasChanged() and RETURN
     (the re-render will call OnAfterRenderAsync again with up-to-date DOM)
  3. Only scroll AFTER no more markdown needs rendering (needsRender == false)
```

This ensures scroll always targets the final DOM state.

### Contract 2: JS `forceScrollToBottom` Must Double-Check

```javascript
forceScrollToBottom: function (element) {
    if (!element) return;
    requestAnimationFrame(function () {
        element.scrollTop = element.scrollHeight;
        // Double-check after micro-task to catch late DOM mutations
        setTimeout(function () {
            element.scrollTop = element.scrollHeight;
        }, 50);
    });
}
```

The `setTimeout` backstop catches any residual DOM changes from async Blazor rendering.

### Contract 3: Smart Scroll Threshold Must Account for Streaming Growth

`scrollToBottom` should use a slightly larger threshold during active streaming:

```javascript
scrollToBottom: function (element, isStreaming) {
    if (!element) return;
    var threshold = isStreaming ? 200 : 100;
    var isNearBottom = element.scrollHeight - element.scrollTop - element.clientHeight < threshold;
    if (isNearBottom) {
        element.scrollTop = element.scrollHeight;
    }
}
```

The Blazor component passes the streaming state:
```csharp
await JS.InvokeVoidAsync("chatScroll.scrollToBottom", _messagesContainer, State.IsStreaming);
```

### Contract 4: Scroll-on-Send Guarantee

`SendMessage()` sets `_forceScrollNext = true`. This is correct. No change needed — the force path is reliable once Contract 1 fixes the ordering.

### Contract 5: Session Switch / Initial Load

The `_wasActive` detection on lines 378–382 sets `_forceScrollNext = true` when the panel becomes visible. This is correct and sufficient once the ordering fix is applied.

---

## Wave Breakdown

### Wave 1: Fix Implementation (Fry)

**Tasks:**
1. **Reorder `OnAfterRenderAsync` in `ChatPanel.razor`** — markdown rendering first, scroll last. Only scroll when `needsRender == false` (all markdown cached). When `needsRender == true`, call `StateHasChanged()` and return without scrolling.
2. **Update `chat.js` `forceScrollToBottom`** — add `setTimeout` backstop (50ms) after `requestAnimationFrame`.
3. **Update `chat.js` `scrollToBottom`** — accept optional `isStreaming` parameter, use 200px threshold when streaming.
4. **Update the JS interop call** in `ChatPanel.razor` to pass `State.IsStreaming` to `scrollToBottom`.

**Files to modify:**
- `src/extensions/BotNexus.Extensions.Channels.SignalR.BlazorClient/Components/ChatPanel.razor` (lines 367–417)
- `src/extensions/BotNexus.Extensions.Channels.SignalR.BlazorClient/wwwroot/js/chat.js`

### Wave 2: Verification & Polish (Hermes + Kif)

**Hermes (Testing):**
- Manual test matrix against all edge cases in the spec table (7 scenarios)
- If a Blazor test harness exists, add a render-lifecycle test confirming scroll fires after final DOM state

**Kif (Docs):**
- No doc updates expected unless the JS API surface changes (it doesn't — only internal behavior)

### Anticipatory Work

Hermes can prepare the manual test checklist from the spec's edge case table immediately. No implementation dependency for that.

---

## Risks & Edge Cases

| Risk | Mitigation |
|------|------------|
| Reorder may cause first-render flash (raw markdown visible briefly before rendered HTML) | Already the current behavior — markdown is rendered async today. No regression. |
| `setTimeout(50)` backstop could cause visible jump | 50ms is below perceptible threshold. User won't notice. |
| Streaming threshold increase (200px) may scroll when user doesn't want it | 200px ≈ 2-3 lines of text. Only applies during active streaming. Acceptable trade-off. |
| `StateHasChanged()` loop if markdown rendering keeps finding new messages | Loop terminates because `_markdownCache` is populated — each message is rendered exactly once. |
| Thread safety of `_markdownCache` dictionary | Blazor WASM is single-threaded. No concern. Blazor Server would need `ConcurrentDictionary` but this project is WASM. |

---

## Files to Modify

| File | Change |
|------|--------|
| `src/extensions/BotNexus.Extensions.Channels.SignalR.BlazorClient/Components/ChatPanel.razor` | Reorder `OnAfterRenderAsync`: markdown first, scroll last. Pass `State.IsStreaming` to smart scroll. |
| `src/extensions/BotNexus.Extensions.Channels.SignalR.BlazorClient/wwwroot/js/chat.js` | Add `setTimeout` backstop to `forceScrollToBottom`. Accept `isStreaming` param in `scrollToBottom` with dynamic threshold. |

No other files need changes. No backend impact. No new dependencies.

---

## Decision

**Approach:** Fix the render-then-scroll ordering in `OnAfterRenderAsync` and harden the JS scroll functions. This is a 2-file fix, small blast radius, no architectural changes.

**Assigned to:** Fry (Wave 1 implementation), Hermes (Wave 2 verification).
