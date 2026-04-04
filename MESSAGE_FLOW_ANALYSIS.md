# Message Flow Comparison: Pi vs BotNexus
## Multi-Turn Tool Calling Analysis

**Date:** 2026-04-04  
**Author:** Bender (Runtime Dev)  
**Context:** HIGH PRIORITY investigation for Jon Bullen  
**Issue:** Multi-turn tool calling may be looping infinitely  

---

## Executive Summary

This document traces the EXACT message flow for a 2-turn tool calling conversation in both Pi and BotNexus to identify divergences. The analysis focuses on the Anthropic Messages API format since both systems use Claude models via Copilot.

**Key Finding:** BotNexus implements the correct Anthropic Messages API format including:
- ✅ Tool results grouped into user messages with `tool_result` content blocks
- ✅ Tool call IDs preserved via `tool_use_id` field
- ✅ Proper alternating user/assistant message pattern
- ✅ System prompt sent at top level

**Remaining unknowns:** Need to capture actual runtime HTTP payloads to verify message ordering and tool_use_id linkage.

---

## Part 1: Pi's Message Flow

### Source Code Analysis

**Files Analyzed:**
- `agent-loop.ts` - Main agent loop logic
- `types.ts` - Type definitions

### Example Scenario: "list cron jobs"

```
Turn 1 - Initial Request:
  User sends: "list cron jobs"
  
  Context built by convertToLlm():
    messages: [
      { role: "user", content: [{ type: "text", text: "list cron jobs" }] }
    ]
  
  LLM returns: AssistantMessage with tool_use content block:
    {
      role: "assistant",
      content: [
        {
          type: "tool_use",
          id: "toolu_01A...",
          name: "cron",
          input: { action: "list" }
        }
      ]
    }
  
  Agent executes: cron tool → result
  
  Adds to context: ToolResultMessage:
    {
      role: "toolResult",  // Pi's internal role
      toolCallId: "toolu_01A...",
      toolName: "cron",
      content: [{ type: "text", text: "Found 3 cron jobs: ..." }],
      timestamp: 1234567890
    }

Turn 2 - Continuation with tool result:
  
  Context before convertToLlm():
    [
      { role: "user", ... },           // Original user message
      { role: "assistant", ... },      // Tool call
      { role: "toolResult", ... }      // Tool result (Pi's internal type)
    ]
  
  convertToLlm() transforms to Anthropic format:
    messages: [
      {
        role: "user",
        content: [{ type: "text", text: "list cron jobs" }]
      },
      {
        role: "assistant",
        content: [
          {
            type: "tool_use",
            id: "toolu_01A...",
            name: "cron",
            input: { action: "list" }
          }
        ]
      },
      {
        role: "user",  // Tool results converted to user message
        content: [
          {
            type: "tool_result",
            tool_use_id: "toolu_01A...",  // Links back to assistant's tool_use
            content: "Found 3 cron jobs: ..."
          }
        ]
      }
    ]
  
  LLM returns: AssistantMessage with text:
    {
      role: "assistant",
      content: [
        { type: "text", text: "You have 3 cron jobs scheduled: ..." }
      ],
      stop_reason: "end_turn"
    }
  
  Loop ends (no more tool calls)
```

### Key Pi Implementation Details

**Message Roles:**
- Pi uses an internal `AgentMessage` type that can be `user`, `assistant`, or `toolResult`
- The `convertToLlm()` function transforms these to Anthropic-compatible messages
- Tool results are stored internally as `toolResult` role but converted to `user` messages with `tool_result` content blocks

**Tool Result Format (Anthropic API):**
```typescript
{
  role: "user",
  content: [
    {
      type: "tool_result",
      tool_use_id: "toolu_01A...",  // Must match assistant's tool_use ID
      content: "..." // String or content blocks
    }
  ]
}
```

**Critical Points:**
1. Tool results MUST be in user messages (Anthropic requirement)
2. `tool_use_id` MUST match the original `tool_use` block's `id`
3. Multiple tool results can be in the same user message as separate content blocks
4. Messages must alternate user/assistant (no consecutive assistant or consecutive user)

---

## Part 2: BotNexus Message Flow

### Source Code Analysis

