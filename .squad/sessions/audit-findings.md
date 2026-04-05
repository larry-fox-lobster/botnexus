# Port Audit: pi-mono → BotNexus

**Audited by:** Leela (Lead/Architect)
**Scope:** Providers, Agent Core, Coding Agent — full side-by-side comparison
**BotNexus branch:** `main`

## Summary

This audit compares the pi-mono TypeScript reference implementation against the BotNexus C# port across three layers: providers (LLM integration), agent core (loop/events/state), and coding agent (tools/session/extensions). The port captures the structural architecture well but has significant behavioral gaps that would cause runtime differences. The most impactful gaps are in signature round-tripping (breaking multi-turn extended thinking), tool schema/semantics divergence (breaking model tool-calling patterns), missing retry/overflow recovery (causing session crashes on transient errors), and streaming event ordering differences.

| Area | Critical | Important | Minor | Total |
|------|----------|-----------|-------|-------|
| Providers | 15 | 28 | 18 | 61 |
| Agent Core | 5 | 8 | 7 | 20 |
| Coding Agent | 13 | 26 | 10 | 49 |
| **Total** | **33** | **62** | **35** | **130** |

---

## Critical Issues (Logic Bugs / Missing Features)

### C-1: Redacted thinking data stored in wrong field (Anthropic)
- **Pi-mono**: `providers/anthropic.ts:296-304` — stores `data` in `thinkingSignature`, text as `"[Reasoning redacted]"`
- **BotNexus**: `Providers.Anthropic/AnthropicProvider.cs:358-361` — stores opaque `data` into `textAccumulators[index]`, signature stays null
- **Fix**: Store `data` in `signatureAccumulators[index]`, set text to `"[Reasoning redacted]"`. Round-trip breaks without this.
- **Assigned to**: Farnsworth

### C-2: Missing thinkingSignature/textSignature round-trip (OpenAI Responses)
- **Pi-mono**: `providers/openai-responses-shared.ts:430-449` — stores JSON as signatures on content blocks
- **BotNexus**: `Providers.OpenAI/OpenAIResponsesProvider.cs:617-625` — neither signature stored
- **Fix**: Store reasoning item JSON as `ThinkingSignature`, text signature as `TextSignature`. Multi-turn extended thinking breaks without this.
- **Assigned to**: Farnsworth

### C-3: Assistant message conversion loses signatures (OpenAI Responses)
- **Pi-mono**: `providers/openai-responses-shared.ts:170-213` — parses signatures back for API resubmission, handles `isDifferentModel`
- **BotNexus**: `Providers.OpenAI/OpenAIResponsesProvider.cs:291-341` — ignores signatures, no `isDifferentModel` handling
- **Fix**: Implement full signature parsing and `isDifferentModel` function_call ID clearing.
- **Assigned to**: Farnsworth

### C-4: Tool result images not forwarded (OpenAI Completions)
- **Pi-mono**: `providers/openai-completions.ts:644-707` — creates synthetic user message with images for vision
- **BotNexus**: `Providers.OpenAI/OpenAICompletionsProvider.cs:374-391` — images silently dropped
- **Fix**: Extract images from tool results, create synthetic user+assistant messages before the tool result.
- **Assigned to**: Farnsworth

### C-5: content_filter stop reason maps to wrong value (Completions)
- **Pi-mono**: `providers/openai-completions.ts:786` — maps to `error` + errorMessage
- **BotNexus**: `Providers.OpenAI/OpenAICompletionsProvider.cs:766` — maps to `Sensitive`, no error message
- **Fix**: Map to `Error` with errorMessage, or add errorMessage to `MapStopReason` return.
- **Assigned to**: Farnsworth

### C-6: Missing hasToolHistory() — empty tools array (Completions)
- **Pi-mono**: `providers/openai-completions.ts:41-53, 401-404` — sends `tools=[]` when history has tool calls but no active tools
- **BotNexus**: `Providers.OpenAI/OpenAICompletionsProvider.cs:247-249` — omits tools entirely
- **Fix**: Add `HasToolHistory()` check. Anthropic via LiteLLM/proxy rejects requests without this.
- **Assigned to**: Farnsworth

### C-7: Missing service_tier support (OpenAI Responses)
- **Pi-mono**: `providers/openai-responses.ts:52-56, 231-251` — serviceTier + cost multiplier (flex=0.5x, priority=2x)
- **BotNexus**: Not present
- **Fix**: Add `ServiceTier` option, send in payload, implement cost multiplier.
- **Assigned to**: Farnsworth

### C-8: AdjustMaxTokensForThinking algorithm diverges (SimpleOptions)
- **Pi-mono**: `providers/simple-options.ts:22-46` — ADDS thinkingBudget to baseMaxTokens, allows budget=0
- **BotNexus**: `Providers.Core/Utilities/SimpleOptionsHelper.cs:85-109` — doesn't add, floors budget at 1024
- **Fix**: Port the TS formula: `maxTokens = Min(base + budget, modelMax)`. Example: base=8000, budget=8192 → TS:16192, C#:8000.
- **Assigned to**: Farnsworth

