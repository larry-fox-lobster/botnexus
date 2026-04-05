using BotNexus.Gateway.Abstractions.Agents;
using BotNexus.Gateway.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace BotNexus.Gateway.Agents;

/// <summary>
/// Default implementation of <see cref="IAgentCommunicator"/> for local sub-agent calls.
/// </summary>
public sealed class DefaultAgentCommunicator : IAgentCommunicator
{
    private readonly IAgentSupervisor _supervisor;
    private readonly ILogger<DefaultAgentCommunicator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAgentCommunicator"/> class.
    /// </summary>
    /// <param name="supervisor">Agent supervisor used to get or create child agent handles.</param>
    /// <param name="logger">Logger instance.</param>
    public DefaultAgentCommunicator(
        IAgentSupervisor supervisor,
        ILogger<DefaultAgentCommunicator> logger)
    {
        _supervisor = supervisor;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AgentResponse> CallSubAgentAsync(
        string parentAgentId,
        string parentSessionId,
        string childAgentId,
        string message,
        CancellationToken cancellationToken = default)
    {
        var childSessionId = $"{parentSessionId}::sub::{childAgentId}";
        _logger.LogInformation(
            "Sub-agent call from '{ParentAgentId}' session '{ParentSessionId}' to '{ChildAgentId}' session '{ChildSessionId}'",
            parentAgentId,
            parentSessionId,
            childAgentId,
            childSessionId);

        var childHandle = await _supervisor.GetOrCreateAsync(childAgentId, childSessionId, cancellationToken);
        return await childHandle.PromptAsync(message, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AgentResponse> CallCrossAgentAsync(
        string sourceAgentId,
        string targetEndpoint,
        string targetAgentId,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(targetEndpoint))
        {
            throw new NotSupportedException(
                $"Remote cross-agent calls to '{targetEndpoint}' are not yet supported. " +
                "Use local cross-agent calls by leaving targetEndpoint empty.");
        }

        var crossSessionId = $"cross::{sourceAgentId}::{targetAgentId}::{Guid.NewGuid():N}";
        _logger.LogInformation(
            "Cross-agent call from '{SourceAgentId}' to '{TargetAgentId}' session '{CrossSessionId}'",
            sourceAgentId,
            targetAgentId,
            crossSessionId);

        var handle = await _supervisor.GetOrCreateAsync(targetAgentId, crossSessionId, cancellationToken);
        return await handle.PromptAsync(message, cancellationToken);
    }
}
