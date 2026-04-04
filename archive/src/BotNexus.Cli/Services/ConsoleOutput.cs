using Spectre.Console;
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
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static void WriteBanner(string version)
    {
        if (Console.IsOutputRedirected) return;
        
        AnsiConsole.Write(
            new FigletText("BotNexus")
                .Color(Color.CornflowerBlue));
        AnsiConsole.MarkupLine($"[dim]v{Markup.Escape(version)}[/]");
        AnsiConsole.WriteLine();
    }

    public static void WriteStatus(ConsoleStatus status, string message)
    {
        var (icon, style) = status switch
        {
            ConsoleStatus.Success => ("✅", "green"),
            ConsoleStatus.Warning => ("⚠️", "yellow"),
            ConsoleStatus.Error => ("❌", "red"),
            _ => ("•", "default")
        };

        AnsiConsole.MarkupLine($"[{style}]{icon} {Markup.Escape(message)}[/]");
    }

    public static void WriteHeader(string text)
    {
        AnsiConsole.Write(new Rule($"[bold]{Markup.Escape(text)}[/]").LeftJustified());
    }

    public static void WriteTable(IEnumerable<string> headers, IEnumerable<IEnumerable<string?>> rows)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey);

        foreach (var header in headers)
            table.AddColumn(new TableColumn(Markup.Escape(header)).Header(new Markup($"[bold]{Markup.Escape(header)}[/]")));

        foreach (var row in rows)
            table.AddRow(row.Select(cell => Markup.Escape(cell ?? string.Empty)).ToArray());

        AnsiConsole.Write(table);
    }

    public static void WriteJson(object? value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        AnsiConsole.Write(
            new Panel(Markup.Escape(json))
                .Header("[bold]Configuration[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Grey));
    }

    public static string Prompt(string label, string defaultValue)
    {
        if (Console.IsInputRedirected)
        {
            Console.Write($"{label}{(string.IsNullOrWhiteSpace(defaultValue) ? string.Empty : $" [{defaultValue}]")}: ");
            var value = Console.ReadLine();
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
        }

        if (string.IsNullOrWhiteSpace(defaultValue))
            return AnsiConsole.Prompt(
                new TextPrompt<string>($"[bold]{Markup.Escape(label)}[/]:"));

        return AnsiConsole.Prompt(
            new TextPrompt<string>($"[bold]{Markup.Escape(label)}[/]:")
                .DefaultValue(defaultValue)
                .ShowDefaultValue());
    }

    public static bool Confirm(string message, bool defaultValue = false)
    {
        if (Console.IsInputRedirected)
        {
            Console.Write($"{message} [y/N] ");
            var value = Console.ReadLine();
            return !string.IsNullOrWhiteSpace(value) &&
                   value.Trim().Equals("y", StringComparison.OrdinalIgnoreCase);
        }

        return AnsiConsole.Confirm(message, defaultValue);
    }

    public static string Select(string prompt, IEnumerable<string> choices)
    {
        var choiceList = choices.ToList();
        if (Console.IsInputRedirected)
        {
            Console.Write($"{prompt} ({string.Join("/", choiceList)}): ");
            var value = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(value) && choiceList.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
                return value.Trim();
            return choiceList.First();
        }

        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"[bold]{Markup.Escape(prompt)}[/]")
                .AddChoices(choiceList));
    }
}
