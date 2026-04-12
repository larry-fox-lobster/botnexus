using System.Diagnostics;

namespace BotNexus.AgentCore.Diagnostics;

/// <summary>
/// Represents agent diagnostics.
/// </summary>
public static class AgentDiagnostics
{
    public const string SourceName = "BotNexus.Agents";

    public static readonly ActivitySource Source = new(SourceName);
}
