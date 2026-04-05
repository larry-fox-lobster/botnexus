using BotNexus.CodingAgent;
using FluentAssertions;

namespace BotNexus.CodingAgent.Tests;

public sealed class SystemPromptBuilderTests
{
    private readonly SystemPromptBuilder _builder = new();

    [Fact]
    public void Build_IncludesRoleToolsAndEnvironmentSections()
    {
        var context = new SystemPromptContext(
            WorkingDirectory: @"C:\repo",
            GitBranch: "main",
            GitStatus: "clean",
            PackageManager: "npm",
            ToolNames: ["read", "write", "bash"],
            Skills: [],
            CustomInstructions: null,
            CurrentDateTime: new DateTimeOffset(2026, 4, 6, 10, 30, 0, TimeSpan.Zero),
            ToolContributions:
            [
                new ToolPromptContribution("read", "Read files with line numbers.", ["Prefer read before edit."]),
                new ToolPromptContribution("write", "Write file content."),
                new ToolPromptContribution("bash", "Execute shell commands.")
            ]);

        var prompt = _builder.Build(context);

        prompt.Should().Contain("You are a coding assistant");
        prompt.Should().Contain("## Environment");
        prompt.Should().Contain("- Working directory: C:/repo");
        prompt.Should().Contain("## Available Tools");
        prompt.Should().Contain("- read: Read files with line numbers.");
        prompt.Should().Contain("## Tool Guidelines");
        prompt.Should().Contain("Prefer read before edit.");
        prompt.Should().Contain("Current date/time: 2026-04-06T10:30:00.0000000+00:00");
        prompt.Should().Contain("Current working directory: C:/repo");
    }

    [Fact]
    public void Build_WithSkillsAndCustomInstructions_IncludesBothSections()
    {
        var context = new SystemPromptContext(
            WorkingDirectory: @"C:\repo",
            GitBranch: null,
            GitStatus: null,
            PackageManager: "dotnet",
            ToolNames: ["read"],
            Skills:
            [
                """
                ---
                name: read
                description: skill a description
                ---
                Skill A
                """,
                "Skill B"
            ],
            CustomInstructions: "Use concise responses.",
            CurrentDateTime: new DateTimeOffset(2026, 4, 6, 10, 30, 0, TimeSpan.Zero));

        var prompt = _builder.Build(context);

        prompt.Should().Contain("## Skills");
        prompt.Should().Contain("name: read");
        prompt.Should().Contain("description: skill a description");
        prompt.Should().Contain("Skill A");
        prompt.Should().Contain("Skill B");
        prompt.Should().Contain("## Custom Instructions");
        prompt.Should().Contain("Use concise responses.");
    }

    [Fact]
    public void Build_WithContextFiles_IncludesProjectContextSection()
    {
        var context = new SystemPromptContext(
            WorkingDirectory: @"C:\repo",
            GitBranch: "main",
            GitStatus: "clean",
            PackageManager: "dotnet",
            ToolNames: ["read"],
            Skills: [],
            CustomInstructions: null,
            CurrentDateTime: new DateTimeOffset(2026, 4, 6, 10, 30, 0, TimeSpan.Zero),
            ContextFiles:
            [
                new PromptContextFile(".botnexus-agent/context/runtime.md", "Runtime details")
            ]);

        var prompt = _builder.Build(context);

        prompt.Should().Contain("## Project Context");
        prompt.Should().Contain("### .botnexus-agent/context/runtime.md");
        prompt.Should().Contain("Runtime details");
    }

    [Fact]
    public void Build_WithEmptyOptionalSections_OmitsSectionHeadings()
    {
        var context = new SystemPromptContext(
            WorkingDirectory: @"C:\repo",
            GitBranch: "main",
            GitStatus: "clean",
            PackageManager: "dotnet",
            ToolNames: ["read"],
            Skills: [],
            CustomInstructions: "   ",
            CurrentDateTime: new DateTimeOffset(2026, 4, 6, 10, 30, 0, TimeSpan.Zero),
            ToolContributions: [new ToolPromptContribution("read", null, ["  "])],
            ContextFiles: [new PromptContextFile("context.md", "   ")]);

        var prompt = _builder.Build(context);

        prompt.Should().NotContain("## Skills");
    }

    [Fact]
    public void Build_WithCustomPrompt_ReplacesBaseAndAppendsConfiguredText()
    {
        var context = new SystemPromptContext(
            WorkingDirectory: @"C:\repo",
            GitBranch: null,
            GitStatus: null,
            PackageManager: "dotnet",
            ToolNames: ["read"],
            Skills: [],
            CustomInstructions: null,
            CustomPrompt: "Custom base prompt",
            AppendSystemPrompt: "Appended prompt",
            CurrentDateTime: new DateTimeOffset(2026, 4, 6, 10, 30, 0, TimeSpan.Zero));

        var prompt = _builder.Build(context);

        prompt.Should().StartWith("Custom base prompt");
        prompt.Should().Contain("Appended prompt");
        prompt.Should().NotContain("## Environment");
    }

    [Fact]
    public void Build_WithOnlyBashTool_AddsBashFileOperationsGuideline()
    {
        var context = new SystemPromptContext(
            WorkingDirectory: @"C:\repo",
            GitBranch: "main",
            GitStatus: "clean",
            PackageManager: "dotnet",
            ToolNames: ["bash"],
            Skills: [],
            CustomInstructions: null,
            CurrentDateTime: new DateTimeOffset(2026, 4, 6, 10, 30, 0, TimeSpan.Zero));

        var prompt = _builder.Build(context);

        prompt.Should().Contain("Use bash for file operations like ls, rg, find.");
    }

    [Fact]
    public void Build_WithBashAndDiscoveryTools_PrefersDedicatedToolsGuideline()
    {
        var context = new SystemPromptContext(
            WorkingDirectory: @"C:\repo",
            GitBranch: "main",
            GitStatus: "clean",
            PackageManager: "dotnet",
            ToolNames: ["bash", "grep", "glob"],
            Skills: [],
            CustomInstructions: null,
            CurrentDateTime: new DateTimeOffset(2026, 4, 6, 10, 30, 0, TimeSpan.Zero));

        var prompt = _builder.Build(context);

        prompt.Should().Contain("Prefer grep/find/ls tools over bash for file exploration (faster, respects .gitignore).");
    }
}