### C-9: Missing silent overflow detection (ContextOverflow)
- **Pi-mono**: `utils/overflow.ts:122-128` — detects `stop=stop` but `usage > contextWindow`
- **BotNexus**: `Providers.Core/Utilities/ContextOverflowDetector.cs` — only inspects error strings
- **Fix**: Add overload checking usage vs contextWindow when stopReason is Stop.
- **Assigned to**: Farnsworth

### C-10: Stop reason mapping diverges (Anthropic)
- **Pi-mono**: `providers/anthropic.ts:885-905` — `refusal`→`error`, `pause_turn`→`stop`, `sensitive`→`error`
- **BotNexus**: `Providers.Anthropic/AnthropicProvider.cs:942-952` — maps to distinct `Refusal`/`PauseTurn`/`Sensitive` enum values
- **Fix**: Collapse to TS values or ensure ALL downstream consumers handle the new enum values.
- **Assigned to**: Farnsworth

### C-11: LlmStream.Push() missing post-done guard
- **Pi-mono**: `utils/event-stream.ts:20-35` — `if (this.done) return;` silently drops post-terminal events
- **BotNexus**: `Providers.Core/Streaming/LlmStream.cs:26-39` — no guard, events written after DoneEvent/ErrorEvent
- **Fix**: Add `_done` flag to guard `Push()`.
- **Assigned to**: Farnsworth

### C-12: supportsXhigh — over-broad opus match + extra Reasoning guard
- **Pi-mono**: Matches `opus-4-6`/`opus-4.6` only, no Reasoning prereq
- **BotNexus**: Matches `opus` anywhere in ID, has `if (!model.Reasoning) return false`
- **Fix**: Match specific opus versions, remove Reasoning guard.
- **Assigned to**: Farnsworth

### C-13: ThinkingBudgets structural mismatch
- **Pi-mono**: `providers/simple-options.ts` — maps level → single `number` (budget)
- **BotNexus**: Maps level → `ThinkingBudgetLevel(ThinkingBudget, MaxTokens)` compound type
- **Fix**: Flatten to `int?` per level or ensure all consumers understand the compound type.
- **Assigned to**: Farnsworth

### C-14: Thinking content dropped during agent message conversion
- **Pi-mono**: `agent-loop.ts:276-319` — `AssistantMessage.content` is heterogeneous array of `TextContent | ThinkingContent | ToolCall`, all preserved
- **BotNexus**: `Loop/MessageConverter.cs:56-63` — `ToAgentMessage` joins only `TextContent` via `.OfType<TextContent>()`, discards all `ThinkingContent` blocks
- **Fix**: `AssistantAgentMessage` needs a content block list. `MessageConverter.ToAgentMessage` must preserve thinking blocks for round-trip.
- **Assigned to**: Bender

### C-15: Abort does not emit agent_end event
- **Pi-mono**: `agent.ts:452-454` — catch block calls `handleRunFailure(error, aborted)` for ALL errors including abort, always emits `agent_end`
- **BotNexus**: `Agent.cs:425-428` — `OperationCanceledException` caught and re-thrown WITHOUT emitting `AgentEndEvent`
- **Fix**: Catch `OperationCanceledException`, create `AssistantAgentMessage` with `StopReason.Aborted`, emit `AgentEndEvent`, then re-throw.
- **Assigned to**: Bender

### C-16: Exceptions propagate to callers — pi-mono swallows them
- **Pi-mono**: `agent.ts:450-456` — `runWithLifecycle` catches ALL errors, calls `handleRunFailure`, does NOT re-throw
- **BotNexus**: `Agent.cs:425-460` — Both `OperationCanceledException` and general exceptions re-thrown
- **Fix**: Swallow exceptions after emitting `agent_end` (matching pi-mono), or document new contract and wrap all callers.
- **Assigned to**: Bender

### C-17: ContinueAsync suppresses follow-up messages when steering is drained
- **Pi-mono**: `agent.ts:334-336` — `continue()` calls `runPromptMessages` with `skipInitialSteeringPoll: true`; follow-up messages still work
- **BotNexus**: `Agent.cs:552-558` — `_suppressFollowUpDrainForNextRun` replaces entire follow-up delegate with no-op for the whole run
- **Fix**: Remove `_suppressFollowUpDrainForNextRun`. Implement `skipInitialSteeringPoll` — first `GetSteeringMessages` call returns empty, subsequent drain normally.
- **Assigned to**: Bender

### C-18: ContinueAsync drains queues regardless of last message role
- **Pi-mono**: `agent.ts:328-350` — `continue()` only drains queues when `lastMessage.role === "assistant"`
- **BotNexus**: `Agent.cs:216-235` — `ContinueAsync()` calls `DrainQueuedMessages()` unconditionally
- **Fix**: Gate `DrainQueuedMessages()` behind check: only drain when last message is assistant.
- **Assigned to**: Bender

### C-19: Edit tool missing NFKC Unicode normalization in fuzzy matching
- **Pi-mono**: `core/tools/edit-diff.ts:36` — applies NFKC normalization so ligatures/width variants match
- **BotNexus**: `Tools/EditTool.cs:336-384` — no NFKC normalization
- **Fix**: Add `string.Normalize(NormalizationForm.FormKC)` in fuzzy matching path.
- **Assigned to**: Bender

