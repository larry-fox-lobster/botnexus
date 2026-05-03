namespace BotNexus.Extensions.Channels.Telegram;

/// <summary>
/// Top-level gateway configuration for the Telegram channel extension.
/// Supports both single-bot (legacy) and multi-bot configurations.
/// </summary>
/// <remarks>
/// <para>
/// <b>Multi-bot (recommended):</b> populate <see cref="Bots"/> with one entry per
/// Telegram bot token. Each entry maps a named bot to a BotNexus agent.
/// </para>
/// <para>
/// <b>Single-bot (legacy):</b> set <see cref="BotToken"/> and optionally
/// <see cref="AgentId"/> at the top level. When <see cref="Bots"/> is empty and
/// <see cref="BotToken"/> is set, the adapter synthesises a single bot entry named
/// <c>default</c> from the top-level fields.
/// </para>
/// </remarks>
public class TelegramGatewayOptions
{
    // ── Legacy single-bot fields ──────────────────────────────────────────────

    /// <summary>
    /// Legacy: bot token for a single-bot deployment.
    /// Ignored when <see cref="Bots"/> is non-empty.
    /// </summary>
    public string? BotToken { get; set; }

    /// <summary>
    /// Legacy: agent ID for a single-bot deployment.
    /// Ignored when <see cref="Bots"/> is non-empty.
    /// </summary>
    public string? AgentId { get; set; }

    /// <summary>
    /// Legacy: webhook URL for a single-bot deployment.
    /// Ignored when <see cref="Bots"/> is non-empty.
    /// </summary>
    public string? WebhookUrl { get; set; }

    /// <summary>
    /// Legacy: allow-list of chat IDs for a single-bot deployment.
    /// Ignored when <see cref="Bots"/> is non-empty.
    /// </summary>
    public ICollection<long> AllowedChatIds { get; } = [];

    /// <summary>
    /// Legacy: polling timeout in seconds for a single-bot deployment.
    /// </summary>
    public int PollingTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Legacy: streaming buffer flush interval in milliseconds.
    /// </summary>
    public int StreamingBufferMs { get; set; } = 500;

    /// <summary>
    /// Legacy: maximum Telegram message length before splitting.
    /// </summary>
    public int MaxMessageLength { get; set; } = 4096;

    // ── Multi-bot configuration ───────────────────────────────────────────────

    /// <summary>
    /// Named bot configurations. Each key is a logical bot name used for
    /// logging and HTTP client naming; each value holds the token and agent
    /// binding for that bot.
    /// When this dictionary is non-empty it takes precedence over the
    /// legacy top-level fields.
    /// </summary>
    public Dictionary<string, TelegramBotConfig> Bots { get; } = new(StringComparer.OrdinalIgnoreCase);

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the effective list of bot configurations to activate.
    /// Uses <see cref="Bots"/> when populated; otherwise synthesises a single
    /// <c>default</c> entry from the legacy top-level fields.
    /// </summary>
    internal IReadOnlyDictionary<string, TelegramBotConfig> ResolveActiveBots()
    {
        if (Bots.Count > 0)
            return Bots;

        // Legacy fallback — synthesise a single "default" bot
        var legacy = new TelegramBotConfig
        {
            BotToken = BotToken,
            AgentId = AgentId,
            WebhookUrl = WebhookUrl,
            PollingTimeoutSeconds = PollingTimeoutSeconds,
            StreamingBufferMs = StreamingBufferMs,
            MaxMessageLength = MaxMessageLength
        };
        foreach (var id in AllowedChatIds)
            legacy.AllowedChatIds.Add(id);

        return new Dictionary<string, TelegramBotConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = legacy
        };
    }
}

/// <summary>
/// Alias kept for backward compatibility with existing code and tests that
/// reference <see cref="TelegramOptions"/>.
/// </summary>
public sealed class TelegramOptions : TelegramGatewayOptions { }
