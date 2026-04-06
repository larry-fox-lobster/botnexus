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

        path.Should().Be(Path.Combine(_homePath, "agents", "farnsworth"));
        File.Exists(Path.Combine(path, "SOUL.md")).Should().BeTrue();
        File.Exists(Path.Combine(path, "IDENTITY.md")).Should().BeTrue();
        File.Exists(Path.Combine(path, "USER.md")).Should().BeTrue();
        File.Exists(Path.Combine(path, "MEMORY.md")).Should().BeTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_homePath))
            Directory.Delete(_homePath, recursive: true);
    }
}
