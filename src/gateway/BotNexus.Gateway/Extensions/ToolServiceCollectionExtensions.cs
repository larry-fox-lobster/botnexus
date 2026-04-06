using BotNexus.AgentCore.Tools;
using BotNexus.Gateway.Agents;
using BotNexus.Gateway.Tools;
using BotNexus.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace BotNexus.Gateway.Extensions;

/// <summary>
/// DI registration extensions for the built-in agent tools.
/// </summary>
public static class ToolServiceCollectionExtensions
{
    /// <summary>
    /// Registers the built-in tools and tool registry.
    /// </summary>
    public static IServiceCollection AddBotNexusTools(this IServiceCollection services)
    {
        // Register built-in tools - use current directory as working directory
        var workingDirectory = Environment.CurrentDirectory;
        services.AddSingleton<IAgentTool>(new ReadTool(workingDirectory));
        services.AddSingleton<IAgentTool>(new WriteTool(workingDirectory));
        services.AddSingleton<IAgentTool>(new EditTool(workingDirectory));
        services.AddSingleton<IAgentTool>(new ShellTool());
        services.AddSingleton<IAgentTool>(new ListDirectoryTool(workingDirectory));
        services.AddSingleton<IAgentTool>(new GrepTool(workingDirectory));
        services.AddSingleton<IAgentTool>(new GlobTool(workingDirectory));

        // Tool registry collects all IAgentTool registrations
        services.AddSingleton<IToolRegistry>(sp => new DefaultToolRegistry(sp.GetServices<IAgentTool>()));

        return services;
    }
}
