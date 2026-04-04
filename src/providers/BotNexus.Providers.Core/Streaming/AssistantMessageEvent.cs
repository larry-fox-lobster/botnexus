using BotNexus.Providers.Core.Models;

namespace BotNexus.Providers.Core.Streaming;

/// <summary>
/// Event protocol for assistant message streaming.
/// Port of pi-mono's AssistantMessageEvent discriminated union.
/// </summary>
public abstract record AssistantMessageEvent(string Type);

public sealed record StartEvent(
    AssistantMessage Partial
) : AssistantMessageEvent("start");

public sealed record TextStartEvent(
    int ContentIndex,
    AssistantMessage Partial
) : AssistantMessageEvent("text_start");

public sealed record TextDeltaEvent(
    int ContentIndex,
    string Delta,
    AssistantMessage Partial
) : AssistantMessageEvent("text_delta");

public sealed record TextEndEvent(
    int ContentIndex,
    string Content,
    AssistantMessage Partial
) : AssistantMessageEvent("text_end");

public sealed record ThinkingStartEvent(
    int ContentIndex,
    AssistantMessage Partial
) : AssistantMessageEvent("thinking_start");

public sealed record ThinkingDeltaEvent(
    int ContentIndex,
    string Delta,
    AssistantMessage Partial
) : AssistantMessageEvent("thinking_delta");

public sealed record ThinkingEndEvent(
    int ContentIndex,
    string Content,
    AssistantMessage Partial
) : AssistantMessageEvent("thinking_end");

public sealed record ToolCallStartEvent(
    int ContentIndex,
    AssistantMessage Partial
) : AssistantMessageEvent("toolcall_start");

public sealed record ToolCallDeltaEvent(
    int ContentIndex,
    string Delta,
    AssistantMessage Partial
) : AssistantMessageEvent("toolcall_delta");

public sealed record ToolCallEndEvent(
    int ContentIndex,
    ToolCallContent ToolCall,
    AssistantMessage Partial
) : AssistantMessageEvent("toolcall_end");

public sealed record DoneEvent(
    StopReason Reason,
    AssistantMessage Message
) : AssistantMessageEvent("done");

public sealed record ErrorEvent(
    StopReason Reason,
    AssistantMessage Error
) : AssistantMessageEvent("error");
