using System.Runtime.InteropServices;
using System.Text;

namespace BotNexus.CodingAgent;

public sealed record ToolPromptContribution(
    string Name,
    string? Snippet = null,
    IReadOnlyList<string>? Guidelines = null);

public sealed record PromptContextFile(
    string Path,
    string Content);

public sealed record SystemPromptContext(
    string WorkingDirectory,
    string? GitBranch,
    string? GitStatus,
    string PackageManager,
    IReadOnlyList<string> ToolNames,
    IReadOnlyList<string> Skills,
    string? CustomInstructions,
    string? CustomPrompt = null,
    string? AppendSystemPrompt = null,
    IReadOnlyList<ToolPromptContribution>? ToolContributions = null,
    IReadOnlyList<PromptContextFile>? ContextFiles = null,
    DateTimeOffset? CurrentDateTime = null);

public sealed class SystemPromptBuilder
{
    public string Build(SystemPromptContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var normalizedWorkingDirectory = context.WorkingDirectory.Replace('\\', '/');
        var timestamp = (context.CurrentDateTime ?? DateTimeOffset.Now).ToString("O");
        var builder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(context.CustomPrompt))
        {
            builder.Append(context.CustomPrompt.Trim());
        }
        else
        {
            var sections = new List<(string? Title, string Content)>
            {
                (null, "You are a coding assistant with access to tools for reading, writing, and editing files, and executing shell commands."),
                ("Environment", BuildEnvironmentSection(context)),
                ("Available Tools", BuildToolsSection(context)),
                ("Tool Guidelines", BuildToolGuidelinesSection(context))
            };

            builder.Append(BuildDocument(sections));
        }

        if (!string.IsNullOrWhiteSpace(context.AppendSystemPrompt))
        {
            builder.AppendLine();
            builder.AppendLine();
            builder.Append(context.AppendSystemPrompt.Trim());
        }

        var contextFilesSection = BuildContextFilesSection(context.ContextFiles ?? []);
        if (!string.IsNullOrWhiteSpace(contextFilesSection))
        {
            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine("## Project Context");
            builder.Append(contextFilesSection);
        }

        var hasReadTool = context.ToolNames.Any(static name => string.Equals(name, "read", StringComparison.OrdinalIgnoreCase));
        var skillsSection = hasReadTool ? BuildSkillsSection(context.Skills) : string.Empty;
        if (!string.IsNullOrWhiteSpace(skillsSection))
        {
            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine("## Skills");
            builder.Append(skillsSection);
        }

        if (!string.IsNullOrWhiteSpace(context.CustomInstructions))
        {
            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine("## Custom Instructions");
            builder.Append(context.CustomInstructions.Trim());
        }

        builder.AppendLine();
        builder.Append($"Current date/time: {timestamp}");
        builder.AppendLine();
        builder.Append($"Current working directory: {normalizedWorkingDirectory}");

        return builder.ToString().TrimEnd();
    }

    private static string BuildDocument(IEnumerable<(string? Title, string Content)> sections)
    {
        var builder = new StringBuilder();
        var first = true;
        foreach (var (title, content) in sections)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            if (!first)
            {
                builder.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(title))
            {
                builder.AppendLine($"## {title}");
            }

            builder.AppendLine(content.TrimEnd());
            first = false;
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildEnvironmentSection(SystemPromptContext context)
    {
        var lines = new[]
        {
            $"- OS: {RuntimeInformation.OSDescription}",
            $"- Working directory: {context.WorkingDirectory.Replace('\\', '/')}",
            $"- Git branch: {context.GitBranch ?? "N/A"}",
            $"- Git status: {context.GitStatus ?? "N/A"}",
            $"- Package manager: {context.PackageManager}"
        };

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildToolsSection(SystemPromptContext context)
    {
        var contributions = context.ToolContributions ?? context.ToolNames.Select(name => new ToolPromptContribution(name)).ToList();
        if (contributions.Count == 0)
        {
            return "none";
        }

        return string.Join(
            Environment.NewLine,
            contributions.Select(contribution =>
                $"- {contribution.Name}: {contribution.Snippet ?? "Available for coding workflow tasks."}"));
    }

    private static string BuildToolGuidelinesSection(SystemPromptContext context)
    {
        var guidelines = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Use tools proactively.",
            "Read files before editing.",
            "Make precise edits.",
            "Verify changes compile.",
            "Be concise in your responses.",
            "Show file paths clearly when working with files."
        };

        var hasBash = context.ToolNames.Any(static name => string.Equals(name, "bash", StringComparison.OrdinalIgnoreCase));
        var hasGrep = context.ToolNames.Any(static name => string.Equals(name, "grep", StringComparison.OrdinalIgnoreCase));
        var hasFind = context.ToolNames.Any(static name => string.Equals(name, "find", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "glob", StringComparison.OrdinalIgnoreCase));
        var hasListDirectory = context.ToolNames.Any(static name =>
            string.Equals(name, "ls", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "list_directory", StringComparison.OrdinalIgnoreCase));

        if (hasBash && !hasGrep && !hasFind && !hasListDirectory)
        {
            guidelines.Add("Use bash for file operations like ls, rg, find.");
        }
        else if (hasBash && (hasGrep || hasFind || hasListDirectory))
        {
            guidelines.Add("Prefer grep/find/ls tools over bash for file exploration (faster, respects .gitignore).");
        }

        foreach (var guideline in (context.ToolContributions ?? []).SelectMany(contribution => contribution.Guidelines ?? []))
        {
            if (!string.IsNullOrWhiteSpace(guideline))
            {
                guidelines.Add(guideline.Trim());
            }
        }

        return string.Join(Environment.NewLine, guidelines.Select(guideline => $"- {guideline}"));
    }

    private static string BuildContextFilesSection(IReadOnlyList<PromptContextFile> contextFiles)
    {
        if (contextFiles.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var file in contextFiles)
        {
            if (string.IsNullOrWhiteSpace(file.Content))
            {
                continue;
            }

            builder.AppendLine($"### {file.Path}");
            builder.AppendLine(file.Content.Trim());
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildSkillsSection(IReadOnlyList<string> skills)
    {
        if (skills.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var skill in skills)
        {
            var parsed = ParseSkill(skill);
            builder.AppendLine("---");
            builder.AppendLine($"name: {parsed.Name}");
            if (!string.IsNullOrWhiteSpace(parsed.Description))
            {
                builder.AppendLine($"description: {parsed.Description}");
            }

            builder.AppendLine("---");
            builder.AppendLine(parsed.Content.Trim());
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static (string Name, string? Description, string Content) ParseSkill(string raw)
    {
        var content = raw.Trim();
        if (!content.StartsWith("---", StringComparison.Ordinal))
        {
            return ("skill", null, content);
        }

        var lines = content.Split(["\r\n", "\n"], StringSplitOptions.None);
        var closingIndex = Array.FindIndex(lines, 1, line => line.Trim().Equals("---", StringComparison.Ordinal));
        if (closingIndex < 0)
        {
            return ("skill", null, content);
        }

        string? name = null;
        string? description = null;
        for (var i = 1; i < closingIndex; i++)
        {
            var line = lines[i];
            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim().Trim('\'', '"');
            if (key.Equals("name", StringComparison.OrdinalIgnoreCase))
            {
                name = value;
            }
            else if (key.Equals("description", StringComparison.OrdinalIgnoreCase))
            {
                description = value;
            }
        }

        var body = string.Join(Environment.NewLine, lines.Skip(closingIndex + 1)).Trim();
        return (name ?? "skill", description, body);
    }
}
