using BotNexus.Gateway.Abstractions.Agents;
using BotNexus.Gateway.Abstractions.Models;
using BotNexus.Gateway.Agents;
using FluentAssertions;

namespace BotNexus.Gateway.Tests;

public sealed class WorkspaceContextBuilderTests
{
    [Fact]
    public async Task BuildSystemPromptAsync_WhenSoulExists_ComposesAllSections()
    {
        var manager = new StubWorkspaceManager(new AgentWorkspace(
            "farnsworth",
            Soul: "SOUL",
            Identity: "IDENTITY",
            User: "USER",
            Memory: "MEMORY"));
        var builder = new WorkspaceContextBuilder(manager);

        var result = await builder.BuildSystemPromptAsync(new AgentDescriptor
        {
            AgentId = "farnsworth",
            DisplayName = "Farnsworth",
            ModelId = "test-model",
            ApiProvider = "test-provider",
            SystemPrompt = "CONFIG"
        });

        result.Should().Be("SOUL\n\n---\n\nIDENTITY\n\n---\n\nCONFIG\n\n---\n\nUSER");
    }

    [Fact]
    public async Task BuildSystemPromptAsync_WhenSoulMissing_UsesDescriptorSystemPrompt()
    {
        var manager = new StubWorkspaceManager(new AgentWorkspace(
            "farnsworth",
            Soul: "",
            Identity: "IDENTITY",
            User: "USER",
            Memory: "MEMORY"));
        var builder = new WorkspaceContextBuilder(manager);

        var result = await builder.BuildSystemPromptAsync(new AgentDescriptor
        {
            AgentId = "farnsworth",
            DisplayName = "Farnsworth",
            ModelId = "test-model",
            ApiProvider = "test-provider",
            SystemPrompt = "CONFIG"
        });

        result.Should().Be("CONFIG");
    }

    private sealed class StubWorkspaceManager : IAgentWorkspaceManager
    {
        private readonly AgentWorkspace _workspace;

        public StubWorkspaceManager(AgentWorkspace workspace)
        {
            _workspace = workspace;
        }

        public Task<AgentWorkspace> LoadWorkspaceAsync(string agentName, CancellationToken ct = default)
            => Task.FromResult(_workspace);

        public Task SaveMemoryAsync(string agentName, string content, CancellationToken ct = default)
            => Task.CompletedTask;

        public string GetWorkspacePath(string agentName)
            => string.Empty;
    }
}
