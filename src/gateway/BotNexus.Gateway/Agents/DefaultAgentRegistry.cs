using BotNexus.Gateway.Abstractions.Agents;
using BotNexus.Gateway.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace BotNexus.Gateway.Agents;

/// <summary>
/// Default in-memory agent registry. Thread-safe via lock.
/// </summary>
public sealed class DefaultAgentRegistry : IAgentRegistry
{
    private readonly Dictionary<string, AgentDescriptor> _agents = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _sync = new();
    private readonly ILogger<DefaultAgentRegistry> _logger;

    public DefaultAgentRegistry(ILogger<DefaultAgentRegistry> logger) => _logger = logger;

    /// <inheritdoc />
    public void Register(AgentDescriptor descriptor)
    {
        lock (_sync)
        {
            if (!_agents.TryAdd(descriptor.AgentId, descriptor))
                throw new InvalidOperationException($"Agent '{descriptor.AgentId}' is already registered.");

            _logger.LogInformation("Registered agent '{AgentId}' ({DisplayName})", descriptor.AgentId, descriptor.DisplayName);
        }
    }

    /// <inheritdoc />
    public void Unregister(string agentId)
    {
        lock (_sync)
        {
            if (_agents.Remove(agentId))
                _logger.LogInformation("Unregistered agent '{AgentId}'", agentId);
        }
    }

    /// <inheritdoc />
    public bool Update(string agentId, AgentDescriptor descriptor)
    {
        if (!string.Equals(agentId, descriptor.AgentId, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The descriptor AgentId must match the route agentId.", nameof(descriptor));

        lock (_sync)
        {
            if (!_agents.ContainsKey(agentId))
                return false;

            _agents[agentId] = descriptor;
            _logger.LogInformation("Updated agent '{AgentId}' ({DisplayName})", descriptor.AgentId, descriptor.DisplayName);
            return true;
        }
    }

    /// <inheritdoc />
    public AgentDescriptor? Get(string agentId)
    {
        lock (_sync) return _agents.GetValueOrDefault(agentId);
    }

    /// <inheritdoc />
    public IReadOnlyList<AgentDescriptor> GetAll()
    {
        lock (_sync) return [.. _agents.Values];
    }

    /// <inheritdoc />
    public bool Contains(string agentId)
    {
        lock (_sync) return _agents.ContainsKey(agentId);
    }
}
