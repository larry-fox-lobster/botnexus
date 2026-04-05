using System.Text;
using BotNexus.CodingAgent.Utils;
using FluentAssertions;

namespace BotNexus.CodingAgent.Tests.Tools;

public sealed class ContextFileDiscoveryTests : IDisposable
{
    private readonly string _workingDirectory = Path.Combine(Path.GetTempPath(), $"botnexus-context-discovery-{Guid.NewGuid():N}");

    public ContextFileDiscoveryTests()
    {
        Directory.CreateDirectory(_workingDirectory);
    }

    [Fact]
    public async Task DiscoverAsync_WithAllSourcesPresent_ReturnsExpectedOrder()
    {
        Directory.CreateDirectory(Path.Combine(_workingDirectory, ".github"));
        Directory.CreateDirectory(Path.Combine(_workingDirectory, "docs"));
        await File.WriteAllTextAsync(Path.Combine(_workingDirectory, ".github", "copilot-instructions.md"), "copilot");
        await File.WriteAllTextAsync(Path.Combine(_workingDirectory, "README.md"), "readme");
        await File.WriteAllTextAsync(Path.Combine(_workingDirectory, "docs", "a.md"), "doc-a");
        await File.WriteAllTextAsync(Path.Combine(_workingDirectory, "docs", "b.md"), "doc-b");

        var discovered = await ContextFileDiscovery.DiscoverAsync(_workingDirectory, CancellationToken.None);

        discovered.Select(file => file.Path).Should().ContainInOrder(Path.Combine(".github", "copilot-instructions.md"), "README.md", Path.Combine("docs", "a.md"), Path.Combine("docs", "b.md"));
    }

    [Fact]
    public async Task DiscoverAsync_WithOnlyReadme_ReturnsReadme()
    {
        await File.WriteAllTextAsync(Path.Combine(_workingDirectory, "README.md"), "readme-only");
        var discovered = await ContextFileDiscovery.DiscoverAsync(_workingDirectory, CancellationToken.None);
        discovered.Should().ContainSingle();
        discovered[0].Path.Should().Be("README.md");
        discovered[0].Content.Should().Be("readme-only");
    }

    [Fact]
    public async Task DiscoverAsync_WithOnlyCopilotInstructions_ReturnsInstructions()
    {
        Directory.CreateDirectory(Path.Combine(_workingDirectory, ".github"));
        await File.WriteAllTextAsync(Path.Combine(_workingDirectory, ".github", "copilot-instructions.md"), "copilot-only");
        var discovered = await ContextFileDiscovery.DiscoverAsync(_workingDirectory, CancellationToken.None);
        discovered.Should().ContainSingle();
        discovered[0].Path.Should().Be(Path.Combine(".github", "copilot-instructions.md"));
        discovered[0].Content.Should().Be("copilot-only");
    }

    [Fact]
    public async Task DiscoverAsync_WithEmptyDirectory_ReturnsNoFiles()
    {
        var discovered = await ContextFileDiscovery.DiscoverAsync(_workingDirectory, CancellationToken.None);
        discovered.Should().BeEmpty();
    }

    [Fact]
    public async Task DiscoverAsync_RespectsSixteenKilobyteBudgetAndTruncates()
    {
        Directory.CreateDirectory(Path.Combine(_workingDirectory, ".github"));
        var oversized = new string('a', 20_000);
        await File.WriteAllTextAsync(Path.Combine(_workingDirectory, ".github", "copilot-instructions.md"), oversized);
        await File.WriteAllTextAsync(Path.Combine(_workingDirectory, "README.md"), "readme");
        var discovered = await ContextFileDiscovery.DiscoverAsync(_workingDirectory, CancellationToken.None);

        discovered.Should().ContainSingle();
        discovered[0].Content.Should().Contain("[truncated]");
        var totalBytes = discovered.Sum(file => Encoding.UTF8.GetByteCount(file.Content));
        totalBytes.Should().BeLessThanOrEqualTo(16 * 1024);
    }

    [Fact]
    public async Task DiscoverAsync_SkipsNonMarkdownFilesInDocs()
    {
        Directory.CreateDirectory(Path.Combine(_workingDirectory, "docs"));
        await File.WriteAllTextAsync(Path.Combine(_workingDirectory, "docs", "guide.md"), "guide");
        await File.WriteAllBytesAsync(Path.Combine(_workingDirectory, "docs", "image.png"), [0x01, 0x02]);
        var discovered = await ContextFileDiscovery.DiscoverAsync(_workingDirectory, CancellationToken.None);

        discovered.Should().ContainSingle();
        discovered[0].Path.Should().Be(Path.Combine("docs", "guide.md"));
    }

    [Fact]
    public async Task DiscoverAsync_WithNoDocsDirectory_DoesNotFail()
    {
        await File.WriteAllTextAsync(Path.Combine(_workingDirectory, "README.md"), "readme");
        var discovered = await ContextFileDiscovery.DiscoverAsync(_workingDirectory, CancellationToken.None);
        discovered.Should().ContainSingle();
        discovered[0].Path.Should().Be("README.md");
    }

    [Fact]
    public async Task DiscoverAsync_PrioritizesCopilotInstructionsBeforeReadmeAndDocs()
    {
        Directory.CreateDirectory(Path.Combine(_workingDirectory, ".github"));
        Directory.CreateDirectory(Path.Combine(_workingDirectory, "docs"));
        await File.WriteAllTextAsync(Path.Combine(_workingDirectory, ".github", "copilot-instructions.md"), "copilot");
        await File.WriteAllTextAsync(Path.Combine(_workingDirectory, "README.md"), "readme");
        await File.WriteAllTextAsync(Path.Combine(_workingDirectory, "docs", "guide.md"), "guide");
        var discovered = await ContextFileDiscovery.DiscoverAsync(_workingDirectory, CancellationToken.None);

        discovered[0].Path.Should().Be(Path.Combine(".github", "copilot-instructions.md"));
        discovered[1].Path.Should().Be("README.md");
        discovered[2].Path.Should().Be(Path.Combine("docs", "guide.md"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_workingDirectory))
        {
            Directory.Delete(_workingDirectory, recursive: true);
        }
    }
}