### C-20: Edit tool incomplete Unicode character coverage
- **Pi-mono**: `core/tools/edit-diff.ts:34-54` — maps 11+ additional chars (smart quotes `\u201A/B/E/F`, dashes `\u2010-\u2015/\u2212`, all special spaces)
- **BotNexus**: `Tools/EditTool.cs:374-384` — missing these characters
- **Fix**: Port the full Unicode normalization map from pi-mono.
- **Assigned to**: Bender

### C-21: ShellTool non-zero exit code treated as success
- **Pi-mono**: `core/tools/bash.ts:360-363` — reports non-zero exit as error with code
- **BotNexus**: `Tools/ShellTool.cs:138-152` — returns exit code in output but no error flag
- **Fix**: Set `is_error=true` when exit code ≠ 0. Model can't detect command failures without this.
- **Assigned to**: Bender

### C-22: ShellTool mandatory 120s default timeout
- **Pi-mono**: `core/tools/bash.ts:29` — default `timeout=120`
- **BotNexus**: `Tools/ShellTool.cs:26,47-49` — no default timeout
- **Fix**: Add `120` as default timeout. Long-running builds get silently killed without clear messaging otherwise.
- **Assigned to**: Bender

### C-23: GrepTool missing `literal` parameter
- **Pi-mono**: `core/tools/grep.ts:28-30` — supports `literal: true` for non-regex searches
- **BotNexus**: `Tools/GrepTool.cs:36-49` — patterns always treated as regex
- **Fix**: Add `literal` boolean parameter, use `Regex.Escape()` or `-F` flag when true.
- **Assigned to**: Bender

### C-24: GrepTool .git/ directory not excluded
- **Pi-mono**: Uses ripgrep which auto-excludes `.git/`
- **BotNexus**: `Tools/GrepTool.cs:155,259` — no `.git/` exclusion
- **Fix**: Add `.git/` to exclusion list. Searches return false matches from git internals without this.
- **Assigned to**: Bender

### C-25: GrepTool schema parameter names differ
- **Pi-mono**: `core/tools/grep.ts:26-34` — `glob`, `ignoreCase`, `limit`
- **BotNexus**: `Tools/GrepTool.cs:42-45` — `include`, `ignore_case`, `max_results`
- **Fix**: Align parameter names to match pi-mono. Models trained on pi-mono schema will send wrong param names.
- **Assigned to**: Bender

### C-26: GlobTool .git/ directory not excluded + per-file git check-ignore perf issue
- **Pi-mono**: Uses `fd` which auto-excludes `.git/`
- **BotNexus**: `Tools/GlobTool.cs:116-120` — no `.git/` exclusion. `PathUtils.cs:123-159` spawns per-file `git check-ignore` subprocess
- **Fix**: Exclude `.git/` directory. Replace per-file subprocess with batch `git check-ignore --stdin` or in-memory gitignore parsing.
- **Assigned to**: Bender

### C-27: ListDirectoryTool completely different semantics
- **Pi-mono**: `core/tools/ls.ts:105-107` — flat `ls` listing, tool name `"ls"`, `path` optional
- **BotNexus**: `Tools/ListDirectoryTool.cs:22-37` — recursive tree with connectors, tool name `"list_directory"`, `path` required
- **Fix**: Rewrite to match `ls` flat-listing semantics, rename to `"ls"`, make `path` optional.
- **Assigned to**: Bender

### C-28: Auto-retry with exponential backoff completely missing
- **Pi-mono**: `core/agent-session.ts:2373-2500` — full retry logic with backoff for transient API errors
- **BotNexus**: Not implemented
- **Fix**: Implement exponential backoff retry wrapper around LLM calls. Without this, transient errors crash the session.
- **Assigned to**: Bender

### C-29: Context overflow recovery missing
- **Pi-mono**: `core/agent-session.ts:1739-1788` — detects overflow, triggers compaction, retries
- **BotNexus**: Not implemented
- **Fix**: Add overflow detection → compaction → retry cycle. Without this, overflow errors halt the session permanently.
- **Assigned to**: Bender

### C-30: Extension discovery limited to single directory
- **Pi-mono**: `core/extensions/loader.ts:474-506` — discovers from project + global + configured directories
- **BotNexus**: `Extensions/ExtensionLoader.cs:12-63` — single directory only
- **Fix**: Add global and configured directory discovery.
- **Assigned to**: Bender

### C-31: Extension API missing lifecycle hooks and runtime context
- **Pi-mono**: `core/extensions/runner.ts:535-858` — 15+ event hooks (`context`, `before_provider_request`, `before_agent_start`, `input`, `resources_discover`, `user_bash`, etc.)
- **BotNexus**: `Extensions/ExtensionRunner.cs` — tools only, no event hooks, no `ExtensionContext`
- **Fix**: Implement event hook registration and `ExtensionContext` providing session state, model registry, abort signals.
- **Assigned to**: Bender

### C-32: ReadTool schema mismatch: offset/limit vs start_line/end_line
- **Pi-mono**: `core/tools/read.ts:19-20` — `offset` (count-based start) + `limit` (number of lines)
- **BotNexus**: `Tools/ReadTool.cs:58-65` — `start_line`/`end_line` (range-based)
- **Fix**: Align schema to `offset`/`limit` semantics. A model sending `offset=50, limit=20` gets completely wrong behavior.
- **Assigned to**: Bender

