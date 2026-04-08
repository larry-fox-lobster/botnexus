namespace BotNexus.Gateway.Abstractions.Hooks;

/// <summary>
/// Dispatches hook events to all registered <see cref="IHookHandler{TEvent, TResult}"/>
/// instances in priority order and collects non-null results.
/// </summary>
public interface IHookDispatcher
{
    Task<IReadOnlyList<TResult>> DispatchAsync<TEvent, TResult>(TEvent hookEvent, CancellationToken ct = default)
        where TResult : class;

    void Register<TEvent, TResult>(IHookHandler<TEvent, TResult> handler)
        where TResult : class;
}
