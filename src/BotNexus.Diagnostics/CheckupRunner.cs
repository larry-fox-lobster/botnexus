using BotNexus.Core.Abstractions;

namespace BotNexus.Diagnostics;

public class CheckupRunner
{
    private readonly IReadOnlyList<IHealthCheckup> _checkups;

    public CheckupRunner(IEnumerable<IHealthCheckup> checkups)
    {
        ArgumentNullException.ThrowIfNull(checkups);
        _checkups = checkups.ToList();
    }

    public async Task<IReadOnlyList<CheckupResult>> RunAllAsync(string? category = null, CancellationToken ct = default)
    {
        IEnumerable<IHealthCheckup> checkupsToRun = _checkups;
        if (!string.IsNullOrWhiteSpace(category))
        {
            checkupsToRun = checkupsToRun.Where(c => string.Equals(c.Category, category, StringComparison.OrdinalIgnoreCase));
        }

        var results = new List<CheckupResult>();
        foreach (var checkup in checkupsToRun)
        {
            ct.ThrowIfCancellationRequested();
            var result = await checkup.RunAsync(ct).ConfigureAwait(false);
            results.Add(result);
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
}
