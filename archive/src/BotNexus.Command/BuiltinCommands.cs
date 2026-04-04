using System.Text;
using BotNexus.Core.Abstractions;
using BotNexus.Core.Models;
using BotNexus.Providers.Base;

namespace BotNexus.Command;

/// <summary>Registers built-in commands (/help, /reset, /status, /models).</summary>
public static class BuiltinCommands
{
    /// <summary>Registers all built-in commands with the router.</summary>
    public static void Register(
        ICommandRouter router,
        ISessionManager sessionManager,
        ProviderRegistry providerRegistry,
        IHeartbeatService? heartbeatService = null)
    {
        router.Register("/help", async (msg, ct) =>
        {
            await Task.CompletedTask;
            return """
                BotNexus Commands:
                /help    - Show this help message
                /reset   - Reset the current conversation session
                /status  - Show system status
                /models  - List all available models by provider
                """;
        }, priority: 100);

        router.Register("/reset", async (msg, ct) =>
        {
            await sessionManager.ResetAsync(msg.SessionKey, ct).ConfigureAwait(false);
            return "✅ Session reset. Starting fresh!";
        }, priority: 100);

        router.Register("/status", async (msg, ct) =>
        {
            await Task.CompletedTask;
            var heartbeat = heartbeatService is not null
                ? $"Last heartbeat: {heartbeatService.LastBeat?.ToString("u") ?? "never"}"
                : "Heartbeat: disabled";
            return $"✅ BotNexus is running\n{heartbeat}";
        }, priority: 100);

        // Helper function for /models and /model commands
        async Task<string> ListModelsAsync(CancellationToken ct)
        {
            var names = providerRegistry.GetProviderNames();
            if (names.Count == 0)
                return "No providers registered.";

            var sb = new StringBuilder("Available models:\n");

            foreach (var name in names)
            {
                var provider = providerRegistry.Get(name);
                if (provider is null)
                    continue;

                sb.AppendLine();
                sb.Append(name).AppendLine(":");

                IReadOnlyList<string> models;
                try
                {
                    models = await provider.GetAvailableModelsAsync(ct).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    models = new[] { provider.DefaultModel };
                }

                foreach (var model in models)
                {
                    var isDefault = model.Equals(provider.DefaultModel, StringComparison.OrdinalIgnoreCase);
                    sb.Append("  ").Append(model);
                    if (isDefault)
                        sb.Append(" (default)");
                    sb.AppendLine();
                }
            }

            return sb.ToString().TrimEnd();
        }

        router.Register("/models", async (msg, ct) => await ListModelsAsync(ct), priority: 100);
        router.Register("/model", async (msg, ct) => await ListModelsAsync(ct), priority: 100);
    }
}
