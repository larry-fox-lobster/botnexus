using BotNexus.Agent.Core.Tools;
using BotNexus.Gateway.Abstractions.Agents;
using BotNexus.Gateway.Agents;
using BotNexus.Gateway.Configuration;
using BotNexus.Gateway.Tools;
using BotNexus.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
        services.AddSingleton<IAgentToolFactory>(sp =>
        {
            var config = sp.GetService<IOptions<PlatformConfig>>()?.Value;
            var preference = ParseShellPreference(config?.Gateway?.ShellPreference);
            return new DefaultAgentToolFactory(preference);
        });

        // Tool registry collects extension IAgentTool registrations.
        services.AddSingleton<IToolRegistry>(sp => new DefaultToolRegistry(sp.GetServices<IAgentTool>()));

        return services;
    }

    private static ShellPreference ParseShellPreference(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ShellPreference.Auto;

        return value.Trim().ToLowerInvariant() switch
        {
            "pwsh" or "powershell" => ShellPreference.Pwsh,
            "bash" => ShellPreference.Bash,
            _ => ShellPreference.Auto,
        };
    }
}
