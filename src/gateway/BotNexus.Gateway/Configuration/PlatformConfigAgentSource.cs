using BotNexus.Gateway.Abstractions.Agents;
using BotNexus.Gateway.Abstractions.Models;
using BotNexus.Gateway.Agents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BotNexus.Gateway.Configuration;

/// <summary>
/// Loads agent descriptors from <see cref="PlatformConfig"/> agent definitions.
/// </summary>
public sealed class PlatformConfigAgentSource(
    IOptions<PlatformConfig> configOptions,
    string configDirectory,
    ILogger<PlatformConfigAgentSource> logger) : IAgentConfigurationSource
{
    private readonly IOptions<PlatformConfig> _configOptions = configOptions;
    private readonly string _configDirectory = Path.GetFullPath(configDirectory);
    private readonly ILogger<PlatformConfigAgentSource> _logger = logger;

    /// <inheritdoc />
    public async Task<IReadOnlyList<AgentDescriptor>> LoadAsync(CancellationToken cancellationToken = default)
    {
        List<AgentDescriptor> descriptors = [];
        var agents = _configOptions.Value.Agents;
        if (agents is null || agents.Count == 0)
            return descriptors;

        foreach (var (agentId, agentConfig) in agents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!agentConfig.Enabled)
                continue;

            var descriptor = new AgentDescriptor
            {
                AgentId = agentId,
                DisplayName = agentId,
                ModelId = agentConfig.Model ?? string.Empty,
                ApiProvider = agentConfig.Provider ?? string.Empty,
                IsolationStrategy = string.IsNullOrWhiteSpace(agentConfig.IsolationStrategy)
                    ? "in-process"
                    : agentConfig.IsolationStrategy
            };

            var validationErrors = AgentDescriptorValidator.Validate(descriptor);
            if (validationErrors.Count > 0)
            {
                _logger.LogWarning(
                    "Skipping platform-config agent '{AgentId}' due to validation errors: {Errors}",
                    agentId,
                    string.Join("; ", validationErrors));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(agentConfig.SystemPromptFile))
            {
                var systemPrompt = await TryLoadSystemPromptFromFileAsync(agentId, agentConfig.SystemPromptFile, cancellationToken);
                if (systemPrompt is null)
                    continue;

                descriptor = descriptor with
                {
                    SystemPrompt = systemPrompt
                };
            }

            descriptors.Add(descriptor);
        }

        return descriptors;
    }

    /// <inheritdoc />
    public IDisposable? Watch(Action<IReadOnlyList<AgentDescriptor>> onChanged)
        => null;

    private async Task<string?> TryLoadSystemPromptFromFileAsync(
        string agentId,
        string systemPromptFile,
        CancellationToken cancellationToken)
    {
        var resolvedPath = Path.GetFullPath(Path.Combine(_configDirectory, systemPromptFile));
        var configDirectoryPrefix = _configDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!resolvedPath.StartsWith(configDirectoryPrefix, StringComparison.OrdinalIgnoreCase) &&
            !resolvedPath.Equals(_configDirectory, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "System prompt file '{SystemPromptFile}' for platform-config agent '{AgentId}' resolves outside the config directory. Path traversal blocked.",
                systemPromptFile,
                agentId);
            return null;
        }

        if (!File.Exists(resolvedPath))
        {
            _logger.LogWarning(
                "System prompt file '{SystemPromptFile}' was not found for platform-config agent '{AgentId}'.",
                resolvedPath,
                agentId);
            return null;
        }

        return await File.ReadAllTextAsync(resolvedPath, cancellationToken);
    }
}
