using BotNexus.Core.Abstractions;

namespace BotNexus.Diagnostics.Checkups.Permissions;

public sealed class HomeDirWritableCheckup(DiagnosticsPaths paths) : IHealthCheckup
{
    private readonly DiagnosticsPaths _paths = paths ?? throw new ArgumentNullException(nameof(paths));

    public string Name => "HomeDirWritable";
    public string Category => "Permissions";
    public string Description => "Checks ~/.botnexus is writable.";
    public bool CanAutoFix => true;

    public Task<CheckupResult> RunAsync(CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(_paths.HomePath);
            VerifyWritable(_paths.HomePath);

            return Task.FromResult(new CheckupResult(
                CheckupStatus.Pass,
                $"BotNexus home directory is writable: {_paths.HomePath}."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new CheckupResult(
                CheckupStatus.Fail,
                $"BotNexus home directory is not writable: {ex.Message}",
                "Grant write permissions to ~/.botnexus or set BOTNEXUS_HOME to a writable directory."));
        }
    }

    public Task<CheckupResult> FixAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(_paths.HomePath);
        return RunAsync(ct);
    }

    private static void VerifyWritable(string path)
    {
        var probePath = Path.Combine(path, $".write-probe-{Guid.NewGuid():N}.tmp");
        File.WriteAllText(probePath, "probe");
        File.Delete(probePath);
    }
}
