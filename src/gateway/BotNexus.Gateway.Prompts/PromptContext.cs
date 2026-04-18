namespace BotNexus.Gateway.Prompts;

/// <summary>
/// Represents prompt context.
/// </summary>
public sealed record PromptContext
{
    /// <summary>
    /// Gets or sets the workspace dir.
    /// </summary>
    public required string WorkspaceDir { get; init; }

    /// <summary>
    /// Gets or sets the context files.
    /// </summary>
    public IReadOnlyList<ContextFile> ContextFiles { get; init; } = [];

    /// <summary>
    /// Gets or sets the available tools.
    /// </summary>
    public IReadOnlySet<string> AvailableTools { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets a value indicating whether is minimal.
    /// </summary>
    public bool IsMinimal { get; init; }

    /// <summary>
    /// Gets or sets the channel.
    /// </summary>
    public string? Channel { get; init; }

    /// <summary>
    /// Gets or sets the extensions.
    /// </summary>
    public IDictionary<string, object?> Extensions { get; init; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    public T? Get<T>(string key)
    {
        if (!Extensions.TryGetValue(key, out var value) || value is not T typed)
        {
            return default;
        }

        return typed;
    }
}
