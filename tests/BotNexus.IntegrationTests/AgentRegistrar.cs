using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BotNexus.IntegrationTests;

public static class AgentRegistrar
{
    public static async Task RegisterAsync(
        HttpClient client,
        AgentDefinition agent,
        CancellationToken ct)
    {
        var descriptor = new
        {
            agentId = agent.Id,
            displayName = agent.DisplayName,
            modelId = agent.Model,
            apiProvider = agent.Provider,
            isolationStrategy = "in-process",
            systemPrompt = agent.SystemPrompt
        };

        var response = await client.PostAsJsonAsync("/api/agents", descriptor, ct);
        if (response.StatusCode is not (HttpStatusCode.Created or HttpStatusCode.Conflict))
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new Exception($"Failed to register agent '{agent.Id}': {response.StatusCode} — {body}");
        }

        Console.WriteLine($"    ✓ Registered agent: {agent.Id}");
    }
}