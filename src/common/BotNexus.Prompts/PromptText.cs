namespace BotNexus.Prompts;

/// <summary>
/// Represents prompt text.
/// </summary>
public static class PromptText
{
    /// <summary>
    /// Executes normalize structured section.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The normalize structured section result.</returns>
    public static string NormalizeStructuredSection(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n').Select(static line => line.TrimEnd());
        return string.Join("\n", lines).Trim();
    }

    /// <summary>
    /// Executes normalize capability ids.
    /// </summary>
    /// <param name="capabilities">The capabilities.</param>
    /// <returns>The normalize capability ids result.</returns>
    public static IReadOnlyList<string> NormalizeCapabilityIds(IEnumerable<string> capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        return capabilities
            .Select(capability => capability.Trim().ToLowerInvariant())
            .Where(static capability => capability.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static capability => capability, StringComparer.Ordinal)
            .ToList();
    }
}