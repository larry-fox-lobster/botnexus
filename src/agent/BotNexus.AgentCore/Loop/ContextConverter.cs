using BotNexus.AgentCore.Configuration;
using BotNexus.AgentCore.Tools;
using BotNexus.AgentCore.Types;
using BotNexus.Providers.Core.Models;

namespace BotNexus.AgentCore.Loop;

internal static class ContextConverter
{
    public static async Task<Context> ToProviderContext(
        AgentContext agentContext,
        ConvertToLlmDelegate convertToLlm,
        CancellationToken ct)
    {
        var providerMessages = await convertToLlm(agentContext.Messages, ct).ConfigureAwait(false);
        var tools = agentContext.Tools.Count == 0
            ? null
            : agentContext.Tools.Select(ToProviderTool).ToList();

        return new Context(agentContext.SystemPrompt, providerMessages, tools);
    }

    public static Tool ToProviderTool(IAgentTool agentTool)
    {
        return new Tool(
            Name: agentTool.Definition.Name,
            Description: agentTool.Definition.Description,
            Parameters: agentTool.Definition.Parameters);
    }
}
