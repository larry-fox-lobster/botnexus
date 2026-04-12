using BotNexus.Gateway.Abstractions.Models;

namespace BotNexus.Gateway.Abstractions.Agents;

/// <summary>
/// Provides agent descriptors from an external configuration source.
/// </summary>
public interface IAgentConfigurationSource
{
    /// <summary>
    /// Loads agent descriptors from this source.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded descriptors.</returns>
    Task<IReadOnlyList<AgentDescriptor>> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Watches for configuration changes and invokes the callback with the latest descriptor set.
    /// </summary>
    /// <param name="onChanged">Callback invoked when configuration changes.</param>
    /// <returns>A disposable watcher handle, or <c>null</c> if watching is unsupported.</returns>
    IDisposable? Watch(Action<IReadOnlyList<AgentDescriptor>> onChanged);
}