### C-33: ReadTool no image resizing
- **Pi-mono**: `core/tools/read.ts:162-163` — caps images at 2000×2000
- **BotNexus**: `Tools/ReadTool.cs:140-149` — raw full-resolution images sent to LLM
- **Fix**: Add image downscaling before base64 encoding.
- **Assigned to**: Bender

---

## Important Issues (Wrong Defaults / Missing Handling)

### I-1: Interleaved thinking beta sent to adaptive models (Anthropic)
- **Pi-mono**: Gates with `!supportsAdaptiveThinking(model.id)`
- **BotNexus**: Doesn't exclude adaptive models
- **Fix**: Add adaptive model detection guard.
- **Assigned to**: Farnsworth

### I-2: Tool input_schema not normalized to {type:"object"} wrapper (Anthropic)
- **Pi-mono**: Wraps `properties`/`required` in `{type:"object"}`
- **BotNexus**: Passes raw JsonElement
- **Fix**: Normalize schema wrapping.
- **Assigned to**: Farnsworth

### I-3: User message image blocks not filtered by model capability (Anthropic)
- **Pi-mono**: Checks `model.input.includes("image")`
- **BotNexus**: Includes all images regardless of model support
- **Fix**: Filter images when model doesn't support them.
- **Assigned to**: Farnsworth

### I-4: Tool result content not sanitized for surrogates (Anthropic)
- **Pi-mono**: Calls `sanitizeSurrogates()`
- **BotNexus**: No surrogate sanitization
- **Fix**: Add surrogate pair sanitization to tool result content.
- **Assigned to**: Farnsworth

### I-5: Tool result missing "(see attached image)" placeholder (Anthropic)
- **Pi-mono**: Adds placeholder text when tool result has only images
- **BotNexus**: No placeholder
- **Fix**: Add placeholder text for image-only tool results.
- **Assigned to**: Farnsworth

### I-6: Tool result multi-text concatenation differs (Anthropic)
- **Pi-mono**: Joins all text blocks with `\n` into single string
- **BotNexus**: Returns array if >1 block
- **Fix**: Join text blocks with newline.
- **Assigned to**: Farnsworth

### I-7: Default effort for unknown reasoning level differs
- **Pi-mono**: Default = `"high"` (Anthropic)
- **BotNexus**: Default = `"medium"`
- **Fix**: Change default to `"high"`.
- **Assigned to**: Farnsworth

### I-8: Thinking config sent to non-reasoning models (Anthropic)
- **Pi-mono**: Wraps in `if (model.reasoning)` guard
- **BotNexus**: Checks only `ThinkingEnabled`, not model capability
- **Fix**: Add `model.Reasoning` guard.
- **Assigned to**: Farnsworth

### I-9: Thinking disabled sent when thinkingEnabled is unset (Anthropic)
- **Pi-mono**: Sends nothing when unset
- **BotNexus**: Sends `thinking: {type: "disabled"}` for reasoning models
- **Fix**: Only send thinking config when explicitly set.
- **Assigned to**: Farnsworth

### I-10: Missing reasoning_details encrypted reasoning for tool calls (Completions)
- **Pi-mono**: Includes encrypted `reasoning_details` in tool call messages
- **BotNexus**: Not implemented
- **Fix**: Port encrypted reasoning forwarding.
- **Assigned to**: Farnsworth

### I-11: Missing qwen/qwen-chat-template thinking formats (Completions)
- **Pi-mono**: Handles qwen-specific thinking tags
- **BotNexus**: Not implemented
- **Fix**: Add qwen thinking format support.
- **Assigned to**: Farnsworth

### I-12: ExtraHigh mapped to "high" instead of "xhigh" (Completions)
- **Pi-mono**: Uses `"xhigh"`
- **BotNexus**: Maps to `"high"`
- **Fix**: Map to `"xhigh"`.
- **Assigned to**: Farnsworth

### I-13: requiresThinkingAsText uses `<thinking>` tags in C# (Completions)
- **Pi-mono**: Explicitly avoids tags
- **BotNexus**: Wraps in `<thinking>...</thinking>`
- **Fix**: Remove tag wrapping to match pi-mono.
- **Assigned to**: Farnsworth

### I-14: ToolCall ID normalization missing (Completions)
- **Pi-mono**: Handles pipe-separated IDs, truncates to 40 chars
- **BotNexus**: Sends raw IDs
- **Fix**: Port ID normalization logic.
- **Assigned to**: Farnsworth

### I-15: Empty text/thinking block filtering missing (Completions)
- **Pi-mono**: Filters out empty blocks before sending
- **BotNexus**: Not implemented
- **Fix**: Add empty block filtering.
- **Assigned to**: Farnsworth

### I-16: Responses tool result fallback text differs
- **Pi-mono**: `"(see attached image)"`
- **BotNexus**: `"(no output)"`
- **Fix**: Use `"(see attached image)"` when images present.
- **Assigned to**: Farnsworth

### I-17: `strict` field missing from Responses tool definitions
- **Pi-mono**: Sends `strict: false`
- **BotNexus**: Omits field
- **Fix**: Add `strict: false` to tool definitions.
- **Assigned to**: Farnsworth

