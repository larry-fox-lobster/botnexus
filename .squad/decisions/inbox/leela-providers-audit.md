# Providers.Core Alignment Audit

**Audited:** `src/providers/BotNexus.Providers.Core/` vs `@mariozechner/pi-ai` (`packages/ai/src/`)
**Date:** 2025-07-15
**Auditor:** Leela (Lead/Architect)
**pi-mono commit:** `1a6a58eb05f7256ecf51cce6c2cae2f9e464d712`

## Summary

**10/12 core areas aligned, 2 partial, plus 7 missing feature gaps identified.**

The C# port is a faithful representation of pi-mono's type system. The gaps are primarily:
routing compat fields on `OpenAICompletionsCompat`, the `details` generic on `ToolResultMessage`,
`ThinkingBudgets` shape divergence, missing model helper functions, and absent Vertex/Bedrock
credential detection in `EnvironmentApiKeys`. No pi-mono OAuth or context-overflow utilities are ported.

---

## Detailed Findings

### 1. Models (`LlmModel` vs `Model<TApi>`) — ✅ Aligned

**pi-mono `Model<TApi>`:**
| Field | Type |
|---|---|
| `id` | `string` |
| `name` | `string` |
| `api` | `TApi extends Api` |
| `provider` | `Provider` |
| `baseUrl` | `string` |
| `reasoning` | `boolean` |
| `input` | `("text" \| "image")[]` |
| `cost` | `{ input, output, cacheRead, cacheWrite }` (number, $/M tokens) |
| `contextWindow` | `number` |
| `maxTokens` | `number` |
| `headers?` | `Record<string, string>` |
| `compat?` | Conditional on TApi |

**BotNexus `LlmModel`:**
| Field | Type |
|---|---|
| `Id` | `string` |
| `Name` | `string` |
| `Api` | `string` |
| `Provider` | `string` |
| `BaseUrl` | `string` |
| `Reasoning` | `bool` |
| `Input` | `IReadOnlyList<string>` |
| `Cost` | `ModelCost(Input, Output, CacheRead, CacheWrite)` (decimal) |
| `ContextWindow` | `int` |
| `MaxTokens` | `int` |
| `Headers` | `IReadOnlyDictionary<string, string>?` |
| `Compat` | `OpenAICompletionsCompat?` |

**Gap:** pi-mono's `compat` is conditionally typed per API (`OpenAICompletionsCompat` for `"openai-completions"`, `OpenAIResponsesCompat` for `"openai-responses"`, `never` otherwise). C# uses a single nullable `OpenAICompletionsCompat?` type. `OpenAIResponsesCompat` is empty in pi-mono so this is acceptable today.
**Action:** None required — the generic type parameter is a TS convenience not needed in C#.

---

### 2. Messages — ⚠️ Partial

**pi-mono:**
```typescript
UserMessage      { role: "user",       content: string | (TextContent | ImageContent)[], timestamp }
AssistantMessage { role: "assistant",  content: (TextContent | ThinkingContent | ToolCall)[], api, provider, model, responseId?, usage, stopReason, errorMessage?, timestamp }
ToolResultMessage<TDetails> { role: "toolResult", toolCallId, toolName, content: (TextContent | ImageContent)[], details?: TDetails, isError, timestamp }
```

**BotNexus:**
```csharp
UserMessage(UserMessageContent Content, long Timestamp)
AssistantMessage(IReadOnlyList<ContentBlock> Content, string Api, string Provider, string ModelId, Usage Usage, StopReason StopReason, string? ErrorMessage, string? ResponseId, long Timestamp)
ToolResultMessage(string ToolCallId, string ToolName, IReadOnlyList<ContentBlock> Content, bool IsError, long Timestamp)
```

