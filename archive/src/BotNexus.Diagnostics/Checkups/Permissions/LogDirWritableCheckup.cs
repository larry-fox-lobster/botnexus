using BotNexus.Core.Abstractions;

namespace BotNexus.Diagnostics.Checkups.Permissions;

public sealed class LogDirWritableCheckup(DiagnosticsPaths paths) : IHealthCheckup
{
    private readonly DiagnosticsPaths _paths = paths ?? throw new ArgumentNullException(nameof(paths));

    public string Name => "LogDirWritable";
    public string Category => "Permissions";
    public string Description => "Checks ~/.botnexus/logs is writable.";
    public bool CanAutoFix => true;

    public Task<CheckupResult> RunAsync(CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(_paths.LogsPath);
            VerifyWritable(_paths.LogsPath);

            return Task.FromResult(new CheckupResult(
                CheckupStatus.Pass,
                $"BotNexus logs directory is writable: {_paths.LogsPath}."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new CheckupResult(
                CheckupStatus.Fail,
                $"BotNexus logs directory is not writable: {ex.Message}",
                "Grant write permissions to ~/.botnexus/logs or set BOTNEXUS_HOME to a writable location."));
        }
    }

    public Task<CheckupResult> FixAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(_paths.LogsPath);
        return RunAsync(ct);
    }

    private static void VerifyWritable(string path)
    {
        var probePath = Path.Combine(path, $".write-probe-{Guid.NewGuid():N}.tmp");
        File.WriteAllText(probePath, "probe");
        File.Delete(probePath);
    }
}