### I-18: InferInitiator skips ToolResultMessages in C# but not in TS (CopilotHeaders)
- **Pi-mono**: Considers ToolResult messages for initiator inference
- **BotNexus**: Skips them
- **Fix**: Include ToolResult in initiator inference.
- **Assigned to**: Farnsworth

### I-19: Surrogates replaced with U+FFFD instead of removed (UnicodeSanitizer)
- **Pi-mono**: Removes surrogates (string gets shorter)
- **BotNexus**: Replaces with `\uFFFD`
- **Fix**: Remove surrogates instead of replacing.
- **Assigned to**: Farnsworth

### I-20: JSON parser only supports object roots (StreamingJsonParser)
- **Pi-mono**: Handles any JSON type
- **BotNexus**: Prepends `{` if missing, corrupting arrays
- **Fix**: Support all JSON root types.
- **Assigned to**: Farnsworth

### I-21: BuildBaseOptions applies hardcoded defaults not in TS (SimpleOptions)
- **Pi-mono**: No such defaults
- **BotNexus**: Adds `CacheRetention=Short`, `MaxRetryDelayMs=60000`, `Transport=Sse`
- **Fix**: Remove hardcoded defaults or make them match TS behavior.
- **Assigned to**: Farnsworth

### I-22: Missing google-vertex and amazon-bedrock credential detection (EnvApiKeys)
- **Pi-mono**: `env-api-keys.ts:78-109` — checks ADC file + project + location + 6 credential sources
- **BotNexus**: Not present
- **Fix**: Add if Vertex/Bedrock support is planned.
- **Assigned to**: Farnsworth

### I-23: isStreaming semantics differ (Agent)
- **Pi-mono**: `agent.ts:446,477` — `isStreaming` true for ENTIRE run including tool execution
- **BotNexus**: `Types/AgentState.cs:91` — only true during assistant message streaming
- **Fix**: Add `IsRunning` bool or change `IsStreaming` to track full run lifecycle.
- **Assigned to**: Bender

### I-24: Additional stop reasons cause early exit (Agent Loop)
- **Pi-mono**: `agent-loop.ts:194` — only `error` and `aborted` exit early
- **BotNexus**: `Loop/AgentLoopRunner.cs:180` — also exits for `Refusal` and `Sensitive`
- **Fix**: Remove `Refusal`/`Sensitive` from early-exit check unless intentional.
- **Assigned to**: Bender

### I-25: StreamAccumulator forces StopReason.Error on error events
- **Pi-mono**: `agent-loop.ts:306-317` — preserves original stop reason from stream
- **BotNexus**: `Loop/StreamAccumulator.cs:207-208` — forces `StopReason.Error` on all error events
- **Fix**: Preserve original stop reason. Aborted streams get misclassified.
- **Assigned to**: Bender

### I-26: Partial message not in context during streaming
- **Pi-mono**: `agent-loop.ts:279-281` — partial message pushed to context on `start` event
- **BotNexus**: `Loop/AgentLoopRunner.cs:174-178` — message only added after accumulation completes
- **Fix**: Add to timeline on `MessageStartEvent` if hooks need in-progress visibility.
- **Assigned to**: Bender

### I-27: Parallel tool execution event ordering differs
- **Pi-mono**: `agent-loop.ts:401-435` — immediate results emit `tool_execution_end` inline during preparation
- **BotNexus**: `Loop/ToolExecutor.cs:109-200` — ALL starts first, then ALL ends
- **Fix**: Emit `tool_execution_end` for immediate results inline during preparation loop.
- **Assigned to**: Bender

### I-28: DefaultMessageConverter includes system messages
- **Pi-mono**: `agent.ts:28-31` — filters to `user`, `assistant`, `toolResult` only
- **BotNexus**: `Configuration/DefaultMessageConverter.cs:19-21` — also includes `system` (wrapped in `<summary>` tags)
- **Fix**: Remove `system` from filter or document the divergence.
- **Assigned to**: Bender

### I-29: Reset() cancels active run in BotNexus but not in pi-mono
- **Pi-mono**: `agent.ts:299-307` — clears state, does NOT abort active run
- **BotNexus**: `Agent.cs:361-380` — cancels CTS then clears state
- **Fix**: Decide which behavior is correct and align.
- **Assigned to**: Bender

### I-30: Tool error message format prefix differs
- **Pi-mono**: `agent-loop.ts:555` — bare `error.message`
- **BotNexus**: `Loop/ToolExecutor.cs:273` — `"Tool '{name}' failed: {message}"`
- **Fix**: Use bare error messages to match pi-mono.
- **Assigned to**: Bender

### I-31: ReadTool line-numbered output adds prefix not in pi-mono
- **Pi-mono**: `core/tools/read.ts:236` — no line number prefix
- **BotNexus**: `Tools/ReadTool.cs:211` — `N | ...` prefix eats ~20% of byte budget
- **Fix**: Match pi-mono output format or adjust byte budget for overhead.
- **Assigned to**: Bender

### I-32: ReadTool byte budget differs (50,000 vs 51,200)
- **Pi-mono**: `core/tools/truncate.ts:12` — 50,000
- **BotNexus**: `Tools/ReadTool.cs:26` — 51,200
- **Fix**: Align to 50,000.
- **Assigned to**: Bender

