using BotNexus.Gateway.Configuration;
using FluentAssertions;

namespace BotNexus.Gateway.Tests;

public sealed class BotNexusHomeTests : IDisposable
{
    private readonly string _homePath;

    public BotNexusHomeTests()
    {
        _homePath = Path.Combine(Path.GetTempPath(), "botnexus-home-tests", Guid.NewGuid().ToString("N"));
    }

    [Fact]
    public void Initialize_CreatesRequiredDirectoriesIncludingAgents()
    {
        var home = new BotNexusHome(_homePath);

        home.Initialize();

        Directory.Exists(Path.Combine(_homePath, "extensions")).Should().BeTrue();
        Directory.Exists(Path.Combine(_homePath, "tokens")).Should().BeTrue();
        Directory.Exists(Path.Combine(_homePath, "sessions")).Should().BeTrue();
        Directory.Exists(Path.Combine(_homePath, "logs")).Should().BeTrue();
        Directory.Exists(Path.Combine(_homePath, "agents")).Should().BeTrue();
    }

    [Fact]
    public void GetAgentDirectory_CreatesWorkspaceAndScaffoldFiles()
    {
        var home = new BotNexusHome(_homePath);

        var path = home.GetAgentDirectory("farnsworth");
        var workspacePath = Path.Combine(path, "workspace");

        path.Should().Be(Path.Combine(_homePath, "agents", "farnsworth"));
        Directory.Exists(workspacePath).Should().BeTrue();
        Directory.Exists(Path.Combine(path, "data", "sessions")).Should().BeTrue();
        File.Exists(Path.Combine(workspacePath, "SOUL.md")).Should().BeTrue();
        File.Exists(Path.Combine(workspacePath, "IDENTITY.md")).Should().BeTrue();
        File.Exists(Path.Combine(workspacePath, "USER.md")).Should().BeTrue();
        File.Exists(Path.Combine(workspacePath, "MEMORY.md")).Should().BeTrue();
    }

    [Fact]
    public void GetAgentDirectory_WhenLegacyLayoutExists_MigratesFilesToWorkspace()
    {
        var home = new BotNexusHome(_homePath);
        var agentPath = Path.Combine(_homePath, "agents", "farnsworth");
        Directory.CreateDirectory(agentPath);
        File.WriteAllText(Path.Combine(agentPath, "SOUL.md"), "legacy soul");
        File.WriteAllText(Path.Combine(agentPath, "IDENTITY.md"), "legacy identity");
        File.WriteAllText(Path.Combine(agentPath, "USER.md"), "legacy user");
        File.WriteAllText(Path.Combine(agentPath, "MEMORY.md"), "legacy memory");

        var path = home.GetAgentDirectory("farnsworth");
        var workspacePath = Path.Combine(path, "workspace");

        Directory.Exists(workspacePath).Should().BeTrue();
        Directory.Exists(Path.Combine(path, "data", "sessions")).Should().BeTrue();
        File.Exists(Path.Combine(path, "SOUL.md")).Should().BeFalse();
        File.Exists(Path.Combine(path, "IDENTITY.md")).Should().BeFalse();
        File.Exists(Path.Combine(path, "USER.md")).Should().BeFalse();
        File.Exists(Path.Combine(path, "MEMORY.md")).Should().BeFalse();
        File.ReadAllText(Path.Combine(workspacePath, "SOUL.md")).Should().Be("legacy soul");
        File.ReadAllText(Path.Combine(workspacePath, "IDENTITY.md")).Should().Be("legacy identity");
        File.ReadAllText(Path.Combine(workspacePath, "USER.md")).Should().Be("legacy user");
        File.ReadAllText(Path.Combine(workspacePath, "MEMORY.md")).Should().Be("legacy memory");
    }

    public void Dispose()
    {
        if (Directory.Exists(_homePath))
            Directory.Delete(_homePath, recursive: true);
    }
}
