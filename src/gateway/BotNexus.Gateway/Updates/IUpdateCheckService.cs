namespace BotNexus.Gateway.Updates;

/// <summary>
/// Manages periodic GitHub commit polling, cached update state, and self-update process spawning.
/// Implementations must also implement <see cref="Microsoft.Extensions.Hosting.IHostedService"/>
/// so the background polling loop is driven by the generic host lifecycle.
/// </summary>
public interface IUpdateCheckService
{
    /// <summary>
    /// Returns the most recently cached update status without hitting GitHub.
    /// Safe to call from any thread — always returns immediately.
    /// </summary>
    UpdateStatusResult GetCurrentStatus();

    /// <summary>
    /// Forces an immediate GitHub poll and refreshes the cached status.
    /// Callers should be tolerant of transient failures — the result will still be returned
    /// with <see cref="UpdateStatusResult.Error"/> populated rather than throwing.
    /// </summary>
    Task<UpdateStatusResult> CheckNowAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates prerequisites and spawns the CLI update process if all checks pass.
    /// Returns 202-style result when the process is launched; 409 when already in progress;
    /// 412 when config or runtime prerequisites are missing.
    /// </summary>
    Task<UpdateStartResult> StartUpdateAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Snapshot of the current auto-update state exposed by <see cref="IUpdateCheckService"/>.
/// </summary>
public sealed record UpdateStatusResult(
    bool Enabled,
    bool IsChecking,
    bool IsUpdateAvailable,
    bool IsUpdateInProgress,
    string CurrentCommitSha,
    string CurrentCommitShort,
    string? LatestCommitSha,
    string? LatestCommitShort,
    DateTimeOffset? LastCheckedAt,
    DateTimeOffset? NextCheckAt,
    string? RepositoryOwner,
    string? RepositoryName,
    string? Branch,
    string? CompareUrl,
    string? Error);

/// <summary>
/// Result of a <see cref="IUpdateCheckService.StartUpdateAsync"/> call.
/// </summary>
public sealed record UpdateStartResult(
    bool Started,
    int? ProcessId,
    string Message);
