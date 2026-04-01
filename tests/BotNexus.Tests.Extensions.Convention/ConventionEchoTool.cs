using BotNexus.Core.Abstractions;
using BotNexus.Core.Models;
using Microsoft.Extensions.Configuration;

namespace BotNexus.Tests.Extensions.Convention;

public sealed class ConventionEchoTool(IConfiguration configuration) : ITool
{
    public ToolDefinition Definition { get; } = new(
        "convention_echo",
        "Echoes configured values for testing",
        new Dictionary<string, ToolParameterSchema>());

    public Task<string> ExecuteAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var message = configuration["Message"] ?? "unset";
        return Task.FromResult($"convention:{message}");
    }
}
