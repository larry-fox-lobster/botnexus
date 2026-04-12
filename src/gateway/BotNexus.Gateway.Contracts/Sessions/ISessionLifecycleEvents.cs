namespace BotNexus.Gateway.Abstractions.Sessions;

/// <summary>
/// Defines the contract for isession lifecycle events.
/// </summary>
public interface ISessionLifecycleEvents
{
    event Func<SessionLifecycleEvent, CancellationToken, Task>? SessionChanged;
}
