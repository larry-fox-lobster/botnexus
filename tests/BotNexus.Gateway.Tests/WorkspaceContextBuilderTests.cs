using BotNexus.Gateway.Abstractions.Agents;
using BotNexus.Gateway.Abstractions.Models;
using BotNexus.Gateway.Agents;
using FluentAssertions;
using System.IO.Abstractions.TestingHelpers;

namespace BotNexus.Gateway.Tests;

public sealed class WorkspaceContextBuilderTests
{
    private readonly MockFileSystem _fileSystem = new();

    [Fact]
    public async Task BuildSystemPromptAsync_WithExplicitPromptFiles_LoadsInOrderAndDeletesBootstrap()
    {
        var workspacePath = CreateWorkspace(
            ("AGENTS.md", "AGENTS"),
            ("SOUL.md", "SOUL"),
            ("TOOLS.md", "TOOLS"),
            ("BOOTSTRAP.md", "BOOTSTRAP"));
        try
        {
            var manager = new StubWorkspaceManager(workspacePath);
            var builder = new WorkspaceContextBuilder(manager, _fileSystem);

            var result = await builder.BuildSystemPromptAsync(new AgentDescriptor
            {
                AgentId = "farnsworth",
                DisplayName = "Farnsworth",
                ModelId = "test-model",
                ApiProvider = "test-provider",
                SystemPromptFiles = ["AGENTS.md", "BOOTSTRAP.md", "TOOLS.md"]
            });

            result.Should().Contain("AGENTS");
            result.Should().Contain("BOOTSTRAP");
            result.Should().Contain("TOOLS");
            result.Should().NotContain("SOUL", "SOUL.md was not in the explicit prompt files list");
            _fileSystem.File.Exists(Path.Combine(workspacePath, "BOOTSTRAP.md")).Should().BeFalse();
        }
        finally
        {
            _fileSystem.Directory.Delete(Path.GetDirectoryName(workspacePath)!, recursive: true);
        }
    }

    [Fact]
    public async Task BuildSystemPromptAsync_WhenPromptFilesEmpty_UsesDefaultOrderAndPrependsInlinePrompt()
    {
        var workspacePath = CreateWorkspace(
            ("AGENTS.md", "AGENTS"),
            ("SOUL.md", "SOUL"),
            ("TOOLS.md", "TOOLS"),
            ("BOOTSTRAP.md", "BOOTSTRAP"),
            ("IDENTITY.md", "IDENTITY"),
            ("USER.md", "USER"));
        try
        {
            var manager = new StubWorkspaceManager(workspacePath);
            var builder = new WorkspaceContextBuilder(manager, _fileSystem);

            var result = await builder.BuildSystemPromptAsync(new AgentDescriptor
            {
                AgentId = "farnsworth",
                DisplayName = "Farnsworth",
                ModelId = "test-model",
                ApiProvider = "test-provider",
                SystemPrompt = "INLINE"
            });

            result.Should().Contain("AGENTS");
            result.Should().Contain("SOUL");
            result.Should().Contain("TOOLS");
            result.Should().Contain("BOOTSTRAP");
            result.Should().Contain("IDENTITY");
            result.Should().Contain("USER");
            _fileSystem.File.Exists(Path.Combine(workspacePath, "BOOTSTRAP.md")).Should().BeFalse();
        }
        finally
        {
            _fileSystem.Directory.Delete(Path.GetDirectoryName(workspacePath)!, recursive: true);
        }
    }

    [Fact]
    public async Task BuildSystemPromptAsync_WithAgentRootPath_ResolvesWorkspaceSubdirectory()
    {
        var workspacePath = CreateWorkspace(("AGENTS.md", "AGENTS"));
        var agentRootPath = Path.GetDirectoryName(workspacePath)!;
        try
        {
            var manager = new StubWorkspaceManager(agentRootPath);
            var builder = new WorkspaceContextBuilder(manager, _fileSystem);

            var result = await builder.BuildSystemPromptAsync(new AgentDescriptor
            {
                AgentId = "farnsworth",
                DisplayName = "Farnsworth",
                ModelId = "test-model",
                ApiProvider = "test-provider"
            });

            result.Should().Contain("AGENTS");
            result.Should().Contain("BotNexus", "SystemPromptBuilder adds the BotNexus identity line");
        }
        finally
        {
            _fileSystem.Directory.Delete(agentRootPath, recursive: true);
        }
    }

    private sealed class StubWorkspaceManager : IAgentWorkspaceManager
    {
        private readonly string _workspacePath;

        public StubWorkspaceManager(string workspacePath)
        {
            _workspacePath = workspacePath;
        }

        public Task<AgentWorkspace> LoadWorkspaceAsync(string agentName, CancellationToken ct = default)
            => Task.FromResult(new AgentWorkspace(agentName, Soul: string.Empty, Identity: string.Empty, User: string.Empty, Memory: string.Empty));

        public Task SaveMemoryAsync(string agentName, string content, CancellationToken ct = default)
            => Task.CompletedTask;

        public string GetWorkspacePath(string agentName)
            => _workspacePath;
    }

    private string CreateWorkspace(params (string FileName, string Content)[] files)
    {
        var rootPath = Path.Combine("C:\\", "botnexus-workspace-context-tests", Guid.NewGuid().ToString("N"));
        var workspacePath = Path.Combine(rootPath, "workspace");
        _fileSystem.Directory.CreateDirectory(workspacePath);

        foreach (var (fileName, content) in files)
            _fileSystem.File.WriteAllText(Path.Combine(workspacePath, fileName), content);

        return workspacePath;
    }
}