**Files Analyzed:**
- `AgentLoop.cs` - Main agent loop (lines 125-450)
- `AnthropicMessagesHandler.cs` - Anthropic API message building (lines 271-401)
- `ChatMessage.cs` - Internal message representation
- `Session.cs` - Session history storage

### Example Scenario: "list cron jobs"

```
Turn 1 - Initial Request:
  User sends: "list cron jobs"
  
  Session history starts empty, builds messages:
    history: []
    current message: "list cron jobs"
  
  ContextBuilder.BuildMessagesAsync() produces:
    [
      { Role: "user", Content: "list cron jobs", ToolCallId: null, ToolName: null, ToolCalls: null }
    ]
  
  ChatRequest sent to provider:
    Messages: [{ Role: "user", Content: "list cron jobs" }]
    SystemPrompt: "..." (at top level)
    Tools: [{ Name: "cron", Description: "...", InputSchema: {...} }]
  
  AnthropicMessagesHandler.BuildRequestPayload() converts to:
    {
      model: "claude-opus-4.6",
      system: "...",
      messages: [
        {
          role: "user",
          content: [
            { type: "text", text: "list cron jobs" }
          ]
        }
      ],
      tools: [...],
      max_tokens: 4096,
      stream: false
    }
  
  LLM returns: LlmResponse with ToolCalls:
    {
      Content: "",
      FinishReason: ToolCalls,
      ToolCalls: [
        {
          Id: "toolu_01A...",
          ToolName: "cron",
          Arguments: { action: "list" }
        }
      ]
    }
  
  Session.AddEntry() stores assistant message:
    {
      Role: Assistant,
      Content: "",
      Timestamp: ...,
      ToolCalls: [{ Id: "toolu_01A...", ToolName: "cron", Arguments: {...} }]
    }
  
  Tool executed, result stored:
    {
      Role: Tool,
      Content: "Found 3 cron jobs: ...",
      Timestamp: ...,
      ToolName: "cron",
      ToolCallId: "toolu_01A..."
    }

Turn 2 - Continuation with tool result:
  
  Session history now contains:
    [
      { Role: User, Content: "list cron jobs", ... },
      { Role: Assistant, Content: "", ToolCalls: [...] },
      { Role: Tool, Content: "Found 3 cron jobs: ...", ToolCallId: "toolu_01A...", ToolName: "cron" }
    ]
  
  AgentLoop builds messages (lines 127-145):
    history: [
      { role: "user", content: "list cron jobs" },
      { role: "assistant", content: "", toolCalls: [...] },
      { role: "tool", content: "...", toolCallId: "toolu_01A...", toolName: "cron" }
    ]
    
  ContextBuilder returns (after excluding current user message):
    messages: [
      { Role: "user", Content: "list cron jobs" },
      { Role: "assistant", Content: "", ToolCalls: [...] },
      { Role: "tool", Content: "...", ToolCallId: "toolu_01A...", ToolName: "cron" }
    ]
  
  AnthropicMessagesHandler.BuildRequestPayload() transforms (lines 276-368):
    
    Loop iteration 1: user message
      → Add { role: "user", content: [{ type: "text", text: "list cron jobs" }] }
    
    Loop iteration 2: assistant message with tool calls
      → Add {
          role: "assistant",
          content: [
            { type: "tool_use", id: "toolu_01A...", name: "cron", input: {...} }
          ]
        }
    
    Loop iteration 3: tool message (isTool = true)
      → Start accumulating user message
      → Add tool_result content block:
          {
            type: "tool_result",
            tool_use_id: "toolu_01A...",  // From message.ToolCallId
            content: "Found 3 cron jobs: ..."
          }
      → Flush accumulated user message at end
    
    Final Anthropic API payload:
      {
        model: "claude-opus-4.6",
        system: "...",
        messages: [
          {
            role: "user",
            content: [{ type: "text", text: "list cron jobs" }]
          },
          {
            role: "assistant",
            content: [
              {
                type: "tool_use",
                id: "toolu_01A...",
                name: "cron",
                input: { action: "list" }
              }
            ]
          },
          {
            role: "user",
            content: [
              {
                type: "tool_result",
                tool_use_id: "toolu_01A...",
                content: "Found 3 cron jobs: ..."
              }
            ]
          }
        ],
        tools: [...],
        max_tokens: 4096,
        stream: false
      }
  
  LLM returns final answer:
    {
      Content: "You have 3 cron jobs scheduled: ...",
      FinishReason: Stop,
      ToolCalls: null
    }
  
  Loop ends (FinishReason = Stop)
```

