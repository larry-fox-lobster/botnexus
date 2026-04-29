using System.CommandLine;
using System.Diagnostics;
using BotNexus.Cli.Services;
using BotNexus.Cli.Services;
using Spectre.Console;

namespace BotNexus.Cli.Commands;

/// <summary>
/// Update command: pull latest source, build, deploy extensions, and restart the gateway.
/// </summary>
internal sealed class UpdateCommand
{
    private readonly IGatewayProcessManager _processManager;

    public UpdateCommand(IGatewayProcessManager processManager)
    {
        _processManager = processManager;
    }

    public Command Build(Option<bool> verboseOption)
    {
        var sourceOption = new Option<string?>("--source", () => null, "Path to the BotNexus repository root. Defaults to ~/botnexus.");
        var targetOption = new Option<string?>("--target", () => null, "BotNexus home directory (config, workspace, extensions). Defaults to ~/.botnexus.");
        var portOption = new Option<int>("--port", () => 5005, "Gateway port.");

        var command = new Command("update", "Pull latest source, build, and restart the BotNexus gateway.")
        {
            sourceOption,
            targetOption,
            portOption
        };

        command.SetHandler(async context =>
        {
            var source = context.ParseResult.GetValueForOption(sourceOption);
            var target = context.ParseResult.GetValueForOption(targetOption);
            var port = context.ParseResult.GetValueForOption(portOption);
            var verbose = context.ParseResult.GetValueForOption(verboseOption);
            var repoRoot = CliPaths.ResolveSource(source);
            var home = CliPaths.ResolveTarget(target);
            context.ExitCode = await ExecuteAsync(repoRoot, home, port, verbose, context.GetCancellationToken());
        });

        return command;
    }

    internal async Task<int> ExecuteAsync(string repoRoot, string home, int port, bool verbose, CancellationToken cancellationToken)
    {
        // Step 1: git pull
        AnsiConsole.MarkupLine("[blue][[update]][/] Checking for updates...");

        var beforeSha = GetCommitSha(repoRoot);

        var pullResult = await RunGitPullAsync(repoRoot, verbose, cancellationToken);
        if (pullResult != 0)
        {
            AnsiConsole.MarkupLine("[red][[update]][/] \u2717 git pull failed. Check network or repo path.");
            return pullResult;
        }

        var afterSha = GetCommitSha(repoRoot);

        if (beforeSha == afterSha)
        {
            AnsiConsole.MarkupLine($"[blue][[update]][/] Already up to date ([dim]{Markup.Escape(Short(beforeSha))}[/]). Restarting gateway with current build...");
        }
        else
        {
            var count = await CountCommitsBetweenAsync(repoRoot, beforeSha, afterSha, cancellationToken);
            var countStr = count > 0 ? $"{count} new commit(s)" : "new commit(s)";
            AnsiConsole.MarkupLine($"[blue][[update]][/] [green]\u2713[/] Pulled {countStr}: [dim]{Markup.Escape(Short(beforeSha))}[/] \u2192 [dim]{Markup.Escape(Short(afterSha))}[/]");
        }

        // Step 2: Build
        AnsiConsole.MarkupLine("[blue][[update]][/] Building...");
        var buildResult = await BuildCommand.BuildSolutionAsync(repoRoot, verbose, cancellationToken);
        if (buildResult != 0)
        {
            AnsiConsole.MarkupLine("[red][[update]][/] \u2717 Build failed.");
            return buildResult;
        }
        AnsiConsole.MarkupLine("[blue][[update]][/] [green]\u2713[/] Build succeeded");

        // Step 3: Deploy extensions
        AnsiConsole.MarkupLine("[blue][[update]][/] Deploying extensions...");
        ServeCommand.DeployExtensions(repoRoot, home, verbose);
        AnsiConsole.MarkupLine("[blue][[update]][/] [green]\u2713[/] Extensions deployed");

        // Step 4: Stop old gateway
        AnsiConsole.MarkupLine("[blue][[update]][/] Restarting gateway...");
        await _processManager.StopAsync(home, cancellationToken);

        await Task.Delay(1000, cancellationToken);

        // Step 5: Start new gateway
        var gatewayDll = Path.Combine(repoRoot, "src", "gateway", "BotNexus.Gateway.Api", "bin", "Release", "net10.0", "BotNexus.Gateway.Api.dll");
        if (!File.Exists(gatewayDll))
        {
            AnsiConsole.MarkupLine($"[red][[update]][/] \u2717 Gateway binary not found: [dim]{Markup.Escape(gatewayDll)}[/]");
            return 1;
        }

        var gatewayUrl = $"http://localhost:{port}";
        var options = new GatewayStartOptions(
            ExecutablePath: gatewayDll,
            Arguments: $"--urls \"{gatewayUrl}\" --environment Development",
            Attached: false,
            HomePath: home
        );

        var startResult = await _processManager.StartAsync(options, cancellationToken);
        if (startResult.Success && startResult.Pid.HasValue)
        {
            AnsiConsole.MarkupLine($"[blue][[update]][/] [green]\u2713[/] Gateway restarted (PID [yellow]{startResult.Pid.Value}[/])");
            AnsiConsole.MarkupLine($"  URL:  [green]{Markup.Escape(gatewayUrl)}[/]");
            return 0;
        }
        else
        {
            AnsiConsole.MarkupLine($"[red][[update]][/] \u2717 Failed to start gateway: {Markup.Escape(startResult.Message ?? "Unknown error")}");
            return 1;
        }
    }

    private static async Task<int> RunGitPullAsync(string repoRoot, bool verbose, CancellationToken cancellationToken)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"-C \"{repoRoot}\" pull origin main",
                UseShellExecute = false,
                RedirectStandardOutput = !verbose,
                RedirectStandardError = !verbose,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc is null) return 1;

            await proc.WaitForExitAsync(cancellationToken);
            return proc.ExitCode;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red][[update]][/] git pull error: {Markup.Escape(ex.Message)}");
            return 1;
        }
    }

    private static string GetCommitSha(string repoRoot)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"-C \"{repoRoot}\" rev-parse HEAD",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
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

    private static async Task<int> CountCommitsBetweenAsync(string repoRoot, string from, string to, CancellationToken cancellationToken)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"-C \"{repoRoot}\" rev-list --count {from}..{to}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc is null) return 0;
            var output = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);
            return int.TryParse(output.Trim(), out var n) ? n : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static string Short(string sha) => sha.Length >= 7 ? sha[..7] : sha;
}
