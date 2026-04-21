# Autoscroll Fix Test Verification

Date: 2026-04-20  
Verifier: Hermes (QA)

## Scope Reviewed

- `src/extensions/BotNexus.Extensions.Channels.SignalR.BlazorClient/Components/ChatPanel.razor`
- `src/extensions/BotNexus.Extensions.Channels.SignalR.BlazorClient/wwwroot/js/chat.js`

## Code Review Against Spec Edge Cases

| Scenario | Expected | Assessment |
|---|---|---|
| User is at bottom, new message arrives | Auto-scroll | **Covered.** `chatScroll.scrollToBottom` scrolls when near bottom; `ChatPanel` calls it on non-force renders. |
| User has scrolled up to read history | Do NOT force-scroll | **Covered.** Smart scroll only triggers within threshold (100px non-streaming). |
| User scrolled up, then scrolls back to bottom | Re-enable auto-scroll | **Covered.** Threshold check is stateless and re-evaluated each render, so auto-scroll resumes once near bottom again. |
| Long streaming response (token by token) | Smoothly follow content | **Covered.** Streaming path passes `State.IsStreaming`, raising threshold to 200px for smoother follow behavior. |
| Multiple rapid messages (tool calls) | Scroll latest, no jitter | **Mostly covered.** Markdown-first render ordering plus deferred re-render reduces race conditions; JS backstop in `forceScrollToBottom` helps late DOM mutations. |
| Session switch | Scroll to bottom of new session | **Covered.** Active-panel transition sets `_forceScrollNext`, invoking forced scroll on next stable render. |
| Initial page load | Scroll to bottom | **Covered.** `firstRender` path force-scrolls. |

## Ordering/Race Condition Verification

`OnAfterRenderAsync` now:
1. Renders uncached assistant markdown first (`BotNexus.renderMarkdown`)
2. Calls `StateHasChanged()` and returns if markdown was newly rendered
3. Performs scroll only on a subsequent stable render

This addresses the original race where scrolling could occur before markdown-expanded DOM height settled.

## Gaps / Concerns

- No JS unit test coverage exists for `chat.js` thresholds/backstop timing; behavior is currently validated via code review and runtime tests.
- bUnit cannot directly assert physical scroll position changes in browser layout; it validates interop call ordering and intent.
- Existing solution baseline includes unrelated warnings and a transient file-lock retry during test execution, but test outcomes are green.

## Test Coverage Assessment

### Automated (unit/integration) — feasible

- **Blazor component lifecycle intent:** verify JS interop ordering and call sequencing in `OnAfterRenderAsync` ✅
- **Session-switch trigger logic:** verify force-scroll invocation path on active change (partially inferable through interop calls) ✅
- **Streaming mode flag propagation:** verify `scrollToBottom` receives streaming flag ✅

### Manual / browser verification — still required

- Actual scroll physics/position in real DOM during rapid updates
- Threshold UX feel (100px vs 200px) with realistic message heights
- No-jitter behavior under true multi-message bursts and token streaming

## Tests Added

- `ChatPanelTests.Renders_markdown_before_autoscroll_invocation`
  - Confirms `BotNexus.renderMarkdown` occurs before `chatScroll.scrollToBottom` in JS interop invocation order, validating the intended markdown-first/stable-DOM scroll sequence.

## Validation Runs

- `dotnet build Q:\repos\botnexus\BotNexus.slnx --nologo --tl:off` ✅
- `dotnet test Q:\repos\botnexus\BotNexus.slnx --nologo --tl:off` ✅
