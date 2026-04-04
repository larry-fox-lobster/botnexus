using BotNexus.Core.Abstractions;

namespace BotNexus.Diagnostics;

public class CheckupRunner
{
    private readonly IReadOnlyList<IHealthCheckup> _checkups;
    public sealed record CheckupExecutionResult(
        IHealthCheckup Checkup,
        CheckupResult Result,
        bool WasAutoFixed,
        CheckupResult? InitialResult = null);

    public CheckupRunner(IEnumerable<IHealthCheckup> checkups)
    {
        ArgumentNullException.ThrowIfNull(checkups);
        _checkups = checkups.ToList();
    }

    public async Task<IReadOnlyList<CheckupResult>> RunAllAsync(string? category = null, CancellationToken ct = default)
    {
        var results = new List<CheckupResult>();
        foreach (var checkup in FilterCheckups(category))
        {
            ct.ThrowIfCancellationRequested();
            var result = await checkup.RunAsync(ct).ConfigureAwait(false);
            results.Add(result);
        }

        return results;
    }

    // Doctor CLI integration note (Bender): wire --fix/--force to this method.
    public async Task<IReadOnlyList<CheckupExecutionResult>> RunAndFixAsync(
        string? category,
        bool force,
        Func<IHealthCheckup, CheckupResult, Task<bool>>? promptUser,
        CancellationToken ct = default)
    {
        var results = new List<CheckupExecutionResult>();
        foreach (var checkup in FilterCheckups(category))
        {
            ct.ThrowIfCancellationRequested();
            var initialResult = await checkup.RunAsync(ct).ConfigureAwait(false);
            var finalResult = initialResult;
            var wasAutoFixed = false;

            if (initialResult.Status == CheckupStatus.Fail && checkup.CanAutoFix)
            {
                var shouldFix = force;
                if (!shouldFix && promptUser is not null)
                    shouldFix = await promptUser(checkup, initialResult).ConfigureAwait(false);

                if (shouldFix)
                {
                    ct.ThrowIfCancellationRequested();
                    finalResult = await checkup.FixAsync(ct).ConfigureAwait(false);
                    wasAutoFixed = true;
                }
            }

            results.Add(new CheckupExecutionResult(
                checkup,
                finalResult,
                wasAutoFixed,
                wasAutoFixed ? initialResult : null));
        }

        return results;
    }

    public IReadOnlyList<string> GetCategories()
    {
        var categories = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var checkup in _checkups)
        {
            if (string.IsNullOrWhiteSpace(checkup.Category) || !seen.Add(checkup.Category))
            {
                continue;
            }

            categories.Add(checkup.Category);
        }

        return categories;
    }

    private IEnumerable<IHealthCheckup> FilterCheckups(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return _checkups;

        return _checkups.Where(c => string.Equals(c.Category, category, StringComparison.OrdinalIgnoreCase));
    }
}
