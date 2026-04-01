using BotNexus.Core.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BotNexus.Tests.Extensions.Registrar;

public sealed class RegistrarExtensionRegistrar : IExtensionRegistrar
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        var message = configuration["Message"] ?? "unset";
        services.AddSingleton<ITool>(_ => new RegistrarEchoTool(message));
    }
}
