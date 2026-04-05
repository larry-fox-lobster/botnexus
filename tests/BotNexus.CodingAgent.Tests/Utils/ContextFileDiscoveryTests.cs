using System.Text;
using BotNexus.CodingAgent.Utils;
using FluentAssertions;

namespace BotNexus.CodingAgent.Tests.Utils;

public sealed class ContextFileDiscoveryTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(Path.GetTempPath(), $"botnexus-context-discovery-{Guid.NewGuid():N}");

    public ContextFileDiscoveryTests()
    {
        Directory.CreateDirectory(_testRoot);
    }

    [Fact]
    public async Task DiscoverAsync_FindsInstructionsInParentDirectory()
    {
        var repoRoot = Path.Combine(_testRoot, "repo");
        var workingDirectory = Path.Combine(repoRoot, "src", "feature");
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        Directory.CreateDirectory(Path.Combine(repoRoot, ".github"));
        Directory.CreateDirectory(workingDirectory);
        await File.WriteAllTextAsync(Path.Combine(repoRoot, ".github", "copilot-instructions.md"), "parent instructions");

        var discovered = await ContextFileDiscovery.DiscoverAsync(workingDirectory, CancellationToken.None);

        discovered.Should().Contain(file => file.Content.Contains("parent instructions", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DiscoverAsync_StopsAtGitBoundary()
    {
        var outsideRoot = Path.Combine(_testRoot, "outside");
        var repoRoot = Path.Combine(outsideRoot, "repo");
        var workingDirectory = Path.Combine(repoRoot, "src");
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        Directory.CreateDirectory(Path.Combine(repoRoot, ".github"));
        Directory.CreateDirectory(Path.Combine(outsideRoot, ".github"));
        Directory.CreateDirectory(workingDirectory);
        await File.WriteAllTextAsync(Path.Combine(repoRoot, ".github", "copilot-instructions.md"), "repo instructions");
        await File.WriteAllTextAsync(Path.Combine(outsideRoot, ".github", "copilot-instructions.md"), "outside instructions");

        var discovered = await ContextFileDiscovery.DiscoverAsync(workingDirectory, CancellationToken.None);

        discovered.Should().Contain(file => file.Content.Contains("repo instructions", StringComparison.Ordinal));
        discovered.Should().NotContain(file => file.Content.Contains("outside instructions", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DiscoverAsync_ClosestPathWinsOnConflict()
    {
        var repoRoot = Path.Combine(_testRoot, "repo");
        var child = Path.Combine(repoRoot, "app");
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(repoRoot, "AGENTS.md"), "parent");
        await File.WriteAllTextAsync(Path.Combine(child, "AGENTS.md"), "child");

        var discovered = await ContextFileDiscovery.DiscoverAsync(child, CancellationToken.None);
        var agentsFiles = discovered.Where(file => file.Path.EndsWith("AGENTS.md", StringComparison.OrdinalIgnoreCase)).ToList();

        agentsFiles.Should().ContainSingle();
        agentsFiles[0].Content.Should().Be("child");
    }

    [Fact]
    public async Task DiscoverAsync_StaysWithinContextBudget()
    {
        var repoRoot = Path.Combine(_testRoot, "repo");
        var workingDirectory = Path.Combine(repoRoot, "src");
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        Directory.CreateDirectory(Path.Combine(repoRoot, ".github"));
        Directory.CreateDirectory(workingDirectory);
        await File.WriteAllTextAsync(Path.Combine(repoRoot, ".github", "copilot-instructions.md"), new string('a', 20_000));
        await File.WriteAllTextAsync(Path.Combine(workingDirectory, "AGENTS.md"), new string('b', 8_000));

        var discovered = await ContextFileDiscovery.DiscoverAsync(workingDirectory, CancellationToken.None);
        var totalBytes = discovered.Sum(file => Encoding.UTF8.GetByteCount(file.Content));

        totalBytes.Should().BeLessThanOrEqualTo(16 * 1024);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }
}
