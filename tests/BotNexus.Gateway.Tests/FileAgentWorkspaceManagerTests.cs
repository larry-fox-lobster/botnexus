using BotNexus.Gateway.Agents;
using BotNexus.Gateway.Configuration;
using Microsoft.Data.Sqlite;
using System.IO.Abstractions.TestingHelpers;

namespace BotNexus.Gateway.Tests;

public sealed class FileAgentWorkspaceManagerTests : IDisposable
{
    private readonly string _homePath;
    private readonly FileAgentWorkspaceManager _workspaceManager;
    private readonly MockFileSystem _fileSystem;

    public FileAgentWorkspaceManagerTests()
    {
        _homePath = @"C:\botnexus\workspace-tests";
        _fileSystem = new MockFileSystem();
        _workspaceManager = new FileAgentWorkspaceManager(new BotNexusHome(_fileSystem, _homePath), _fileSystem);
    }

    [Fact]
    public async Task LoadWorkspaceAsync_WhenMissing_CreatesWorkspaceAndReturnsEmptyFiles()
    {
        var workspace = await _workspaceManager.LoadWorkspaceAsync("farnsworth");

        workspace.AgentName.ShouldBe("farnsworth");
        workspace.Soul.ShouldContain("# Soul");
        workspace.Identity.ShouldContain("# Identity");
        workspace.User.ShouldContain("# User");
        workspace.Memory.ShouldBeEmpty();

        var workspacePath = _workspaceManager.GetWorkspacePath("farnsworth");
        _fileSystem.File.Exists(Path.Combine(workspacePath, "AGENTS.md")).ShouldBeTrue();
        _fileSystem.File.Exists(Path.Combine(workspacePath, "SOUL.md")).ShouldBeTrue();
        _fileSystem.File.Exists(Path.Combine(workspacePath, "TOOLS.md")).ShouldBeTrue();
        _fileSystem.File.Exists(Path.Combine(workspacePath, "BOOTSTRAP.md")).ShouldBeTrue();
        _fileSystem.File.Exists(Path.Combine(workspacePath, "IDENTITY.md")).ShouldBeTrue();
        _fileSystem.File.Exists(Path.Combine(workspacePath, "USER.md")).ShouldBeTrue();
        _fileSystem.File.Exists(Path.Combine(workspacePath, "MEMORY.md")).ShouldBeFalse();
    }

    [Fact]
    public async Task SaveMemoryAsync_AppendsMemoryFile()
    {
        await _workspaceManager.SaveMemoryAsync("farnsworth", "first line");
        await _workspaceManager.SaveMemoryAsync("farnsworth", "second line");

        var memoryPath = Path.Combine(_workspaceManager.GetWorkspacePath("farnsworth"), "MEMORY.md");
        var content = await _fileSystem.File.ReadAllTextAsync(memoryPath);

        content.ShouldContain("first line");
        content.ShouldContain("second line");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (_fileSystem.Directory.Exists(_homePath))
            _fileSystem.Directory.Delete(_homePath, recursive: true);
    }
}
