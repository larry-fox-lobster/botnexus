using System.Text.Json.Nodes;
using BotNexus.Gateway.Abstractions.Models;
using BotNexus.Gateway.Configuration;
using System.IO.Abstractions.TestingHelpers;

namespace BotNexus.Gateway.Tests;

public sealed class PlatformConfigAgentWriterTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "botnexus-platform-agent-writer-tests", Guid.NewGuid().ToString("N"));
    private readonly string _configPath;
    private readonly BotNexusHome _home;
    private readonly MockFileSystem _fileSystem;

    public PlatformConfigAgentWriterTests()
    {
        _fileSystem = new MockFileSystem();
        _home = new BotNexusHome(_fileSystem, _rootPath);
        _fileSystem.Directory.CreateDirectory(_rootPath);
        _configPath = Path.Combine(_rootPath, "config.json");
    }

    [Fact]
    public async Task SaveAsync_WritesAgentIntoConfigAndCreatesWorkspace()
    {
        var writer = new PlatformConfigAgentWriter(_configPath, _home, _fileSystem);
        var descriptor = CreateDescriptor("nova") with
        {
            AllowedModelIds = ["claude-sonnet-4.5"],
            ToolIds = ["read"],
            SubAgentIds = ["helper"],
            Metadata = new Dictionary<string, object?> { ["owner"] = "gateway" },
            IsolationOptions = new Dictionary<string, object?> { ["timeoutMs"] = 1000 }
        };

        await writer.SaveAsync(descriptor);

        var root = await ReadConfigAsync();
        var agent = root["agents"]!["nova"]!;

        agent["provider"]!.GetValue<string>().ShouldBe("github-copilot");
        agent["model"]!.GetValue<string>().ShouldBe("claude-sonnet-4.5");
        agent["displayName"]!.GetValue<string>().ShouldBe("nova");
        agent["enabled"]!.GetValue<bool>().ShouldBeTrue();
        agent["allowedModels"]!.AsArray().ShouldHaveSingleItem()!.GetValue<string>().ShouldBe("claude-sonnet-4.5");
        agent["toolIds"]!.AsArray().ShouldHaveSingleItem()!.GetValue<string>().ShouldBe("read");
        agent["subAgents"]!.AsArray().ShouldHaveSingleItem()!.GetValue<string>().ShouldBe("helper");
        agent["metadata"]!["owner"]!.GetValue<string>().ShouldBe("gateway");
        agent["isolationOptions"]!["timeoutMs"]!.GetValue<int>().ShouldBe(1000);

        _fileSystem.Directory.Exists(Path.Combine(_home.AgentsPath, "nova")).ShouldBeTrue();
        _fileSystem.File.Exists(Path.Combine(_home.AgentsPath, "nova", "workspace", "SOUL.md")).ShouldBeTrue();
    }

    [Fact]
    public async Task SaveAsync_PreservesUnknownFieldsAndOmitsEmptyOptionalValues()
    {
        await _fileSystem.File.WriteAllTextAsync(_configPath, """
            {
              "version": 1,
              "customRootField": "preserve-me",
              "agents": {
                "nova": {
                  "customAgentField": "keep"
                }
              }
            }
            """);

        var writer = new PlatformConfigAgentWriter(_configPath, _home, _fileSystem);
        await writer.SaveAsync(CreateDescriptor("nova") with
        {
            Description = null,
            SystemPromptFile = null,
            AllowedModelIds = [],
            ToolIds = [],
            SubAgentIds = [],
            MaxConcurrentSessions = 0
        });

        var root = await ReadConfigAsync();
        var agent = root["agents"]!["nova"]!;

        root["customRootField"]!.GetValue<string>().ShouldBe("preserve-me");
        agent["customAgentField"]!.GetValue<string>().ShouldBe("keep");
        agent["description"].ShouldBeNull();
        agent["systemPromptFile"].ShouldBeNull();
        agent["allowedModels"].ShouldBeNull();
        agent["toolIds"].ShouldBeNull();
        agent["subAgents"].ShouldBeNull();
        agent["maxConcurrentSessions"].ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsync_RemovesAgentFromConfig()
    {
        await _fileSystem.File.WriteAllTextAsync(_configPath, """
            {
              "agents": {
                "nova": { "provider": "github-copilot", "model": "gpt-4.1" },
                "other": { "provider": "openai", "model": "gpt-4.1" }
              }
            }
            """);

        var writer = new PlatformConfigAgentWriter(_configPath, _home, _fileSystem);
        await writer.DeleteAsync("nova");

        var root = await ReadConfigAsync();
        root["agents"]!["nova"].ShouldBeNull();
        root["agents"]!["other"].ShouldNotBeNull();
    }

    public void Dispose()
    {
        if (_fileSystem.Directory.Exists(_rootPath))
            _fileSystem.Directory.Delete(_rootPath, recursive: true);
    }

    private async Task<JsonObject> ReadConfigAsync()
    {
        await using var stream = _fileSystem.File.OpenRead(_configPath);
        var node = await JsonNode.ParseAsync(stream);
        return node!.AsObject();
    }

    private static AgentDescriptor CreateDescriptor(string agentId)
        => new()
        {
            AgentId = agentId,
            DisplayName = agentId,
            ModelId = "claude-sonnet-4.5",
            ApiProvider = "github-copilot",
            IsolationStrategy = "in-process",
            MaxConcurrentSessions = 0
        };
}
