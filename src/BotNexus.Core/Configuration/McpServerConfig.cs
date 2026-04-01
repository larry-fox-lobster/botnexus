namespace BotNexus.Core.Configuration;

/// <summary>MCP (Model Context Protocol) server configuration.</summary>
public class McpServerConfig
{
    public string Name { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public List<string> Args { get; set; } = [];
    public Dictionary<string, string> Env { get; set; } = [];
}