### I-33: ReadTool offset beyond EOF returns info vs throws error
- **Pi-mono**: `core/tools/read.ts:196-197` — throws error
- **BotNexus**: `Tools/ReadTool.cs:229-230` — returns informational text
- **Fix**: Throw error to match pi-mono (different model retry behavior).
- **Assigned to**: Bender

### I-34: ReadTool no "first line exceeds limit" detection
- **Pi-mono**: `core/tools/read.ts:210-215` — detects and handles
- **BotNexus**: Not implemented
- **Fix**: Add detection to prevent infinite retry loop when single line > 50KB.
- **Assigned to**: Bender

### I-35: ReadTool SVG treated as image instead of text
- **Pi-mono**: Excludes SVG from image MIME set
- **BotNexus**: `Tools/ReadTool.cs:263-264` — treats SVG as image (base64)
- **Fix**: Treat SVG as text.
- **Assigned to**: Bender

### I-36: ReadTool extension-based MIME detection vs magic-byte sniffing
- **Pi-mono**: Uses magic-byte sniffing
- **BotNexus**: `Tools/ReadTool.cs:242-272` — extension-based only
- **Fix**: Add magic-byte detection for common image types.
- **Assigned to**: Bender

### I-37: EditTool BOM stripped in C# but preserved in TS
- **Pi-mono**: `core/tools/edit.ts:231,245` — preserves UTF-8 BOM
- **BotNexus**: `Tools/EditTool.cs:97,108` — strips BOM
- **Fix**: Preserve BOM if present in original file.
- **Assigned to**: Bender

### I-38: EditTool no diff output returned to model
- **Pi-mono**: `core/tools/edit.ts:258-266` — returns full unified diff
- **BotNexus**: `Tools/EditTool.cs:110-115` — returns 120-char snippet
- **Fix**: Return unified diff for model self-verification.
- **Assigned to**: Bender

### I-39: ShellTool stdout/stderr separated vs interleaved
- **Pi-mono**: `core/tools/bash.ts:94-95` — interleaved output preserving chronological order
- **BotNexus**: `Tools/ShellTool.cs:114-115` — separated with headers
- **Fix**: Interleave stdout/stderr to preserve context.
- **Assigned to**: Bender

### I-40: ShellTool timeout error discards buffered output
- **Pi-mono**: `core/tools/bash.ts:376-380` — includes buffered output in timeout error
- **BotNexus**: `Tools/ShellTool.cs:124-127` — discards output
- **Fix**: Include buffered output so model can diagnose why command timed out.
- **Assigned to**: Bender

### I-41: ShellTool output truncation method differs
- **Pi-mono**: `core/tools/truncate.ts:157-230` — 2000-line OR 50KB two-tier with temp file
- **BotNexus**: `Tools/ShellTool.cs:146-149` — 50,000 chars, no line limit
- **Fix**: Add line limit (2000) alongside byte limit.
- **Assigned to**: Bender

### I-42: GrepTool no byte-level output truncation (50KB cap)
- **Pi-mono**: `core/tools/grep.ts:321` — 50KB output cap
- **BotNexus**: `Tools/GrepTool.cs:228-244` — match count limit only
- **Fix**: Add byte-level output truncation.
- **Assigned to**: Bender

### I-43: GlobTool missing `limit` parameter
- **Pi-mono**: `core/tools/find.ts:25` — model can set custom limit
- **BotNexus**: `Tools/GlobTool.cs:26` — hardcoded 1000
- **Fix**: Add `limit` parameter.
- **Assigned to**: Bender

### I-44: GlobTool tool name mismatch
- **Pi-mono**: Tool name `"find"`
- **BotNexus**: Tool name `"glob"`
- **Fix**: Rename to `"find"` for model compatibility.
- **Assigned to**: Bender

### I-45: ListDirectoryTool hidden files excluded by default
- **Pi-mono**: `core/tools/ls.ts:159-174` — shows hidden files
- **BotNexus**: `Tools/ListDirectoryTool.cs:100` — excludes hidden files
- **Fix**: Show hidden files by default. `.env`, `.gitignore`, `.github/` are invisible without this.
- **Assigned to**: Bender

### I-46: ListDirectoryTool no entry/byte limits
- **Pi-mono**: `core/tools/ls.ts:15-16,129,186` — 500 entry limit + 50KB byte limit
- **BotNexus**: Not implemented
- **Fix**: Add entry and byte limits to prevent unbounded output.
- **Assigned to**: Bender

### I-47: Compaction token estimation uses chars/4 vs actual API usage
- **Pi-mono**: `core/compaction/compaction.ts:232-290` — uses cumulative API token usage
- **BotNexus**: `Session/SessionCompactor.cs:227-248` — estimates from `chars/4` per message
- **Fix**: Use actual API usage data for accurate triggering.
- **Assigned to**: Bender

### I-48: Compaction summary injected as system-role vs user-role
- **Pi-mono**: `core/compaction/compaction.ts:88-91` — user-role message
- **BotNexus**: `Session/SessionCompactor.cs:261-268` — system-role message
- **Fix**: Use user-role for summary injection.
- **Assigned to**: Bender

