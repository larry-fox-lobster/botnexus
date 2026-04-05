namespace BotNexus.Channels.Telegram;

/// <summary>
/// Configuration options for the Telegram channel adapter.
/// </summary>
/// <remarks>
/// Phase 2 stub options used for DI wiring and host configuration validation.
/// A full adapter would consume these values when creating Bot API clients and
/// filtering inbound updates.
/// </remarks>
public sealed class TelegramOptions
{
    /// <summary>
    /// Gets or sets the Telegram bot token used to authenticate Bot API calls.
    /// </summary>
    public string? BotToken { get; set; }

    /// <summary>
    /// Gets or sets the webhook URL when running in webhook mode.
    /// </summary>
    public string? WebhookUrl { get; set; }

    /// <summary>
    /// Gets the allow-list of Telegram chat IDs that can interact with this bot.
    /// </summary>
    public ICollection<long> AllowedChatIds { get; } = [];
}
