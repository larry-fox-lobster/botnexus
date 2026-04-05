namespace BotNexus.CodingAgent.Cli;

/// <summary>
/// Parses coding-agent CLI command-line arguments.
/// </summary>
public sealed class CommandParser
{
    public CommandOptions Parse(IReadOnlyList<string> args)
    {
        var positional = new List<string>();
        string? model = null;
        string? provider = null;
        string? resume = null;
        var nonInteractive = false;
        var verbose = false;
        var showHelp = false;

        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--model":
                    model = ReadValue(args, ref index, "--model");
                    break;
                case "--provider":
                    provider = ReadValue(args, ref index, "--provider");
                    break;
                case "--resume":
                    resume = ReadValue(args, ref index, "--resume");
                    break;
                case "--non-interactive":
                    nonInteractive = true;
                    break;
                case "--verbose":
                    verbose = true;
                    break;
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                default:
                    positional.Add(arg);
                    break;
            }
        }

        var prompt = positional.Count == 0
            ? null
            : string.Join(' ', positional).Trim();

        return new CommandOptions(
            Model: model,
            Provider: provider,
            ResumeSessionId: resume,
            NonInteractive: nonInteractive,
            Verbose: verbose,
            ShowHelp: showHelp,
            InitialPrompt: string.IsNullOrWhiteSpace(prompt) ? null : prompt);
    }

    public static string GetUsage()
    {
        return """
               BotNexus Coding Agent

               Usage:
                 botnexus-coding-agent [options] [prompt]

               Options:
                 --model <model>          Override model id
                 --provider <provider>    Override provider id
                 --resume <session-id>    Resume an existing session
                 --non-interactive        Run one prompt and exit
                 --verbose                Enable verbose logs
                 --help                   Show this help
               """;
    }

    private static string ReadValue(IReadOnlyList<string> args, ref int index, string option)
    {
        if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Missing value for {option}.");
        }

        index++;
        return args[index];
    }
}

/// <summary>
/// Parsed command-line options for the coding-agent CLI.
/// </summary>
public sealed record CommandOptions(
    string? Model,
    string? Provider,
    string? ResumeSessionId,
    bool NonInteractive,
    bool Verbose,
    bool ShowHelp,
    string? InitialPrompt);
