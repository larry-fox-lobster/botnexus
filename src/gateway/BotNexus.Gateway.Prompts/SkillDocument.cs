namespace BotNexus.Gateway.Prompts;

/// <summary>
/// Represents skill document.
/// </summary>
public sealed record SkillDocument(string Name, string? Description, string Content);