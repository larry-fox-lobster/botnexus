using System.Reflection;
using BotNexus.Gateway.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace BotNexus.Gateway.Tests;

public sealed class GatewayAuthManagerTests : IDisposable
{
    private readonly string _rootPath;
    private readonly string _authFilePath;
    private readonly string _legacyAuthFilePath;
    private readonly Dictionary<string, string?> _originalEnvironmentVariables = new(StringComparer.Ordinal);

    public GatewayAuthManagerTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "botnexus-gateway-auth-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _authFilePath = Path.Combine(_rootPath, "auth.json");
        _legacyAuthFilePath = Path.Combine(_rootPath, "legacy-auth.json");
    }

    [Fact]
    public async Task GetApiKeyAsync_WhenAuthJsonHasValidEntry_ReturnsAccessToken()
    {
        await File.WriteAllTextAsync(_authFilePath, """
                                             {
                                               "openai": {
                                                 "type": "token",
                                                 "refresh": "unused",
                                                 "access": "auth-access-key",
                                                 "expires": 4102444800000,
                                                 "endpoint": "https://api.openai.test"
                                               }
                                             }
                                             """);

        var manager = CreateManager(new PlatformConfig());

        var apiKey = await manager.GetApiKeyAsync("openai");

        apiKey.Should().Be("auth-access-key");
    }

    [Fact]
    public async Task GetApiKeyAsync_WhenCopilotUsesGithubCopilotEntry_ReturnsAccessToken()
    {
        await File.WriteAllTextAsync(_authFilePath, """
                                             {
                                               "github-copilot": {
                                                 "type": "oauth",
                                                 "refresh": "unused",
                                                 "access": "copilot-access-key",
                                                 "expires": 4102444800000,
                                                 "endpoint": "https://api.enterprise.githubcopilot.com"
                                               }
                                             }
                                             """);

        var manager = CreateManager(new PlatformConfig());

        var apiKey = await manager.GetApiKeyAsync("copilot");

        apiKey.Should().Be("copilot-access-key");
    }

    [Fact]
    public async Task GetApiKeyAsync_WhenHomeAuthMissing_UsesLegacyRepoAuthFile()
    {
        await File.WriteAllTextAsync(_legacyAuthFilePath, """
                                                   {
                                                     "openai": {
                                                       "type": "token",
                                                       "refresh": "unused",
                                                       "access": "legacy-auth-access-key",
                                                       "expires": 4102444800000,
                                                       "endpoint": "https://api.openai.test"
                                                     }
                                                   }
                                                   """);

        var manager = CreateManager(new PlatformConfig(), usePrimaryAuthPath: false);

        var apiKey = await manager.GetApiKeyAsync("openai");

        apiKey.Should().Be("legacy-auth-access-key");
    }

    [Fact]
    public async Task GetApiKeyAsync_WhenAuthJsonMissing_FallsBackToEnvironmentVariable()
    {
        SetEnvironmentVariable("OPENAI_API_KEY", "env-openai-key");
        var manager = CreateManager(new PlatformConfig());

        var apiKey = await manager.GetApiKeyAsync("openai");

        apiKey.Should().Be("env-openai-key");
    }

    [Fact]
    public async Task GetApiKeyAsync_WhenNoAuthOrEnv_FallsBackToPlatformConfigApiKey()
    {
        SetEnvironmentVariable("OPENAI_API_KEY", null);
        var manager = CreateManager(new PlatformConfig
        {
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["openai"] = new()
                {
                    ApiKey = "config-openai-key"
                }
            }
        });

        var apiKey = await manager.GetApiKeyAsync("openai");

        apiKey.Should().Be("config-openai-key");
    }

    [Fact]
    public async Task GetApiKeyAsync_WhenPlatformConfigUsesAuthPrefix_ResolvesFromAuthJson()
    {
        SetEnvironmentVariable("OPENAI_API_KEY", null);
        await File.WriteAllTextAsync(_authFilePath, """
                                             {
                                               "github-copilot": {
                                                 "type": "token",
                                                 "refresh": "unused",
                                                 "access": "copilot-auth-access-key",
                                                 "expires": 4102444800000,
                                                 "endpoint": "https://copilot.test"
                                               }
                                             }
                                             """);

        var manager = CreateManager(new PlatformConfig
        {
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["openai"] = new()
                {
                    ApiKey = "auth:github-copilot"
                }
            }
        });

        var apiKey = await manager.GetApiKeyAsync("openai");

        apiKey.Should().Be("copilot-auth-access-key");
    }

    [Fact]
    public async Task GetApiKeyAsync_WhenProviderIsNull_ReturnsNull()
    {
        var manager = CreateManager(new PlatformConfig());

        var apiKey = await manager.GetApiKeyAsync(null!);

        apiKey.Should().BeNull();
    }

    [Fact]
    public void GetApiEndpoint_WhenAuthJsonHasEndpoint_ReturnsEndpoint()
    {
        File.WriteAllText(_authFilePath, """
                                        {
                                          "openai": {
                                            "type": "token",
                                            "refresh": "unused",
                                            "access": "auth-access-key",
                                            "expires": 4102444800000,
                                            "endpoint": "https://auth-endpoint.test"
                                          }
                                        }
                                        """);
        var manager = CreateManager(new PlatformConfig());

        var endpoint = manager.GetApiEndpoint("openai");

        endpoint.Should().Be("https://auth-endpoint.test");
    }

    [Fact]
    public void GetApiEndpoint_WhenAuthJsonMissing_FallsBackToPlatformConfigBaseUrl()
    {
        var manager = CreateManager(new PlatformConfig
        {
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["openai"] = new()
                {
                    BaseUrl = "https://platform-base-url.test"
                }
            }
        });

        var endpoint = manager.GetApiEndpoint("openai");

        endpoint.Should().Be("https://platform-base-url.test");
    }

    [Fact]
    public void GetApiEndpoint_WhenNoConfig_ReturnsNull()
    {
        var manager = CreateManager(new PlatformConfig());

        var endpoint = manager.GetApiEndpoint("openai");

        endpoint.Should().BeNull();
    }

    public void Dispose()
    {
        foreach (var (name, value) in _originalEnvironmentVariables)
            Environment.SetEnvironmentVariable(name, value);

        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }

    private GatewayAuthManager CreateManager(PlatformConfig platformConfig, bool usePrimaryAuthPath = true)
    {
        var manager = new GatewayAuthManager(platformConfig, NullLogger<GatewayAuthManager>.Instance);
        var authPathField = typeof(GatewayAuthManager).GetField("_authFilePath", BindingFlags.NonPublic | BindingFlags.Instance);
        var legacyAuthPathField = typeof(GatewayAuthManager).GetField("_legacyAuthFilePath", BindingFlags.NonPublic | BindingFlags.Instance);
        authPathField.Should().NotBeNull();
        legacyAuthPathField.Should().NotBeNull();
        authPathField!.SetValue(manager, usePrimaryAuthPath ? _authFilePath : Path.Combine(_rootPath, "missing-auth.json"));
        legacyAuthPathField!.SetValue(manager, _legacyAuthFilePath);
        return manager;
    }

    private void SetEnvironmentVariable(string name, string? value)
    {
        if (!_originalEnvironmentVariables.ContainsKey(name))
            _originalEnvironmentVariables[name] = Environment.GetEnvironmentVariable(name);

        Environment.SetEnvironmentVariable(name, value);
    }
}
