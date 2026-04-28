using BotNexus.Domain.AgentExchange;
using BotNexus.Gateway.Abstractions.Models;

namespace BotNexus.Gateway.Abstractions.Agents;

/// <summary>
/// Executes synchronous conversations between registered peer agents.
/// </summary>
public interface IAgentExchangeService
{
    /// <summary>
    /// Starts a peer agent conversation and returns the completed transcript/result.
    /// </summary>
    Task<AgentExchangeResult> ConverseAsync(AgentExchangeRequest request, CancellationToken cancellationToken = default);
}
