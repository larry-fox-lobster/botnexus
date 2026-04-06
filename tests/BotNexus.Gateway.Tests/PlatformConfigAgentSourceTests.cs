using System.Reflection;
using BotNexus.Gateway.Abstractions.Agents;
using BotNexus.Gateway.Abstractions.Isolation;
using BotNexus.Gateway.Abstractions.Models;
using BotNexus.Gateway.Configuration;
using BotNexus.Gateway.Isolation;
using BotNexus.Providers.Core;
using BotNexus.Providers.Core.Models;
using BotNexus.Providers.Core.Registry;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BotNexus.Gateway.Tests;

public sealed class PlatformConfigAgentSourceTests : IDisposable
{
    private readonly string _configDirectory;

    public PlatformConfigAgentSourceTests()
    {
        _configDirectory = Path.Combine(Path.GetTempPath(), "botnexus-platform-agent-source-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_configDirectory);
    }

    [Fact]
    public async Task LoadAsync_WithEnabledAgents_MapsDescriptorAndLoadsSystemPromptFile()
    {
        var promptPath = Path.Combine(_configDirectory, "prompts", "assistant.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(promptPath)!);
        await File.WriteAllTextAsync(promptPath, "You are helpful.");

        var config = new PlatformConfig
        {
            Agents = new Dictionary<string, AgentDefinitionConfig>
            {
                ["assistant"] = new()
                {
                    Provider = "copilot",
                    Model = "gpt-4.1",
                    SystemPromptFile = @"prompts\assistant.txt",
                    IsolationStrategy = "remote",
                    Enabled = true
                },
                ["disabled-agent"] = new()
                {
                    Provider = "copilot",
                    Model = "gpt-4.1",
                    Enabled = false
                }
            }
        };

        var source = new PlatformConfigAgentSource(
            Options.Create(config),
            _configDirectory,
            new ListLogger<PlatformConfigAgentSource>());

        var descriptor = (await source.LoadAsync()).Should().ContainSingle().Subject;

        descriptor.AgentId.Should().Be("assistant");
        descriptor.DisplayName.Should().Be("assistant");
        descriptor.ApiProvider.Should().Be("copilot");
        descriptor.ModelId.Should().Be("gpt-4.1");
        descriptor.IsolationStrategy.Should().Be("remote");
        descriptor.SystemPrompt.Should().Be("You are helpful.");
    }

    [Fact]
    public async Task LoadAsync_WithMissingSystemPromptFile_SkipsDescriptor()
    {
        var logger = new ListLogger<PlatformConfigAgentSource>();
        var config = new PlatformConfig
        {
            Agents = new Dictionary<string, AgentDefinitionConfig>
            {
                ["assistant"] = new()
                {
                    Provider = "copilot",
                    Model = "gpt-4.1",
                    SystemPromptFile = @"prompts\missing.txt"
                }
            }
        };

        var source = new PlatformConfigAgentSource(Options.Create(config), _configDirectory, logger);

        var descriptors = await source.LoadAsync();

        descriptors.Should().BeEmpty();
        logger.Entries.Should().Contain(e =>
            e.Level == LogLevel.Warning &&
            e.Message.Contains("was not found", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlatformConfigAgentSource_LoadsAgents_ThatInProcessIsolationCanCreate()
    {
        var source = new PlatformConfigAgentSource(
            Options.Create(new PlatformConfig
            {
                Agents = new Dictionary<string, AgentDefinitionConfig>
                {
                    ["assistant"] = new()
                    {
                        Provider = "test-provider",
                        Model = "test-model",
                        IsolationStrategy = "in-process",
                        Enabled = true
                    }
                }
            }),
            _configDirectory,
            new ListLogger<PlatformConfigAgentSource>());

        var descriptor = (await source.LoadAsync()).Should().ContainSingle().Subject;

        var modelRegistry = new ModelRegistry();
        modelRegistry.Register("test-provider", new LlmModel(
            Id: "test-model",
            Name: "test-model",
            Api: "responses",
            Provider: "test-provider",
            BaseUrl: "https://llm.test",
            Reasoning: false,
            Input: ["text"],
            Cost: new ModelCost(0, 0, 0, 0),
            ContextWindow: 8192,
            MaxTokens: 1024));

        var strategy = new InProcessIsolationStrategy(
            new LlmClient(new ApiProviderRegistry(), modelRegistry),
            CreateGatewayAuthManagerWithTempAuthPath(),
            NullLogger<InProcessIsolationStrategy>.Instance);

        var handle = await strategy.CreateAsync(
            descriptor,
            new AgentExecutionContext { SessionId = "session-1" });

        handle.AgentId.Should().Be("assistant");
        handle.SessionId.Should().Be("session-1");
    }

    [Fact]
    public void Watch_ReturnsNull()
    {
        var source = new PlatformConfigAgentSource(
            Options.Create(new PlatformConfig()),
            _configDirectory,
            new ListLogger<PlatformConfigAgentSource>());

        source.Watch(_ => { }).Should().BeNull();
    }

    public void Dispose()
    {
        if (Directory.Exists(_configDirectory))
            Directory.Delete(_configDirectory, recursive: true);
    }

    private GatewayAuthManager CreateGatewayAuthManagerWithTempAuthPath()
    {
        var authManager = new GatewayAuthManager(new PlatformConfig(), NullLogger<GatewayAuthManager>.Instance);
        var authPathField = typeof(GatewayAuthManager).GetField("_authFilePath", BindingFlags.NonPublic | BindingFlags.Instance);
        authPathField.Should().NotBeNull();
        authPathField!.SetValue(authManager, Path.Combine(_configDirectory, "auth.json"));
        return authManager;
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel)
            => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
