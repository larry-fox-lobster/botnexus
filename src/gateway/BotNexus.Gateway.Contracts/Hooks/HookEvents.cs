using BotNexus.Domain.Primitives;

namespace BotNexus.Gateway.Abstractions.Hooks;

// ── Before prompt build ──────────────────────────────────────────────

/// <summary>
/// Represents before prompt build event.
/// </summary>
public sealed record BeforePromptBuildEvent(
    AgentId AgentId,
    string CurrentPrompt,
    IReadOnlyList<object> Messages);

/// <summary>
/// Represents before prompt build result.
/// </summary>
public sealed record BeforePromptBuildResult
{
    /// <summary>
    /// Gets or sets the prepend system context.
    /// </summary>
    public string? PrependSystemContext { get; init; }
    /// <summary>
    /// Gets or sets the append system context.
    /// </summary>
    public string? AppendSystemContext { get; init; }
}

// ── Before tool call ─────────────────────────────────────────────────

/// <summary>
/// Represents before tool call event.
/// </summary>
public sealed record BeforeToolCallEvent(
    AgentId AgentId,
    string ToolName,
    string ToolCallId,
    IReadOnlyDictionary<string, object?> Arguments);

/// <summary>
/// Represents before tool call result.
/// </summary>
public sealed record BeforeToolCallResult
{
    /// <summary>
    /// Gets or sets the denied.
    /// </summary>
    public bool Denied { get; init; }
    /// <summary>
    /// Gets or sets the deny reason.
    /// </summary>
    public string? DenyReason { get; init; }
    /// <summary>
    /// Gets or sets the modified arguments.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? ModifiedArguments { get; init; }
}

// ── After tool call ──────────────────────────────────────────────────

/// <summary>
/// Represents after tool call event.
/// </summary>
public sealed record AfterToolCallEvent(
    AgentId AgentId,
    string ToolName,
    string ToolCallId,
    string? Result,
    bool IsError);

/// <summary>
/// Represents after tool call result.
/// </summary>
public sealed record AfterToolCallResult;
