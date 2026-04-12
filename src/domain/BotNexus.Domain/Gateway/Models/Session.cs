using BotNexus.Domain.Primitives;

namespace BotNexus.Gateway.Abstractions.Models;

/// <summary>
/// Domain session state without infrastructure concerns such as locks or replay buffering.
/// </summary>
public sealed record Session
{
    public required SessionId SessionId { get; init; }

    public required AgentId AgentId { get; set; }

    public ChannelKey? ChannelType { get; set; }

    public SessionType SessionType { get; set; } = SessionType.UserAgent;

    public SessionStatus Status { get; set; } = SessionStatus.Active;

    public bool IsInteractive => SessionType.Equals(SessionType.UserAgent);

    public List<SessionParticipant> Participants { get; init; } = [];

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ExpiresAt { get; set; }

    public Dictionary<string, object?> Metadata { get; init; } = [];

    public List<SessionEntry> History { get; init; } = [];

    public int MessageCount => History.Count;
}
