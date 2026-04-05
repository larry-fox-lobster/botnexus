using System.Text.Json;
using BotNexus.Gateway.Abstractions.Agents;
using BotNexus.Gateway.Abstractions.Sessions;
using BotNexus.Gateway.Configuration;
using BotNexus.Gateway.Extensions;
using BotNexus.Gateway.Sessions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BotNexus.Gateway.Tests;

public sealed class PlatformConfigurationTests
{
    [Fact]
    public async Task PlatformConfigLoader_LoadAsync_WhenFileMissing_ReturnsDefaultConfig()
    {
        var config = await PlatformConfigLoader.LoadAsync(Path.Combine(Guid.NewGuid().ToString("N"), "missing.json"));

        config.Should().NotBeNull();
        config.DefaultAgentId.Should().BeNull();
    }

    [Fact]
    public void PlatformConfigLoader_Validate_WithInvalidValues_ReturnsErrors()
    {
        var errors = PlatformConfigLoader.Validate(new PlatformConfig
        {
            ListenUrl = "not-a-url",
            LogLevel = "verbose",
            AgentsDirectory = "bad\0path"
        });

        errors.Should().Contain(e => e.Contains("ListenUrl", StringComparison.Ordinal));
        errors.Should().Contain(e => e.Contains("LogLevel", StringComparison.Ordinal));
        errors.Should().Contain(e => e.Contains("AgentsDirectory", StringComparison.Ordinal));
    }

    [Fact]
    public void AddPlatformConfiguration_AppliesGatewayDefaultsAndStoragePaths()
    {
        using var fixture = new PlatformConfigFixture();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBotNexusGateway();
        services.AddPlatformConfiguration(fixture.ConfigPath);

        using var provider = services.BuildServiceProvider();
        var gatewayOptions = provider.GetRequiredService<IOptions<GatewayOptions>>().Value;
        var sessionStore = provider.GetRequiredService<ISessionStore>();
        var agentSources = provider.GetServices<IAgentConfigurationSource>();

        gatewayOptions.DefaultAgentId.Should().Be("config-agent");
        sessionStore.Should().BeOfType<FileSessionStore>();
        agentSources.Should().ContainSingle();
    }

    private sealed class PlatformConfigFixture : IDisposable
    {
        public PlatformConfigFixture()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "botnexus-platform-config-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);

            ConfigPath = Path.Combine(RootPath, "config.json");
            var config = new PlatformConfig
            {
                DefaultAgentId = "config-agent",
                AgentsDirectory = "agents",
                SessionsDirectory = "sessions",
                LogLevel = "Information"
            };

            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config));
        }

        public string RootPath { get; }

        public string ConfigPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
                Directory.Delete(RootPath, recursive: true);
        }
    }
}
