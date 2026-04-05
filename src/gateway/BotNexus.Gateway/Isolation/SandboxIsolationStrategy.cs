using BotNexus.Gateway.Abstractions.Agents;
using BotNexus.Gateway.Abstractions.Isolation;
using BotNexus.Gateway.Abstractions.Models;

namespace BotNexus.Gateway.Isolation;

/// <summary>
/// Sandbox isolation strategy — runs agents in a restricted process with limited permissions.
/// </summary>
/// <remarks>
/// Phase 2 stub. When implemented, this will spawn a child process with reduced privileges,
/// restricted file system access, and memory/CPU limits. Suitable for untrusted agents.
/// </remarks>
public sealed class SandboxIsolationStrategy : IIsolationStrategy
{
    /// <inheritdoc />
    public string Name => "sandbox";

    /// <inheritdoc />
    public Task<IAgentHandle> CreateAsync(AgentDescriptor descriptor, AgentExecutionContext context, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            $"The '{Name}' isolation strategy is not yet implemented. " +
            "Use 'in-process' for development or contribute an implementation.");
    }
}
