namespace BotNexus.IntegrationTests;

public class ScenarioDefinition
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public List<AgentDefinition> Agents { get; set; } = [];
    public List<ScenarioStep> Steps { get; set; } = [];
}

public class AgentDefinition
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Model { get; set; } = "gpt-4.1";
    public string Provider { get; set; } = "copilot";
    public string? SystemPrompt { get; set; }
}

public class ScenarioStep
{
    public string Action { get; set; } = "";
    public string? Agent { get; set; }
    public string? Content { get; set; }
    public string? Label { get; set; }
    public string? Type { get; set; }
    public string? FromStep { get; set; }
    public string? Step { get; set; }
    public string? Condition { get; set; }
    public List<string>? Steps { get; set; }
    public List<EventWait>? Events { get; set; }
    public int TimeoutSeconds { get; set; } = 15;
}

public class EventWait
{
    public string Type { get; set; } = "";
    public string? FromStep { get; set; }
}