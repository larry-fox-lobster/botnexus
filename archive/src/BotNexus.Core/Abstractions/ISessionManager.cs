using BotNexus.Core.Models;

namespace BotNexus.Core.Abstractions;

/// <summary>Contract for session management.</summary>
public interface ISessionManager
{
    /// <summary>Gets or creates a session for the given key and agent.</summary>
    Task<Session> GetOrCreateAsync(string sessionKey, string agentName, CancellationToken cancellationToken = default);

    /// <summary>Saves a session to the backing store.</summary>
    Task SaveAsync(Session session, CancellationToken cancellationToken = default);

    /// <summary>Resets (clears) a session's history.</summary>
    Task ResetAsync(string sessionKey, CancellationToken cancellationToken = default);

    /// <summary>Deletes a session entirely.</summary>
    Task DeleteAsync(string sessionKey, CancellationToken cancellationToken = default);

    /// <summary>Returns all active session keys.</summary>
    Task<IReadOnlyList<string>> ListKeysAsync(CancellationToken cancellationToken = default);

    /// <summary>Sets the hidden flag for a session.</summary>
    Task SetHiddenAsync(string sessionKey, bool hidden, CancellationToken cancellationToken = default);

    /// <summary>Gets the hidden flag for a session (returns false if session doesn't exist).</summary>
    Task<bool> IsHiddenAsync(string sessionKey, CancellationToken cancellationToken = default);
}
