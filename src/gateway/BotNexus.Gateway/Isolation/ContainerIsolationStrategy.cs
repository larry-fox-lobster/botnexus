using BotNexus.Gateway.Abstractions.Agents;
using BotNexus.Gateway.Abstractions.Isolation;
using BotNexus.Gateway.Abstractions.Models;

namespace BotNexus.Gateway.Isolation;

/// <summary>
/// Container isolation strategy — runs an agent in a Docker container.
/// </summary>
/// <remarks>
/// Phase 2 stub. When implemented, this will pull/build an image, mount volumes for agent context,
/// and communicate with the isolated agent process over gRPC/HTTP.
/// </remarks>
public sealed class ContainerIsolationStrategy : IIsolationStrategy
{
    public string Name => "container";

    public Task<IAgentHandle> CreateAsync(AgentDescriptor descriptor, AgentExecutionContext context, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            $"The '{Name}' isolation strategy is not yet implemented. " +
            "Use 'in-process' for development or contribute a container runner.");
    }
}
