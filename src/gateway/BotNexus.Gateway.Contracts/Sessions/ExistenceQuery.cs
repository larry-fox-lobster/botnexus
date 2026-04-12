using BotNexus.Domain.Primitives;

namespace BotNexus.Gateway.Abstractions.Sessions;

/// <summary>
/// Represents existence query.
/// </summary>
public sealed record ExistenceQuery
{
    /// <summary>
    /// Gets or sets the from.
    /// </summary>
    public DateTimeOffset? From { get; init; }
    /// <summary>
    /// Gets or sets the to.
    /// </summary>
    public DateTimeOffset? To { get; init; }
    /// <summary>
    /// Gets or sets the type filter.
    /// </summary>
    public SessionType? TypeFilter { get; init; }
    /// <summary>
    /// Gets or sets the limit.
    /// </summary>
    public int? Limit { get; init; }
}
