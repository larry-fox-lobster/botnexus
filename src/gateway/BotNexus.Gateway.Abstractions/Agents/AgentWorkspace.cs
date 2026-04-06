namespace BotNexus.Gateway.Abstractions.Agents;

public sealed record AgentWorkspace(
    string AgentName,
    string? Soul,
    string? Identity,
    string? User,
    string? Memory);
