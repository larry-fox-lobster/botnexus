using BotNexus.AgentCore.Tools;

namespace BotNexus.CodingAgent.Extensions;

/// <summary>
/// Extension contract for adding capabilities to the coding agent.
/// Extensions are loaded from assemblies in the extensions directory.
/// </summary>
public interface IExtension
{
    /// <summary>
    /// Extension name for display and logging.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Returns the tools this extension provides.
    /// </summary>
    IReadOnlyList<IAgentTool> GetTools();
}