### I-49: Tool results not truncated in compaction summarization
- **Pi-mono**: `core/compaction/utils.ts:89,156` — truncates tool results to 2000 chars
- **BotNexus**: `Session/SessionCompactor.cs:436-445` — no truncation
- **Fix**: Truncate tool results to 2000 chars to stay within summarization token budget.
- **Assigned to**: Bender

### I-50: No iterative compaction — always fresh summary
- **Pi-mono**: `core/compaction/compaction.ts:487-524` — iterative, preserving prior compaction context
- **BotNexus**: Always creates fresh summary from scratch
- **Fix**: Implement iterative compaction to preserve context.
- **Assigned to**: Bender

### I-51: Branch summarization completely missing
- **Pi-mono**: `core/compaction/branch-summarization.ts` — dedicated branch summarization
- **BotNexus**: Not implemented
- **Fix**: Implement branch summarization.
- **Assigned to**: Bender

### I-52: Session persistence: full-file rewrite vs append-only
- **Pi-mono**: `core/session-manager.ts:796-814` — append-only for durability
- **BotNexus**: `Session/SessionManager.cs:54-103` — full file rewrite on every save
- **Fix**: Switch to append-only writes for durability and performance.
- **Assigned to**: Bender

### I-53: Missing session entry types
- **Pi-mono**: `core/session-manager.ts:50-146` — supports `custom_message`, `branch_summary`, `custom`, `label`, `session_info`
- **BotNexus**: `Session/SessionManager.cs:417-477` — missing these types
- **Fix**: Add missing entry types for full session compatibility.
- **Assigned to**: Bender

### I-54: No date in system prompt
- **Pi-mono**: `core/system-prompt.ts:164-165` — includes current date
- **BotNexus**: Not implemented
- **Fix**: Add current date to system prompt.
- **Assigned to**: Bender

### I-55: Config list merge replaces instead of unions
- **Pi-mono**: Additive merging for `AllowedCommands`, `BlockedPaths`
- **BotNexus**: `CodingAgentConfig.cs:147-155` — replaces lists
- **Fix**: Merge additively (union) instead of replacing.
- **Assigned to**: Bender

### I-56: No global (user-level) skills directory
- **Pi-mono**: `core/skills.ts:452-453` — discovers skills from global directory
- **BotNexus**: `SkillsLoader.cs:22-33` — project-level only
- **Fix**: Add global skills directory support.
- **Assigned to**: Bender

### I-57: Per-file git check-ignore subprocess — catastrophic performance
- **Pi-mono**: Uses `fd`/`ripgrep` with native gitignore support
- **BotNexus**: `PathUtils.cs:123-159` — spawns per-file `git check-ignore` subprocess
- **Fix**: Batch via `git check-ignore --stdin` or use in-memory gitignore parsing library.
- **Assigned to**: Bender

### I-58: Missing OpenRouter Anthropic cache control injection (Completions)
- **Pi-mono**: Injects cache control headers for OpenRouter
- **BotNexus**: Not implemented
- **Fix**: Port cache control injection.
- **Assigned to**: Farnsworth

### I-59: Missing OpenRouter/Vercel routing preferences (Completions)
- **Pi-mono**: Supports routing preferences for OpenRouter and Vercel gateways
- **BotNexus**: Not implemented
- **Fix**: Port routing preference support.
- **Assigned to**: Farnsworth

### I-60: Missing OpenRouter error metadata extraction (Completions)
- **Pi-mono**: Extracts OpenRouter-specific error metadata
- **BotNexus**: Not implemented
- **Fix**: Port error metadata extraction.
- **Assigned to**: Farnsworth

### I-61: OpenAICompletionsCompat missing fields + wrong nullable semantics
- **Pi-mono**: Supports `openRouterRouting`, `vercelGatewayRouting`, `zaiToolStream`
- **BotNexus**: Missing fields; booleans should be `bool?`
- **Fix**: Add missing fields, fix nullable semantics.
- **Assigned to**: Farnsworth

### I-62: Compat `store` set to `true` instead of `false`
- **Pi-mono**: `store: false` (privacy default)
- **BotNexus**: `store: true`
- **Fix**: Change to `false`. Privacy concern.
- **Assigned to**: Farnsworth

---

## Minor Issues (Code Quality / Cleanup)

### M-1: `is_error` omitted when false instead of explicitly sent (Anthropic)
- **Assigned to**: Farnsworth

### M-2: OAuth token detection uses StartsWith vs Contains (Anthropic)
- **Assigned to**: Farnsworth

### M-3: C# filters extra StopReasons (Refusal, Sensitive) in MessageTransformer
- **Assigned to**: Farnsworth

### M-4: normalizeToolCallId delegate signature loses model/message context
- **Assigned to**: Farnsworth

### M-5: modelsAreEqual compares extra BaseUrl field + case-insensitive
- **Assigned to**: Farnsworth

### M-6: calculateCost returns new object vs mutating in-place
- **Assigned to**: Farnsworth

### M-7: ToolResultMessage.Content too broad (allows ThinkingContent)
- **Assigned to**: Farnsworth

### M-8: StopReason enum has 3 extra values (Refusal, PauseTurn, Sensitive)
- **Assigned to**: Farnsworth