### Key BotNexus Implementation Details

**Message Roles:**
- BotNexus uses `MessageRole` enum: User, Assistant, Tool, System
- Session stores messages with their original roles
- `AnthropicMessagesHandler` converts Tool role to user messages with `tool_result` blocks

**Tool Result Format (lines 298-325):**
```csharp
// Tool messages: accumulate into current user message
if (isTool)
{
    // Start new user message if needed
    if (currentMessage is null)
    {
        currentMessage = new Dictionary<string, object?> { ["role"] = "user" };
        currentContentBlocks = new List<Dictionary<string, object?>>();
    }
    
    // Add tool_result block
    if (!string.IsNullOrEmpty(message.ToolCallId))
    {
        currentContentBlocks!.Add(new Dictionary<string, object?>
        {
            ["type"] = "tool_result",
            ["tool_use_id"] = message.ToolCallId,  // ✅ Preserved from tool execution
            ["content"] = message.Content ?? string.Empty
        });
    }
}
```

**Critical Implementation:**
1. ✅ Tool messages converted to user role (Anthropic requirement)
2. ✅ `tool_use_id` set from `message.ToolCallId` (preserved from execution)
3. ✅ Multiple consecutive tool messages grouped into single user message
4. ✅ System prompt sent at top level (line 378)
5. ✅ Assistant tool calls formatted as `tool_use` content blocks (lines 343-356)

---

## Part 3: Divergence Analysis

### Structural Comparison

| Aspect | Pi | BotNexus | Match? |
|--------|----|-----------:|--------|
| **Tool Result Role** | Converted to `user` | Converted to `user` | ✅ |
| **Tool Result Content Type** | `tool_result` | `tool_result` | ✅ |
| **Tool Use ID Linkage** | `tool_use_id` | `tool_use_id` | ✅ |
| **Multiple Tool Results** | Grouped in single user message | Grouped in single user message | ✅ |
| **System Prompt** | Top-level `systemPrompt` | Top-level `system` | ✅ |
| **Assistant Tool Calls** | `tool_use` content blocks | `tool_use` content blocks | ✅ |
| **Message Alternation** | Enforced by convertToLlm | Enforced by BuildRequestPayload | ✅ |

### Potential Issues (Need Runtime Verification)

**1. Tool Call ID Preservation**
- **Where it's set:** `AgentLoop.cs` line 434 - `ToolCallId: toolCall.Id`
- **Where it's used:** `AnthropicMessagesHandler.cs` line 313 - `["tool_use_id"] = message.ToolCallId`
- **Risk:** If `toolCall.Id` is not being captured correctly from LLM response, linkage breaks
- **Test:** Add logging to verify IDs match between assistant message and tool result

**2. Message Ordering**
- **AgentLoop builds messages from session history** (lines 127-153)
- **Filter excludes current user message** - are we excluding the wrong message?
- **Risk:** If we're building history incorrectly, messages could be out of order
- **Test:** Log the full `history` array before sending to provider

**3. Tool Execution Timing**
- **Tool results added immediately** after execution (line 429-434)
- **No explicit flush/commit** - is the session state consistent?
- **Risk:** Race condition or stale session data
- **Test:** Verify session history after each AddEntry call

**4. Streaming vs Non-Streaming**
- **Streaming mode** aggregates tool calls from chunks (lines 186-267)
- **Different code path** than non-streaming
- **Risk:** Streaming may lose tool call IDs or merge incorrectly
- **Test:** Compare streaming vs non-streaming tool call flows

---

## Part 4: Logging Added

### Change Made

**File:** `AnthropicMessagesHandler.cs`  
**Lines:** 440-448  
**Change:** Added INFO-level logging of full request payload

```csharp
private HttpRequestMessage CreateHttpRequest(ModelDefinition model, Dictionary<string, object?> payload, string apiKey)
{
    var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    var url = $"{model.BaseUrl.TrimEnd('/')}/v1/messages";
    
    // Log the full request payload for debugging multi-turn tool calls
    _logger.LogInformation("Anthropic Messages API Request:\nModel: {Model}\nURL: {Url}\nPayload:\n{Payload}", 
        model.Id, url, json);
    
    // ... rest of method
}
```

