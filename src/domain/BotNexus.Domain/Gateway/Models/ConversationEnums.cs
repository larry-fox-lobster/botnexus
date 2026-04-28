namespace BotNexus.Gateway.Abstractions.Models;

/// <summary>
/// Lifecycle status of a conversation.
/// </summary>
public enum ConversationStatus
{
    /// <summary>The conversation is active and accepts new sessions.</summary>
    Active,

    /// <summary>The conversation has been archived and is read-only.</summary>
    Archived
}

/// <summary>
/// Controls how a channel binding participates in message fan-out.
/// </summary>
public enum BindingMode
{
    /// <summary>Inbound and outbound — full interactive channel.</summary>
    Interactive,

    /// <summary>Outbound only — the binding receives fan-out but does not originate messages.</summary>
    NotifyOnly,

    /// <summary>No outbound fan-out — the binding is silenced.</summary>
    Muted
}

/// <summary>
/// Controls how conversations map to native channel threading models.
/// </summary>
public enum ThreadingMode
{
    /// <summary>One conversation per channel address (DMs, SMS).</summary>
    Single,

    /// <summary>The conversation maps to a native thread or topic (Teams, Slack, Telegram topics).</summary>
    NativeThread,

    /// <summary>The conversation name is prefixed on messages (iMessage fallback, SMS multi-conversation).</summary>
    Prefix
}