### M-9: DoneEvent/ErrorEvent reason not constrained to valid subsets
- **Assigned to**: Farnsworth

### M-10: API mismatch validation missing (model.api !== provider.api)
- **Assigned to**: Farnsworth

### M-11: TextSignatureV1 interface not ported
- **Assigned to**: Farnsworth

### M-12: KnownApi/KnownProvider string unions not ported
- **Assigned to**: Farnsworth

### M-13: No test mock provider (faux.ts equivalent)
- **Assigned to**: Farnsworth

### M-14: Compat strict set to true instead of false for tools
- **Assigned to**: Farnsworth

### M-15: Compat missing reasoning/thinking stream handling
- **Assigned to**: Farnsworth

### M-16: Compat missing sanitizeSurrogates on text
- **Assigned to**: Farnsworth

### M-17: Compat usage parsing ignores cache/reasoning details
- **Assigned to**: Farnsworth

### M-18: StreamOptions.Transport defaults to Sse instead of null
- **Assigned to**: Farnsworth

### M-19: MessageStartEvent only tracks assistant messages in BotNexus
- **Assigned to**: Bender

### M-20: Missing hasQueuedMessages() public API
- **Assigned to**: Bender

### M-21: Missing public abort signal access (CancellationToken)
- **Assigned to**: Bender

### M-22: No proxy stream equivalent (proxy.ts)
- **Assigned to**: Bender

### M-23: MessageUpdateEvent shape differs (no raw provider event)
- **Assigned to**: Bender

### M-24: agentLoop() stream API not ported (only callback-based)
- **Assigned to**: Bender

### M-25: Content model flattening loses block structure
- **Assigned to**: Bender

### M-26: WriteTool success message format differs
- **Assigned to**: Bender

### M-27: EditTool error messages lack file path and edit-index detail
- **Assigned to**: Bender

### M-28: EditTool legacy oldText/newText merging differs
- **Assigned to**: Bender

### M-29: ReadTool empty file returns message vs empty content
- **Assigned to**: Bender

### M-30: ReadTool no ~ or @ prefix expansion
- **Assigned to**: Bender

### M-31: ShellTool timeout type "integer" vs "number"
- **Assigned to**: Bender

### M-32: GrepTool regex flavor mismatch (Rust vs .NET)
- **Assigned to**: Bender

### M-33: GrepTool paths relative to search path vs working dir
- **Assigned to**: Bender

### M-34: GlobTool always case-insensitive vs platform-default
- **Assigned to**: Bender

### M-35: FileMutationQueue doesn't resolve symlinks — same file gets separate locks
- **Assigned to**: Bender

---

## Documentation Gaps

### D-1: No documentation on which pi-mono providers are NOT ported
BotNexus is missing: `amazon-bedrock.ts`, `azure-openai-responses.ts`, `google.ts`, `google-shared.ts`, `google-vertex.ts`, `google-gemini-cli.ts`, `mistral.ts`, `openai-codex-responses.ts`. Should document as intentional scope or future work.

### D-2: No documentation on intentional behavioral divergences
Several areas where BotNexus intentionally differs (path containment enforcement, recursive tree listing, etc.) are not documented. Should be in a PORTING-NOTES.md.

### D-3: Session format incompatibility not documented
Session file header format differs (`"session"` vs `"session_header"`, different field names, different version). Cross-tool session files are incompatible.

### D-4: Extension API surface not documented
What events/hooks are available, what context is provided, and the tool-only limitation should be documented.

---

## Training Material Needs

### T-1: Tool schema reference card
A quick-reference showing pi-mono tool names/params vs BotNexus tool names/params would help engineers port tools correctly.

### T-2: Streaming event lifecycle diagram
Visual comparison of event ordering for: normal completion, tool execution, parallel tools, abort, error — showing both pi-mono and BotNexus timelines.

### T-3: Compaction algorithm walkthrough
Side-by-side walkthrough of the compaction trigger → cut-point → summarize → inject cycle for both implementations.

### T-4: Extension hook catalog
Complete catalog of pi-mono extension hooks with which ones are ported and which are pending.

---

## Top 10 Priority Fixes

1. **C-1/C-2/C-3**: Signature round-trip (Anthropic redacted thinking + OpenAI Responses) — multi-turn extended thinking is broken
2. **C-14**: Thinking content dropped in agent message conversion — Anthropic extended thinking breaks
3. **C-8**: AdjustMaxTokensForThinking algorithm — wrong token budgets for every thinking request
4. **C-28/C-29**: Auto-retry + context overflow recovery — transient errors crash sessions permanently
5. **C-21/C-22**: ShellTool exit code + timeout — model can't detect command failures
6. **C-24/C-26/I-57**: .git/ exclusion + git check-ignore perf — searches broken and catastrophically slow
7. **C-32/C-27/C-25**: Tool schema alignment (read params, ls semantics, grep params) — model sends wrong params
8. **C-15/C-16**: Agent abort/error lifecycle — cleanup never fires, exceptions escape
9. **C-19/C-20**: Edit tool Unicode normalization — fuzzy matching fails on smart quotes/ligatures
10. **C-11**: LlmStream post-done guard — events after terminal corrupt stream state
