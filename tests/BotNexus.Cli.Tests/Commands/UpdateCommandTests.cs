using System.CommandLine;
using BotNexus.Cli.Commands;
using BotNexus.Cli.Services;
using NSubstitute;

namespace BotNexus.Cli.Tests.Commands;

public class UpdateCommandTests
{
    private static UpdateCommand BuildCommand()
    {
        var pm = Substitute.For<IGatewayProcessManager>();
        pm.StopAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new GatewayStopResult(true, null));
        pm.StartAsync(Arg.Any<GatewayStartOptions>(), Arg.Any<CancellationToken>())
            .Returns(new GatewayStartResult(true, 99999, null));
        return new UpdateCommand(pm);
    }

    [Fact]
    public void Update_command_is_registered_on_root()
    {
        var verbose = new Option<bool>("--verbose");
        var command = BuildCommand().Build(verbose);

        command.Name.ShouldBe("update");
    }

    [Fact]
    public void Update_command_has_expected_options()
    {
        var verbose = new Option<bool>("--verbose");
        var command = BuildCommand().Build(verbose);

        var names = command.Options.Select(o => o.Name).ToList();
        names.ShouldContain("source");
        names.ShouldContain("target");
        names.ShouldContain("port");
    }

    [Fact]
    public async Task Update_with_non_git_directory_returns_nonzero()
    {
        var pm = Substitute.For<IGatewayProcessManager>();
        var cmd = new UpdateCommand(pm);

        var tempDir = Path.Combine(Path.GetTempPath(), $"botnexus-update-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var exitCode = await cmd.ExecuteAsync(
                repoRoot: tempDir,
                home: tempDir,
                port: 5005,
                verbose: false,
                cancellationToken: CancellationToken.None);

            exitCode.ShouldNotBe(0);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
