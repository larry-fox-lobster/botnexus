using System.CommandLine;
using Spectre.Console;

namespace BotNexus.Cli.Commands;

internal sealed class BuildCommand
{
    public Command Build(Option<bool> verboseOption)
    {
        var pathOption = new Option<string?>(
            "--path",
            () => null,
            "Path to the repository root. Defaults to the current directory.");

        var devOption = new Option<bool>(
            "--dev",
            "Build from a development repo clone instead of the install location.");

        var command = new Command("build", "Build the BotNexus solution.")
        {
            pathOption,
            devOption
        };

        command.SetHandler(async context =>
        {
            var path = context.ParseResult.GetValueForOption(pathOption);
            var dev = context.ParseResult.GetValueForOption(devOption);
            var verbose = context.ParseResult.GetValueForOption(verboseOption);
            var repoRoot = ResolveRepoRoot(path, dev);
            context.ExitCode = await BuildSolutionAsync(repoRoot, verbose, context.GetCancellationToken());
        });

        return command;
    }

    internal static async Task<int> BuildSolutionAsync(string repoRoot, bool verbose, CancellationToken cancellationToken)
    {
        var solution = Path.Combine(repoRoot, "BotNexus.slnx");
        if (!File.Exists(solution))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Solution file not found: {Markup.Escape(solution)}");
            return 1;
        }

        AnsiConsole.MarkupLine("[blue][[build]][/] Building solution (Release, skipping test projects)...");

        return await BuildOutputStreamer.RunAsync(solution, repoRoot, verbose, cancellationToken);
    }

    internal static string ResolveRepoRoot(string? explicitPath, bool dev)
    {
        if (explicitPath is not null)
            return explicitPath;

        if (dev)
            return Directory.GetCurrentDirectory();

        return DefaultInstallPath;
    }

    internal static string DefaultInstallPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "botnexus");
}
