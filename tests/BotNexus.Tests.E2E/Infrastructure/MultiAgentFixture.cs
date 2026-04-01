using BotNexus.Agent;
using BotNexus.Agent.Tools;
using BotNexus.Core.Abstractions;
using BotNexus.Core.Configuration;
using BotNexus.Core.Models;
using BotNexus.Providers.Base;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BotNexus.Tests.E2E.Infrastructure;

/// <summary>
/// Bootstraps the full BotNexus Gateway with 5 mock agents, a shared
/// MockLlmProvider, and two mock channels for multi-agent E2E testing.
/// </summary>
public sealed class MultiAgentFixture : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private readonly string _workspacePath = Path.Combine(
        AppContext.BaseDirectory, "e2e-workspace", Guid.NewGuid().ToString("N"));

    public MockLlmProvider MockProvider { get; } = new();
    public MockWebChannel WebChannel { get; } = new();
    public MockApiChannel ApiChannel { get; } = new();

    public IServiceProvider Services => _factory!.Services;

    public IMessageBus MessageBus => Services.GetRequiredService<IMessageBus>();

    /// <summary>Sends a message from a user to a specific agent via a specific channel.</summary>
    public async Task SendMessageAsync(
        string agentName,
        string content,
        MockChannelBase channel,
        string chatId = "test-chat",
        string senderId = "test-user")
    {
        var message = new InboundMessage(
            Channel: channel.Name,
            SenderId: senderId,
            ChatId: chatId,
            Content: content,
            Timestamp: DateTimeOffset.UtcNow,
            Media: [],
            Metadata: new Dictionary<string, object> { ["agent"] = agentName });

        await MessageBus.PublishAsync(message);
    }

    /// <summary>Sends a message without specifying an agent (uses default routing).</summary>
    public async Task SendMessageToDefaultAsync(
        string content,
        MockChannelBase channel,
        string chatId = "test-chat",
        string senderId = "test-user")
    {
        var message = new InboundMessage(
            Channel: channel.Name,
            SenderId: senderId,
            ChatId: chatId,
            Content: content,
            Timestamp: DateTimeOffset.UtcNow,
            Media: [],
            Metadata: new Dictionary<string, object>());

        await MessageBus.PublishAsync(message);
    }

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_workspacePath);
        var sessionsPath = Path.Combine(_workspacePath, "sessions");
        Directory.CreateDirectory(sessionsPath);

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddJsonFile(
                        Path.Combine(AppContext.BaseDirectory, "appsettings.Testing.json"),
                        optional: false);

                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["BotNexus:Agents:Workspace"] = _workspacePath,
                        ["BotNexus:ExtensionsPath"] = Path.Combine(_workspacePath, "extensions"),
                        ["BotNexus:Gateway:ApiKey"] = string.Empty,
                    });
                });

                builder.ConfigureServices(services =>
                {
                    // Remove hosted services that interfere with tests
                    services.RemoveAll<IHostedService>(s =>
                        s.ImplementationType?.Name is "HeartbeatService" or "CronService");

                    // Register shared mock provider
                    services.AddSingleton<ILlmProvider>(MockProvider);
                    services.AddSingleton(sp =>
                    {
                        var registry = new ProviderRegistry();
                        registry.Register("mock", MockProvider);
                        return registry;
                    });

                    // Register mock channels
                    services.AddSingleton<IChannel>(WebChannel);
                    services.AddSingleton<IChannel>(ApiChannel);

                    // Register agent runners for all 5 agents
                    RegisterAgentRunners(services);
                });
            });

        // Force host to start so Gateway BackgroundService begins reading from MessageBus
        _ = _factory.Server;
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _factory?.Dispose();
        try
        {
            if (Directory.Exists(_workspacePath))
                Directory.Delete(_workspacePath, recursive: true);
        }
        catch { /* best-effort cleanup */ }
        return Task.CompletedTask;
    }

    private void RegisterAgentRunners(IServiceCollection services)
    {
        var agentNames = new[] { "nova", "quill", "bolt", "echo", "sage" };

        foreach (var agentName in agentNames)
        {
            services.AddSingleton<IAgentRunner>(sp =>
            {
                var cfg = sp.GetRequiredService<IOptions<BotNexusConfig>>().Value;
                var agentCfg = cfg.Agents.Named.GetValueOrDefault(agentName);

                var generation = new GenerationSettings
                {
                    Model = cfg.Agents.Model,
                    MaxTokens = cfg.Agents.MaxTokens,
                    Temperature = cfg.Agents.Temperature,
                    ContextWindowTokens = cfg.Agents.ContextWindowTokens,
                    MaxToolIterations = cfg.Agents.MaxToolIterations
                };

                var registry = sp.GetRequiredService<ProviderRegistry>();

                var loop = new AgentLoop(
                    agentName: agentName,
                    systemPrompt: agentCfg?.SystemPrompt,
                    providerRegistry: registry,
                    sessionManager: sp.GetRequiredService<ISessionManager>(),
                    contextBuilder: new ContextBuilder(NullLogger<ContextBuilder>.Instance),
                    toolRegistry: new ToolRegistry(),
                    settings: generation,
                    providerName: "mock",
                    enableMemory: agentCfg?.EnableMemory == true,
                    memoryStore: sp.GetRequiredService<IMemoryStore>(),
                    logger: NullLogger<AgentLoop>.Instance,
                    maxToolIterations: cfg.Agents.MaxToolIterations);

                // Find the channel that matches — agent sends responses back to
                // whichever channel the message came from. We use the ChannelManager
                // to find the right one at dispatch time. For simplicity in tests,
                // the AgentRunner responds on the first available mock channel.
                // The Gateway's dispatch pattern routes responses correctly.
                var channels = sp.GetServices<IChannel>().ToList();

                return new AgentRunner(
                    agentName: agentName,
                    agentLoop: loop,
                    logger: NullLogger<AgentRunner>.Instance,
                    responseChannel: new ChannelRouter(channels));
            });
        }
    }
}

/// <summary>
/// Routes outbound messages to the correct channel based on the outbound message's
/// Channel field, matching the channel name from the inbound message.
/// </summary>
internal sealed class ChannelRouter : IChannel
{
    private readonly IReadOnlyList<IChannel> _channels;

    public ChannelRouter(IReadOnlyList<IChannel> channels) => _channels = channels;

    public string Name => "router";
    public string DisplayName => "Channel Router";
    public bool IsRunning => true;
    public bool SupportsStreaming => false;

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SendAsync(OutboundMessage message, CancellationToken cancellationToken = default)
    {
        var target = _channels.FirstOrDefault(c =>
            string.Equals(c.Name, message.Channel, StringComparison.OrdinalIgnoreCase));

        return target?.SendAsync(message, cancellationToken) ?? Task.CompletedTask;
    }

    public Task SendDeltaAsync(string chatId, string delta, IReadOnlyDictionary<string, object>? metadata = null, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public bool IsAllowed(string senderId) => true;
}

/// <summary>Extension to remove hosted services by predicate.</summary>
internal static class ServiceCollectionExtensions
{
    public static void RemoveAll<T>(this IServiceCollection services, Func<ServiceDescriptor, bool> predicate)
    {
        var descriptors = services.Where(d => d.ServiceType == typeof(T) && predicate(d)).ToList();
        foreach (var d in descriptors)
            services.Remove(d);
    }
}
