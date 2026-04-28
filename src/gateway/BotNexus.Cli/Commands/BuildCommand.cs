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

        // Resolve git commit SHA from the repo so it gets embedded in the gateway binary.
        // Falls back to "unknown" if git is unavailable or the directory is not a repo.
        var commitSha = ResolveCommitSha(repoRoot);

        AnsiConsole.MarkupLine("[blue][[build]][/] Building solution (Release, skipping test projects)...");

        return await BuildOutputStreamer.RunAsync(solution, repoRoot, commitSha, verbose, cancellationToken);
    }

    /// <summary>
    /// Resolves the current git commit SHA from the repo at <paramref name="repoRoot"/>.
    /// Returns <c>"unknown"</c> if git is not available or the directory is not a git repo.
    /// </summary>
    internal static string ResolveCommitSha(string repoRoot)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = "-C \"" + repoRoot + "\" rev-parse HEAD",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc is null) return "unknown";
            var sha = proc.StandardOutput.ReadLine()?.Trim();
            proc.WaitForExit();
            return string.IsNullOrWhiteSpace(sha) ? "unknown" : sha;
        }
        catch
        {
            return "unknown";
        }
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
