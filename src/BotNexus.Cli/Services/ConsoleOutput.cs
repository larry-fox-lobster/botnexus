using System.Text.Json;

namespace BotNexus.Cli.Services;

public enum ConsoleStatus
{
    Success,
    Warning,
    Error
}

public static class ConsoleOutput
{
    private const string Reset = "\u001b[0m";
    private const string BoldUnderline = "\u001b[1;4m";
    private const string Green = "\u001b[32m";
    private const string Yellow = "\u001b[33m";
    private const string Red = "\u001b[31m";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static void WriteTable(IEnumerable<string> headers, IEnumerable<IEnumerable<string?>> rows)
    {
        var headerList = headers.ToList();
        var rowList = rows.Select(r => r.Select(c => c ?? string.Empty).ToList()).ToList();
        var widths = Enumerable.Range(0, headerList.Count)
            .Select(index => Math.Max(
                headerList[index].Length,
                rowList.Select(r => index < r.Count ? r[index].Length : 0).DefaultIfEmpty(0).Max()))
            .ToList();

        Console.WriteLine(BuildRow(headerList, widths));
        Console.WriteLine(BuildSeparator(widths));
        foreach (var row in rowList)
            Console.WriteLine(BuildRow(row, widths));
    }

    public static void WriteStatus(ConsoleStatus status, string message)
    {
        var (icon, color) = status switch
        {
            ConsoleStatus.Success => ("✅", Green),
            ConsoleStatus.Warning => ("⚠️", Yellow),
            ConsoleStatus.Error => ("❌", Red),
            _ => ("•", string.Empty)
        };

        WriteWithColor($"{icon} {message}", color);
    }

    public static void WriteHeader(string text)
    {
        if (!Console.IsOutputRedirected)
            Console.WriteLine($"{BoldUnderline}{text}{Reset}");
        else
            Console.WriteLine(text);
    }

    public static void WriteJson(object? value)
        => Console.WriteLine(JsonSerializer.Serialize(value, JsonOptions));

    private static string BuildRow(IReadOnlyList<string> values, IReadOnlyList<int> widths)
    {
        var columns = widths.Select((width, index) =>
        {
            var value = index < values.Count ? values[index] : string.Empty;
            return value.PadRight(width);
        });

        return $"| {string.Join(" | ", columns)} |";
    }

    private static string BuildSeparator(IReadOnlyList<int> widths)
        => $"+-{string.Join("-+-", widths.Select(w => new string('-', w)))}-+";

    private static void WriteWithColor(string text, string color)
    {
        if (Console.IsOutputRedirected || string.IsNullOrEmpty(color))
        {
            Console.WriteLine(text);
            return;
        }

        Console.WriteLine($"{color}{text}{Reset}");
    }
}
