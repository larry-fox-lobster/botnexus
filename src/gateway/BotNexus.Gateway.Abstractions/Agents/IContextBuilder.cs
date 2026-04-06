using BotNexus.Gateway.Abstractions.Models;

namespace BotNexus.Gateway.Abstractions.Agents;

public interface IContextBuilder
{
    Task<string> BuildSystemPromptAsync(AgentDescriptor descriptor, CancellationToken ct = default);
}
