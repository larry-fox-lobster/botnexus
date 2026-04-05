using BotNexus.Gateway.Abstractions.Agents;
using BotNexus.Gateway.Abstractions.Isolation;
using BotNexus.Gateway.Abstractions.Models;

namespace BotNexus.Gateway.Isolation;

/// <summary>
/// Remote isolation strategy — delegates execution to a remote agent service via HTTP/gRPC.
/// </summary>
/// <remarks>
/// Phase 2 stub. When implemented, this will forward prompts to a remote Gateway endpoint
/// and relay remote responses and streaming events back to local callers.
/// </remarks>
public sealed class RemoteIsolationStrategy : IIsolationStrategy
{
    public string Name => "remote";

    public Task<IAgentHandle> CreateAsync(AgentDescriptor descriptor, AgentExecutionContext context, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            $"The '{Name}' isolation strategy is not yet implemented. " +
            "Use 'in-process' for development or configure a supported remote backend.");
    }
}
