using System.Text.Json;
using System.Text.Json.Serialization;
using BotNexus.Gateway.Abstractions.Agents;
using BotNexus.Gateway.Abstractions.Models;

namespace BotNexus.Gateway.Configuration;

public sealed class FileAgentConfigurationWriter(string directoryPath, BotNexusHome botNexusHome) : IAgentConfigurationWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _directoryPath = Path.GetFullPath(directoryPath);
    private readonly BotNexusHome _botNexusHome = botNexusHome;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public async Task SaveAsync(AgentDescriptor descriptor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.AgentId);

        Directory.CreateDirectory(_directoryPath);
        _ = _botNexusHome.GetAgentDirectory(descriptor.AgentId);

        var configPath = GetConfigPath(descriptor.AgentId);
        var tempPath = configPath + "." + Guid.NewGuid().ToString("N") + ".tmp";

        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, AgentConfigurationFile.FromDescriptor(descriptor), JsonOptions, cancellationToken);
            }

            File.Move(tempPath, configPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            _writeGate.Release();
        }
    }

    public async Task DeleteAsync(string agentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        var configPath = GetConfigPath(agentId);

        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(configPath))
                File.Delete(configPath);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private string GetConfigPath(string agentId)
        => Path.Combine(_directoryPath, $"{agentId.Trim()}.json");

    private sealed record AgentConfigurationFile
    {
        public string AgentId { get; init; } = string.Empty;

        public string DisplayName { get; init; } = string.Empty;

        public string? Description { get; init; }

        public string ModelId { get; init; } = string.Empty;

        public string ApiProvider { get; init; } = string.Empty;

        public string? SystemPrompt { get; init; }

        public string? SystemPromptFile { get; init; }

        public IReadOnlyList<string> ToolIds { get; init; } = [];

        public string IsolationStrategy { get; init; } = "in-process";

        public int MaxConcurrentSessions { get; init; }

        public IReadOnlyDictionary<string, object?> Metadata { get; init; } = new Dictionary<string, object?>();

        public IReadOnlyDictionary<string, object?> IsolationOptions { get; init; } = new Dictionary<string, object?>();

        public IReadOnlyList<string> SubAgentIds { get; init; } = [];

        public static AgentConfigurationFile FromDescriptor(AgentDescriptor descriptor)
            => new()
            {
                AgentId = descriptor.AgentId,
                DisplayName = descriptor.DisplayName,
                Description = descriptor.Description,
                ModelId = descriptor.ModelId,
                ApiProvider = descriptor.ApiProvider,
                SystemPrompt = descriptor.SystemPrompt,
                SystemPromptFile = descriptor.SystemPromptFile,
                ToolIds = descriptor.ToolIds,
                IsolationStrategy = descriptor.IsolationStrategy,
                MaxConcurrentSessions = descriptor.MaxConcurrentSessions,
                Metadata = descriptor.Metadata,
                IsolationOptions = descriptor.IsolationOptions,
                SubAgentIds = descriptor.SubAgentIds
            };
    }
}
