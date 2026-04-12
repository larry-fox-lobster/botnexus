namespace BotNexus.Prompts;

/// <summary>
/// Represents prompt runtime info.
/// </summary>
public sealed record PromptRuntimeInfo
{
    /// <summary>
    /// Gets or sets the agent id.
    /// </summary>
    public string? AgentId { get; init; }
    /// <summary>
    /// Gets or sets the host.
    /// </summary>
    public string? Host { get; init; }
    /// <summary>
    /// Gets or sets the os.
    /// </summary>
    public string? Os { get; init; }
    /// <summary>
    /// Gets or sets the arch.
    /// </summary>
    public string? Arch { get; init; }
    /// <summary>
    /// Gets or sets the provider.
    /// </summary>
    public string? Provider { get; init; }
    /// <summary>
    /// Gets or sets the model.
    /// </summary>
    public string? Model { get; init; }
    /// <summary>
    /// Gets or sets the default model.
    /// </summary>
    public string? DefaultModel { get; init; }
    /// <summary>
    /// Gets or sets the shell.
    /// </summary>
    public string? Shell { get; init; }
    /// <summary>
    /// Gets or sets the channel.
    /// </summary>
    public string? Channel { get; init; }
    /// <summary>
    /// Gets or sets the capabilities.
    /// </summary>
    public IReadOnlyList<string>? Capabilities { get; init; }
}