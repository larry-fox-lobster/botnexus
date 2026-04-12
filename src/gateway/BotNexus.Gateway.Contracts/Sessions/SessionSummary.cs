using BotNexus.Gateway.Abstractions.Models;
using ChannelKey = BotNexus.Domain.Primitives.ChannelKey;

namespace BotNexus.Gateway.Abstractions.Sessions;

/// <summary>
/// Represents session summary.
/// </summary>
public sealed record SessionSummary(
    string SessionId,
    string AgentId,
    ChannelKey? ChannelType,
    SessionStatus Status,
    int MessageCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
