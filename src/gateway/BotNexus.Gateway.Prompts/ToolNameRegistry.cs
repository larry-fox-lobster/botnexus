namespace BotNexus.Gateway.Prompts;

/// <summary>
/// Represents tool name registry.
/// </summary>
public sealed class ToolNameRegistry
{
    private readonly Dictionary<string, string> _canonicalByNormalized = new(StringComparer.OrdinalIgnoreCase);

    public ToolNameRegistry(IEnumerable<string>? rawToolNames)
    {
        foreach (var tool in rawToolNames ?? [])
        {
            var trimmed = tool?.Trim() ?? string.Empty;
            if (trimmed.Length == 0 || _canonicalByNormalized.ContainsKey(trimmed))
            {
                continue;
            }

            _canonicalByNormalized[trimmed.ToLowerInvariant()] = trimmed;
        }
    }

    public IReadOnlySet<string> NormalizedTools => _canonicalByNormalized.Keys.ToHashSet(StringComparer.Ordinal);

    public IReadOnlyList<string> RawTools => _canonicalByNormalized.Values.ToList();

    /// <summary>
    /// Executes resolve.
    /// </summary>
    /// <param name="normalizedName">The normalized name.</param>
    /// <returns>The resolve result.</returns>
    public string Resolve(string normalizedName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedName);
        return _canonicalByNormalized.TryGetValue(normalizedName, out var canonical) ? canonical : normalizedName;
    }

    /// <summary>
    /// Executes contains.
    /// </summary>
    /// <param name="normalizedName">The normalized name.</param>
    /// <returns>The contains result.</returns>
    public bool Contains(string normalizedName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedName);
        return _canonicalByNormalized.ContainsKey(normalizedName);
    }
}