**Gaps:**
1. **❌ `ToolResultMessage.details`** — pi-mono has a generic `details?: TDetails` field used by tool implementations to attach structured metadata. C# has no equivalent.
2. **Minor:** pi-mono field is `model: string`, C# uses `ModelId: string` — semantically equivalent, just different naming.
3. **Minor:** pi-mono `ToolResultMessage.content` is typed `(TextContent | ImageContent)[]` restricting to text/image only. C# uses `IReadOnlyList<ContentBlock>` which is broader (allows thinking/toolcall blocks). Not a problem in practice.

**Action:** Add `object? Details` field to `ToolResultMessage` (or `JsonElement? Details` for type safety).

---

### 3. Content Blocks — ✅ Aligned

**pi-mono:**
| Type | Fields |
|---|---|
| `TextContent` | `type: "text"`, `text`, `textSignature?` |
| `ThinkingContent` | `type: "thinking"`, `thinking`, `thinkingSignature?`, `redacted?` |
| `ImageContent` | `type: "image"`, `data` (base64), `mimeType` |
| `ToolCall` | `type: "toolCall"`, `id`, `name`, `arguments: Record<string, any>`, `thoughtSignature?` |

**BotNexus:**
| Type | Fields |
|---|---|
| `TextContent` | `Text`, `TextSignature?` |
| `ThinkingContent` | `Thinking`, `ThinkingSignature?`, `Redacted?` |
| `ImageContent` | `Data`, `MimeType` |
| `ToolCallContent` | `Id`, `Name`, `Arguments: Dictionary<string, object?>`, `ThoughtSignature?` |

**Gap:** pi-mono defines `TextSignatureV1 { v: 1, id: string, phase?: "commentary" | "final_answer" }` as a structured form of `textSignature`. C# stores it as a plain string (which is how it's serialized anyway).
**Action:** None — `TextSignatureV1` is just a JSON payload inside the string field.

---

### 4. Streaming Events — ✅ Aligned

**pi-mono `AssistantMessageEvent` (12 variants):**
`start`, `text_start`, `text_delta`, `text_end`, `thinking_start`, `thinking_delta`, `thinking_end`, `toolcall_start`, `toolcall_delta`, `toolcall_end`, `done`, `error`

**BotNexus (12 records inheriting `AssistantMessageEvent`):**
`StartEvent`, `TextStartEvent`, `TextDeltaEvent`, `TextEndEvent`, `ThinkingStartEvent`, `ThinkingDeltaEvent`, `ThinkingEndEvent`, `ToolCallStartEvent`, `ToolCallDeltaEvent`, `ToolCallEndEvent`, `DoneEvent`, `ErrorEvent`

Every event type present. Every field (contentIndex, delta, content, partial, reason, message/error) mapped.

**pi-mono `EventStream<T,R>` → BotNexus `LlmStream`:**
- `push(event)` → `Push(evt)` ✅
- `end(result?)` → `End(result?)` ✅
- `[Symbol.asyncIterator]` → `IAsyncEnumerable<AssistantMessageEvent>` ✅
- `result()` → `GetResultAsync()` ✅

**Gap:** None.
**Action:** None.

---

### 5. Enums — ✅ Aligned

| pi-mono | BotNexus | Status |
|---|---|---|
| `StopReason`: `"stop" \| "length" \| "toolUse" \| "error" \| "aborted"` | `StopReason`: `Stop, Length, ToolUse, Error, Aborted` | ✅ |
| `ThinkingLevel`: `"minimal" \| "low" \| "medium" \| "high" \| "xhigh"` | `ThinkingLevel`: `Minimal, Low, Medium, High, ExtraHigh` | ✅ |
| `CacheRetention`: `"none" \| "short" \| "long"` | `CacheRetention`: `None, Short, Long` | ✅ |
| `Transport`: `"sse" \| "websocket" \| "auto"` | `Transport`: `Sse, WebSocket, Auto` | ✅ |

All serialized with `JsonStringEnumMemberName` matching the camelCase pi-mono values.

**Gap:** None.
**Action:** None.

---

### 6. Stream Options — ⚠️ Partial

#### StreamOptions — ✅ Aligned

