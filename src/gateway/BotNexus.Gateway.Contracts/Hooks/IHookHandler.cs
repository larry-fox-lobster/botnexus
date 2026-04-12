namespace BotNexus.Gateway.Abstractions.Hooks;

/// <summary>
/// Handles a specific hook event and optionally returns a result.
/// Handlers run in <see cref="Priority"/> order (lower values run first).
/// </summary>
public interface IHookHandler<TEvent, TResult>
{
    /// <summary>Lower values run first. Default 0.</summary>
    int Priority { get; }

    Task<TResult?> HandleAsync(TEvent hookEvent, CancellationToken ct = default);
}
