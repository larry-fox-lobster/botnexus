namespace BotNexus.IntegrationTests;

/// <summary>
/// Dual-output logger: writes to both console and a log file for post-run analysis.
/// </summary>
public sealed class TestLogger : IDisposable
{
    private readonly StreamWriter _fileWriter;
    private readonly object _lock = new();
    private readonly string _logPath;

    public TestLogger(string logPath)
    {
        _logPath = logPath;
        var dir = Path.GetDirectoryName(logPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        _fileWriter = new StreamWriter(logPath, append: false) { AutoFlush = true };

        Write($"=== BotNexus Integration Test Log — {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC ===");
    }

    public string LogPath => _logPath;

    public void Write(string message)
    {
        var line = $"[{DateTimeOffset.UtcNow:HH:mm:ss.fff}] {message}";
        lock (_lock)
        {
            Console.WriteLine($"    {line}");
            _fileWriter.WriteLine(line);
        }
    }

    public void WriteHeader(string scenarioName)
    {
        var separator = new string('─', 60);
        lock (_lock)
        {
            var header = $"\n{separator}\n▶ {scenarioName}\n{separator}";
            Console.Write(header);
            _fileWriter.Write(header);
        }
    }

    public void WriteResult(string scenarioName, bool passed, string? error = null)
    {
        lock (_lock)
        {
            if (passed)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(" PASS");
                Console.ResetColor();
                _fileWriter.WriteLine($" PASS");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(" FAIL");
                Console.ResetColor();
                _fileWriter.WriteLine($" FAIL");
                if (error is not null)
                {
                    Console.WriteLine($"    ❌ {error}");
                    _fileWriter.WriteLine($"    ❌ {error}");
                }
            }
        }
    }

    public void Dispose() => _fileWriter.Dispose();
}