| pi-mono field | BotNexus field | Status |
|---|---|---|
| `temperature?` | `Temperature` | ✅ |
| `maxTokens?` | `MaxTokens` | ✅ |
| `signal?: AbortSignal` | `CancellationToken` | ✅ (C# equivalent) |
| `apiKey?` | `ApiKey` | ✅ |
| `transport?` | `Transport` | ✅ (non-nullable, defaults to Sse) |
| `cacheRetention?` | `CacheRetention` | ✅ (non-nullable, defaults to Short) |
| `sessionId?` | `SessionId` | ✅ |
| `onPayload?` | `OnPayload` | ✅ |
| `headers?` | `Headers` | ✅ |
| `maxRetryDelayMs?` | `MaxRetryDelayMs` | ✅ (defaults to 60000) |
| `metadata?` | `Metadata` | ✅ |

#### SimpleStreamOptions — ✅ Aligned
Both add `reasoning?: ThinkingLevel` and `thinkingBudgets?: ThinkingBudgets`.

#### ThinkingBudgets — ⚠️ Different Shape

**pi-mono:**
```typescript
interface ThinkingBudgets {
    minimal?: number;   // token count
    low?: number;
    medium?: number;
    high?: number;
}
```

**BotNexus:**
```csharp
record ThinkingBudgets {
    ThinkingBudgetLevel? Minimal;  // (ThinkingBudget: int, MaxTokens: int)
    ThinkingBudgetLevel? Low;
    ThinkingBudgetLevel? Medium;
    ThinkingBudgetLevel? High;
    ThinkingBudgetLevel? ExtraHigh;  // not in pi-mono
}
```

**Gaps:**
1. pi-mono uses plain `number` per level; C# uses `ThinkingBudgetLevel(ThinkingBudget, MaxTokens)` — richer but structurally different.
2. C# adds `ExtraHigh` level not present in pi-mono.
3. pi-mono has default budgets inline in `adjustMaxTokensForThinking` (`minimal: 1024, low: 2048, medium: 8192, high: 16384`); C# doesn't embed these defaults.

**Action:** Consider whether the richer `ThinkingBudgetLevel` shape is intentional or should be simplified to match pi-mono's plain `int?` per level.

---

### 7. Tools — ✅ Aligned

**pi-mono:** `Tool<TParameters extends TSchema> { name, description, parameters: TParameters }`
**BotNexus:** `Tool(string Name, string Description, JsonElement Parameters)`

`JsonElement` is the C# equivalent of TypeBox's `TSchema` for JSON schema representation.

**Gap:** None.
**Action:** None.

---

### 8. Context — ✅ Aligned

**pi-mono:** `Context { systemPrompt?, messages: Message[], tools?: Tool[] }`
**BotNexus:** `Context(string? SystemPrompt, IReadOnlyList<Message> Messages, IReadOnlyList<Tool>? Tools)`

**Gap:** None.
**Action:** None.

---

### 9. Usage / UsageCost — ✅ Aligned

**pi-mono:**
```typescript
Usage { input, output, cacheRead, cacheWrite, totalTokens: number,
        cost: { input, output, cacheRead, cacheWrite, total: number } }
```

**BotNexus:**
```csharp
Usage { Input, Output, CacheRead, CacheWrite, TotalTokens: int,
        Cost: UsageCost(Input, Output, CacheRead, CacheWrite, Total: decimal) }
```

**Gap:** None. `decimal` gives better precision for cost calculations than `number`.
**Action:** None.

---

### 10. Client API (`LlmClient` vs `stream.ts`) — ✅ Aligned

| pi-mono | BotNexus | Status |
|---|---|---|
| `stream(model, context, options?)` | `LlmClient.Stream(model, context, options?)` | ✅ |
| `complete(model, context, options?)` | `LlmClient.CompleteAsync(model, context, options?)` | ✅ |
| `streamSimple(model, context, options?)` | `LlmClient.StreamSimple(model, context, options?)` | ✅ |
| `completeSimple(model, context, options?)` | `LlmClient.CompleteSimpleAsync(model, context, options?)` | ✅ |

Both resolve the provider from the registry and delegate. C# uses `Async` suffix per convention.

**Gap:** None.
**Action:** None.

---

### 11. Provider & Model Registries — ⚠️ Partial

#### ApiProviderRegistry — ✅ Aligned

| pi-mono | BotNexus | Status |
|---|---|---|
| `registerApiProvider(provider, sourceId?)` | `ApiProviderRegistry.Register(provider, sourceId?)` | ✅ |
| `getApiProvider(api)` | `ApiProviderRegistry.Get(api)` | ✅ |
| `getApiProviders()` | `ApiProviderRegistry.GetAll()` | ✅ |
| `unregisterApiProviders(sourceId)` | `ApiProviderRegistry.Unregister(sourceId)` | ✅ |
| `clearApiProviders()` | `ApiProviderRegistry.Clear()` | ✅ |

`IApiProvider { Api, Stream(), StreamSimple() }` maps faithfully to pi-mono's `ApiProvider<TApi, TOptions>`.

#### ModelRegistry — ⚠️ Partial

| pi-mono | BotNexus | Status |
|---|---|---|
| `getModel(provider, modelId)` | `ModelRegistry.GetModel(provider, modelId)` | ✅ |
| `getProviders()` | `ModelRegistry.GetProviders()` | ✅ |
| `getModels(provider)` | `ModelRegistry.GetModels(provider)` | ✅ |
| `calculateCost(model, usage)` | `ModelRegistry.CalculateCost(model, usage)` | ✅ |
| `supportsXhigh(model)` | — | ❌ Missing |
| `modelsAreEqual(a, b)` | — | ❌ Missing |

**Action:** Add `SupportsXhigh(LlmModel)` and `ModelsAreEqual(LlmModel?, LlmModel?)` to `ModelRegistry`.

---

### 12. Missing Features — Items in pi-ai with No Equivalent

#### 12a. OpenAICompletionsCompat — ⚠️ Partial (3 fields missing)

| pi-mono field | BotNexus | Status |
|---|---|---|
| `supportsStore?` | `SupportsStore` | ✅ |
| `supportsDeveloperRole?` | `SupportsDeveloperRole` | ✅ |
| `supportsReasoningEffort?` | `SupportsReasoningEffort` | ✅ |
| `reasoningEffortMap?` | `ReasoningEffortMap` | ✅ |
| `supportsUsageInStreaming?` | `SupportsUsageInStreaming` | ✅ |
| `maxTokensField?` | `MaxTokensField` | ✅ |
| `requiresToolResultName?` | `RequiresToolResultName` | ✅ |
| `requiresAssistantAfterToolResult?` | `RequiresAssistantAfterToolResult` | ✅ |
| `requiresThinkingAsText?` | `RequiresThinkingAsText` | ✅ |
| `thinkingFormat?` | `ThinkingFormat` | ✅ |
| `supportsStrictMode?` | `SupportsStrictMode` | ✅ |
| `openRouterRouting?` | — | ❌ Missing |
| `vercelGatewayRouting?` | — | ❌ Missing |
| `zaiToolStream?` | — | ❌ Missing |

**Action:** Add `OpenRouterRouting?`, `VercelGatewayRouting?`, and `bool ZaiToolStream` to `OpenAICompletionsCompat`.

#### 12b. EnvironmentApiKeys — ⚠️ Partial

C# `EnvironmentApiKeys` handles: simple env-var map, `github-copilot` multi-var, `anthropic` OAuth-first.

**Missing:**
- ❌ `google-vertex` ADC credential detection (checks `~/.config/gcloud/application_default_credentials.json`, `GOOGLE_CLOUD_PROJECT`, `GOOGLE_CLOUD_LOCATION`)
- ❌ `amazon-bedrock` multi-credential detection (`AWS_PROFILE`, `AWS_ACCESS_KEY_ID`, `AWS_BEARER_TOKEN_BEDROCK`, ECS/IRSA env vars)

**Action:** Add Vertex and Bedrock credential detection to `EnvironmentApiKeys.GetApiKey()`.

#### 12c. OAuth System — ❌ Not Ported

pi-mono has a complete OAuth subsystem under `utils/oauth/`:
- `OAuthProvider`, `OAuthCredentials`, `OAuthLoginCallbacks`, `OAuthPrompt` types
- CLI login flow (`cli.ts`)
- Provider registration

This is a significant feature in pi-mono used for Anthropic console OAuth and potentially other providers.

**Action:** Not immediately needed if C# consumers use API keys, but flag for future if OAuth provider auth is required.

#### 12d. Utility Gaps

| pi-mono utility | BotNexus equivalent | Status |
|---|---|---|
| `utils/event-stream.ts` | `LlmStream` | ✅ |
| `utils/json-parse.ts` | `StreamingJsonParser` | ✅ |
| `utils/sanitize-unicode.ts` | `UnicodeSanitizer` | ✅ |
| `providers/simple-options.ts` | `SimpleOptionsHelper` | ✅ |
| `providers/transform-messages.ts` | `MessageTransformer` | ✅ |
| `providers/github-copilot-headers.ts` | `CopilotHeaders` | ✅ |
| `utils/overflow.ts` | — | ❌ Missing |
| `utils/validation.ts` | — | ❌ Missing |
| `utils/hash.ts` | — | ❌ Missing |
| `utils/typebox-helpers.ts` | — | N/A (TypeBox-specific) |
| `providers/faux.ts` | — | ❌ Missing |

- **`overflow.ts`**: Context window overflow detection and message truncation. Needed if consumers do long conversations.
- **`validation.ts`**: Message validation (ensures well-formed conversations). Useful defensive check.
- **`faux.ts`**: Mock/fake provider for testing. Useful for unit tests.

**Action:** Port `overflow.ts` and `validation.ts` when needed. Port `faux.ts` for test infrastructure.

#### 12e. Type System Gaps

| pi-mono type | BotNexus equivalent | Status |
|---|---|---|
| `KnownApi` union type | Plain `string` | N/A (C# pattern) |
| `KnownProvider` union type | Plain `string` | N/A (C# pattern) |
| `ProviderStreamOptions` (`StreamOptions & Record<string, unknown>`) | Not typed | N/A (not needed in C#) |
| `StreamFunction<TApi, TOptions>` type alias | `IApiProvider` interface | ✅ (different pattern, same purpose) |
| `OpenAIResponsesCompat` | — | ❌ Missing (empty interface, low priority) |
| `OpenRouterRouting` | — | ❌ Missing |
| `VercelGatewayRouting` | — | ❌ Missing |

---

## Action Item Summary

| Priority | Item | Location | Effort |
|---|---|---|---|
| **P1** | Add `Details` field to `ToolResultMessage` | `Models/Messages.cs` | S |
| **P1** | Add `OpenRouterRouting`, `VercelGatewayRouting`, `ZaiToolStream` to `OpenAICompletionsCompat` | `Compatibility/OpenAICompletionsCompat.cs` | S |
| **P2** | Add `SupportsXhigh()` and `ModelsAreEqual()` to `ModelRegistry` | `Registry/ModelRegistry.cs` | S |
| **P2** | Add Vertex ADC + Bedrock multi-credential detection to `EnvironmentApiKeys` | `EnvironmentApiKeys.cs` | M |
| **P2** | Reconcile `ThinkingBudgets` shape (plain int vs ThinkingBudgetLevel) | `Models/ThinkingBudgets.cs` | S |
| **P3** | Port `overflow.ts` (context window overflow) | New: `Utilities/ContextOverflow.cs` | M |
| **P3** | Port `validation.ts` (message validation) | New: `Utilities/MessageValidator.cs` | S |
| **P3** | Port `faux.ts` (mock provider for tests) | New test project utility | M |
| **P4** | Add `OpenRouterRouting` / `VercelGatewayRouting` record types | New: `Compatibility/` | S |
| **P4** | Consider OAuth system port | New subsystem | L |