### What This Will Show

When you run a multi-turn tool call conversation, the logs will now contain:

```
info: Anthropic Messages API Request:
Model: claude-opus-4.6
URL: https://api.githubcopilot.com/v1/messages
Payload:
{
  "model": "claude-opus-4.6",
  "system": "...",
  "messages": [
    {
      "role": "user",
      "content": [
        { "type": "text", "text": "list cron jobs" }
      ]
    },
    {
      "role": "assistant",
      "content": [
        {
          "type": "tool_use",
          "id": "toolu_01A...",
          "name": "cron",
          "input": { "action": "list" }
        }
      ]
    },
    {
      "role": "user",
      "content": [
        {
          "type": "tool_result",
          "tool_use_id": "toolu_01A...",  ← VERIFY THIS MATCHES ABOVE
          "content": "Found 3 cron jobs: ..."
        }
      ]
    }
  ],
  "tools": [...],
  "max_tokens": 4096
}
```

**Key things to verify:**
1. ✅ `tool_use_id` in turn 2 matches `id` from turn 1
2. ✅ Messages alternate user → assistant → user
3. ✅ Tool results are in user messages, not separate role
4. ✅ System prompt is at top level, not in messages array
5. ✅ Both turns include the tools array (or verify if it should be omitted in turn 2)

---

## Part 5: Next Steps

### Immediate Actions

1. **Run test-tool-call-logging.ps1** to capture actual HTTP payloads
   ```powershell
   .\test-tool-call-logging.ps1
   ```

2. **Check the logs** for the "Anthropic Messages API Request" entries
   - Turn 1: Verify tools array is present and well-formed
   - Turn 2: Verify tool_use_id matches the ID from turn 1
   - Turn 2: Verify messages are in correct order

3. **Compare with Pi's behavior** if available
   - Pi logs Anthropic requests when DEBUG logging is enabled
   - Look for same message structure

### Debugging Checklist

If the loop is still infinite after verifying the payloads:

- [ ] Tool call IDs match between assistant message and tool result
- [ ] Messages are in correct order (not accidentally duplicated/reversed)
- [ ] Tool definitions are present in both turns (or verify if they should be omitted)
- [ ] System prompt is consistent across turns
- [ ] Tool result content is non-empty and well-formed
- [ ] FinishReason is correctly mapped (tool_use vs end_turn)
- [ ] No extra messages being added by middleware/hooks
- [ ] Session history is not corrupted or duplicated
- [ ] Streaming vs non-streaming produce identical payloads

### Code Locations for Deep Debugging

**If tool call IDs don't match:**
- Check `CopilotProvider.cs` - how are tool call IDs extracted from response?
- Check `AnthropicMessagesHandler.cs` ParseResponse - are IDs preserved?
- Check `AgentLoop.cs` line 434 - is `toolCall.Id` the right field?

**If message ordering is wrong:**
- Check `AgentLoop.cs` lines 127-153 - is the Where clause correct?
- Check `Session.cs` AddEntry - is history modified correctly?
- Check `ContextBuilder.BuildMessagesAsync` - does it reorder messages?

**If tool results aren't grouped correctly:**
- Check `AnthropicMessagesHandler.cs` lines 276-368 - the grouping logic
- Verify multiple consecutive tool messages are handled

---

## Conclusion

**Theoretical Analysis:** BotNexus appears to implement the correct Anthropic Messages API format for multi-turn tool calling. The code follows the same pattern as Pi and respects all Anthropic API requirements.

**Empirical Analysis:** Still needed. The logging added in this session will allow you to capture the actual HTTP payloads and verify:
1. Tool call ID linkage is preserved
2. Message ordering is correct
3. Tool results are formatted properly
4. No unexpected differences vs Pi

**Recommendation:** Run the test script, examine the logs, and compare with Pi's behavior. If payloads look identical but the loop still occurs, the issue may be in:
- How the LLM response is parsed
- How tool execution results are captured
- Session state management
- The loop termination logic in AgentLoop

---

**Build Status:** ✅ Solution builds successfully with logging changes  
**Test Script:** `test-tool-call-logging.ps1` ready to run  
**Logging Level:** INFO (no need to change config)  

**Author:** Bender  
**Date:** 2026-04-04  
**Status:** Analysis complete, awaiting runtime verification
