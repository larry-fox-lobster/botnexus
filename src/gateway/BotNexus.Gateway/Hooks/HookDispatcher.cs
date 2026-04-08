using System.Collections.Concurrent;
using BotNexus.Gateway.Abstractions.Hooks;

namespace BotNexus.Gateway.Hooks;

/// <summary>
/// Collects registered <see cref="IHookHandler{TEvent, TResult}"/> instances and dispatches
/// events in priority order, returning all non-null results.
/// </summary>
public sealed class HookDispatcher : IHookDispatcher
{
    // Key: (TEvent, TResult) tuple → value: List<object> of IHookHandler<TEvent, TResult>
    private readonly ConcurrentDictionary<(Type Event, Type Result), List<object>> _handlers = new();
    private readonly Lock _sync = new();

    public void Register<TEvent, TResult>(IHookHandler<TEvent, TResult> handler)
        where TResult : class
    {
        ArgumentNullException.ThrowIfNull(handler);

        var key = (typeof(TEvent), typeof(TResult));
        lock (_sync)
        {
            var list = _handlers.GetOrAdd(key, _ => []);
            list.Add(handler);
            list.Sort((a, b) =>
                ((IHookHandler<TEvent, TResult>)a).Priority
                    .CompareTo(((IHookHandler<TEvent, TResult>)b).Priority));
        }
    }

    public async Task<IReadOnlyList<TResult>> DispatchAsync<TEvent, TResult>(
        TEvent hookEvent,
        CancellationToken ct = default)
        where TResult : class
    {
        ArgumentNullException.ThrowIfNull(hookEvent);

        var key = (typeof(TEvent), typeof(TResult));
        List<object>? snapshot;

        lock (_sync)
        {
            if (!_handlers.TryGetValue(key, out var list) || list.Count == 0)
                return [];

            snapshot = [.. list];
        }

        var results = new List<TResult>();
        foreach (var raw in snapshot)
        {
            ct.ThrowIfCancellationRequested();
            var handler = (IHookHandler<TEvent, TResult>)raw;
            var result = await handler.HandleAsync(hookEvent, ct).ConfigureAwait(false);
            if (result is not null)
                results.Add(result);
        }

        return results;
    }
}
