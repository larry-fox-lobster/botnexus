using BotNexus.CodingAgent.Session;

namespace BotNexus.CodingAgent.Cli;

/// <summary>
/// Writes formatted terminal output for coding-agent interactions.
/// </summary>
public sealed class OutputFormatter
{
    private readonly bool _nonInteractive;

    public OutputFormatter(bool nonInteractive = false)
    {
        _nonInteractive = nonInteractive;
    }

    public void WriteWelcome(string model, SessionInfo? session)
    {
        if (_nonInteractive)
        {
            Console.WriteLine($"[session:start] model={model}");
            if (session is not null)
            {
                Console.WriteLine($"[session:info] id={session.Id} name={session.Name} messages={session.MessageCount}");
            }
            return;
        }

        WriteColoredLine("╔══════════════════════════════════════╗", ConsoleColor.Cyan);
        WriteColoredLine("║         BotNexus Coding Agent       ║", ConsoleColor.Cyan);
        WriteColoredLine("╚══════════════════════════════════════╝", ConsoleColor.Cyan);
        WriteColoredLine($"Model: {model}", ConsoleColor.DarkCyan);

        if (session is not null)
        {
            WriteSessionInfo(session);
        }
    }

    public void WriteAssistantText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (_nonInteractive)
        {
            Console.Write(text);
        }
        else
        {
            WriteColored(text, ConsoleColor.Gray);
        }
    }

    public void WriteToolStart(string toolName, string args)
    {
        if (_nonInteractive)
        {
            Console.WriteLine($"[tool:start] {toolName}");
            return;
        }

        var summary = string.IsNullOrWhiteSpace(args) ? "{}" : args;
        WriteColoredLine($"🔧 {toolName}: {summary}", ConsoleColor.Yellow);
    }

    public void WriteToolEnd(string toolName, bool success)
    {
        if (_nonInteractive)
        {
            Console.WriteLine($"[tool:end] {toolName} success={success.ToString().ToLowerInvariant()}");
            return;
        }

        var icon = success ? "✅" : "❌";
        var color = success ? ConsoleColor.Green : ConsoleColor.Red;
        WriteColoredLine($"{icon} {toolName}", color);
    }

    public void WriteError(string message)
    {
        if (_nonInteractive)
        {
            Console.WriteLine($"[error] {message}");
        }
        else
        {
            WriteColoredLine(message, ConsoleColor.Red);
        }
    }

    public void WriteTurnSeparator()
    {
        if (_nonInteractive)
        {
            Console.WriteLine("---");
        }
        else
        {
            WriteColoredLine(Environment.NewLine + "────────────────────────────────────────", ConsoleColor.DarkGray);
        }
    }

    public void WriteSessionInfo(SessionInfo session)
    {
        if (_nonInteractive)
        {
            Console.WriteLine($"[session:info] id={session.Id} name={session.Name} messages={session.MessageCount} updated={session.UpdatedAt:yyyy-MM-dd HH:mm:ss}");
        }
        else
        {
            WriteColoredLine(
                $"Session: {session.Id} ({session.Name}) | Messages: {session.MessageCount} | Updated: {session.UpdatedAt:yyyy-MM-dd HH:mm:ss}",
                ConsoleColor.DarkGray);
        }
    }

    private static void WriteColored(string text, ConsoleColor color)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ForegroundColor = previous;
    }

    private static void WriteColoredLine(string text, ConsoleColor color)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ForegroundColor = previous;
    }
}
