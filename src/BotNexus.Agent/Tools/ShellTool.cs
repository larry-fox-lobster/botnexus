using System.Diagnostics;
using BotNexus.Core.Abstractions;
using BotNexus.Core.Models;

namespace BotNexus.Agent.Tools;

/// <summary>Tool for executing shell commands in the agent workspace.</summary>
public sealed class ShellTool : ITool
{
    private readonly string _workspacePath;
    private readonly int _timeoutSeconds;

    public ShellTool(string workspacePath, int timeoutSeconds = 60)
    {
        _workspacePath = workspacePath;
        _timeoutSeconds = timeoutSeconds;
    }

    /// <inheritdoc/>
    public ToolDefinition Definition => new(
        "shell",
        "Execute a shell command and return the output.",
        new Dictionary<string, ToolParameterSchema>
        {
            ["command"] = new("string", "Shell command to execute", Required: true),
            ["workdir"] = new("string", "Working directory (optional, defaults to workspace)", Required: false)
        });

    /// <inheritdoc/>
    public async Task<string> ExecuteAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var command = arguments.GetValueOrDefault("command")?.ToString() ?? string.Empty;
        var workdir = arguments.GetValueOrDefault("workdir")?.ToString() ?? _workspacePath;

        if (string.IsNullOrWhiteSpace(command))
            return "Error: command is required";

        Directory.CreateDirectory(workdir);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

        try
        {
            var isWindows = OperatingSystem.IsWindows();
            var processInfo = new ProcessStartInfo
            {
                FileName = isWindows ? "cmd.exe" : "/bin/bash",
                Arguments = isWindows ? $"/c {command}" : $"-c \"{command.Replace("\"", "\\\"")}\"",
                WorkingDirectory = workdir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processInfo };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var errorTask = process.StandardError.ReadToEndAsync(cts.Token);

            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);

            var output = await outputTask.ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);

            var result = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(output)) result.AppendLine(output.TrimEnd());
            if (!string.IsNullOrEmpty(error)) result.AppendLine($"STDERR: {error.TrimEnd()}");
            result.AppendLine($"Exit code: {process.ExitCode}");

            return result.ToString().TrimEnd();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return $"Error: Command timed out after {_timeoutSeconds} seconds";
        }
    }
}
