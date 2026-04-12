namespace BotNexus.Prompts;

public sealed record PromptContext
{
    public required string WorkspaceDir { get; init; }

    public IReadOnlyList<ContextFile> ContextFiles { get; init; } = [];

    public IReadOnlySet<string> AvailableTools { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public bool IsMinimal { get; init; }

    public string? Channel { get; init; }

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
