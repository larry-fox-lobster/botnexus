using BotNexus.Core.Abstractions;
using BotNexus.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BotNexus.Providers.OpenAI;

/// <summary>Registers OpenAI provider services when loaded as an extension.</summary>
public sealed class OpenAiExtensionRegistrar : IExtensionRegistrar
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ILlmProvider>(sp =>
        {
            var botConfig = sp.GetRequiredService<IOptions<BotNexusConfig>>().Value;
            var providerConfig = configuration.Get<ProviderConfig>() ?? new ProviderConfig();
            var logger = sp.GetRequiredService<ILogger<OpenAiProvider>>();

            return new OpenAiProvider(
                apiKey: providerConfig.ApiKey,
                model: providerConfig.DefaultModel ?? botConfig.Agents.Model ?? "gpt-4o",
                apiBase: providerConfig.ApiBase,
                logger: logger,
                maxRetries: providerConfig.MaxRetries);
        });
    }
}
