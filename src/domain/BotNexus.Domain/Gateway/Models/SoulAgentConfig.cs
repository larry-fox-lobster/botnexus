namespace BotNexus.Gateway.Abstractions.Models;

/// <summary>
/// Represents soul agent config.
/// </summary>
public sealed class SoulAgentConfig
{
    /// <summary>
    /// Gets or sets the enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the timezone.
    /// </summary>
    public string Timezone { get; set; } = "UTC";

    /// <summary>
    /// Gets or sets the day boundary.
    /// </summary>
    public string DayBoundary { get; set; } = "00:00";

    /// <summary>
    /// Gets or sets the reflection on seal.
    /// </summary>
    public bool ReflectionOnSeal { get; set; }

    /// <summary>
    /// Gets or sets the reflection prompt.
    /// </summary>
    public string? ReflectionPrompt { get; set; }
}
