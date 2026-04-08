namespace BotNexus.Gateway.Abstractions.Hooks;

// ── Before prompt build ──────────────────────────────────────────────

public sealed record BeforePromptBuildEvent(
    string AgentId,
    string CurrentPrompt,
    IReadOnlyList<object> Messages);

public sealed record BeforePromptBuildResult
{
    public string? PrependSystemContext { get; init; }
    public string? AppendSystemContext { get; init; }
}

// ── Before tool call ─────────────────────────────────────────────────

public sealed record BeforeToolCallEvent(
    string AgentId,
    string ToolName,
    string ToolCallId,
    IReadOnlyDictionary<string, object?> Arguments);

public sealed record BeforeToolCallResult
{
    public bool Denied { get; init; }
    public string? DenyReason { get; init; }
    public IReadOnlyDictionary<string, object?>? ModifiedArguments { get; init; }
}

// ── After tool call ──────────────────────────────────────────────────

public sealed record AfterToolCallEvent(
    string AgentId,
    string ToolName,
    string ToolCallId,
    string? Result,
    bool IsError);

public sealed record AfterToolCallResult;
