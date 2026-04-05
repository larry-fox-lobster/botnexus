using BotNexus.CodingAgent.Session;

namespace BotNexus.CodingAgent.Cli;

/// <summary>
/// Writes formatted terminal output for coding-agent interactions.
/// </summary>
public sealed class OutputFormatter
{
    public void WriteWelcome(string model, SessionInfo? session)
    {
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

        WriteColored(text, ConsoleColor.Gray);
    }

    public void WriteToolStart(string toolName, string args)
    {
        var summary = string.IsNullOrWhiteSpace(args) ? "{}" : args;
        WriteColoredLine($"🔧 {toolName}: {summary}", ConsoleColor.Yellow);
    }

    public void WriteToolEnd(string toolName, bool success)
    {
        var icon = success ? "✅" : "❌";
        var color = success ? ConsoleColor.Green : ConsoleColor.Red;
        WriteColoredLine($"{icon} {toolName}", color);
    }

    public void WriteError(string message)
    {
        WriteColoredLine(message, ConsoleColor.Red);
    }

    public void WriteTurnSeparator()
    {
        WriteColoredLine(Environment.NewLine + "────────────────────────────────────────", ConsoleColor.DarkGray);
    }

    public void WriteSessionInfo(SessionInfo session)
    {
        WriteColoredLine(
            $"Session: {session.Id} ({session.Name}) | Messages: {session.MessageCount} | Updated: {session.UpdatedAt:yyyy-MM-dd HH:mm:ss}",
            ConsoleColor.DarkGray);
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
