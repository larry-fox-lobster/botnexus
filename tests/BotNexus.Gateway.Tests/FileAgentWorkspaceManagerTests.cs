using BotNexus.Gateway.Agents;
using BotNexus.Gateway.Configuration;
using FluentAssertions;

namespace BotNexus.Gateway.Tests;

public sealed class FileAgentWorkspaceManagerTests : IDisposable
{
    private readonly string _homePath;
    private readonly FileAgentWorkspaceManager _workspaceManager;

    public FileAgentWorkspaceManagerTests()
    {
        _homePath = Path.Combine(Path.GetTempPath(), "botnexus-workspace-tests", Guid.NewGuid().ToString("N"));
        _workspaceManager = new FileAgentWorkspaceManager(new BotNexusHome(_homePath));
    }

    [Fact]
    public async Task LoadWorkspaceAsync_WhenMissing_CreatesWorkspaceAndReturnsEmptyFiles()
    {
        var workspace = await _workspaceManager.LoadWorkspaceAsync("farnsworth");

        workspace.AgentName.Should().Be("farnsworth");
        workspace.Soul.Should().BeEmpty();
        workspace.Identity.Should().BeEmpty();
        workspace.User.Should().BeEmpty();
        workspace.Memory.Should().BeEmpty();

        var workspacePath = _workspaceManager.GetWorkspacePath("farnsworth");
        File.Exists(Path.Combine(workspacePath, "SOUL.md")).Should().BeTrue();
        File.Exists(Path.Combine(workspacePath, "IDENTITY.md")).Should().BeTrue();
        File.Exists(Path.Combine(workspacePath, "USER.md")).Should().BeTrue();
        File.Exists(Path.Combine(workspacePath, "MEMORY.md")).Should().BeTrue();
    }

    [Fact]
    public async Task SaveMemoryAsync_AppendsMemoryFile()
    {
        await _workspaceManager.SaveMemoryAsync("farnsworth", "first line");
        await _workspaceManager.SaveMemoryAsync("farnsworth", "second line");

        var memoryPath = Path.Combine(_workspaceManager.GetWorkspacePath("farnsworth"), "MEMORY.md");
        var content = await File.ReadAllTextAsync(memoryPath);

        content.Should().Contain("first line");
        content.Should().Contain("second line");
    }

    public void Dispose()
    {
        if (Directory.Exists(_homePath))
            Directory.Delete(_homePath, recursive: true);
    }
}
