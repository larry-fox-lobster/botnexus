using BotNexus.Core.Abstractions;
using BotNexus.Diagnostics.Checkups.Configuration;
using BotNexus.Diagnostics.Checkups.Connectivity;
using BotNexus.Diagnostics.Checkups.Extensions;
using BotNexus.Diagnostics.Checkups.Permissions;
using BotNexus.Diagnostics.Checkups.Resources;
using BotNexus.Diagnostics.Checkups.Security;
using Microsoft.Extensions.DependencyInjection;

namespace BotNexus.Diagnostics;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBotNexusDiagnostics(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(_ => DiagnosticsPaths.FromBotNexusHome());
        services.AddSingleton<CheckupRunner>();

        services.AddSingleton<IHealthCheckup, ConfigValidCheckup>();
        services.AddSingleton<IHealthCheckup, AgentConfigCheckup>();
        services.AddSingleton<IHealthCheckup, ProviderConfigCheckup>();

        services.AddSingleton<IHealthCheckup, ApiKeyStrengthCheckup>();
        services.AddSingleton<IHealthCheckup, TokenPermissionsCheckup>();
        services.AddSingleton<IHealthCheckup, ExtensionSignedCheckup>();

        services.AddSingleton<IHealthCheckup, ProviderReachableCheckup>();
        services.AddSingleton<IHealthCheckup, PortAvailableCheckup>();

        services.AddSingleton<IHealthCheckup, ExtensionsFolderExistsCheckup>();
        services.AddSingleton<IHealthCheckup, ExtensionAssembliesValidCheckup>();

        services.AddSingleton<IHealthCheckup, HomeDirWritableCheckup>();
        services.AddSingleton<IHealthCheckup, LogDirWritableCheckup>();

        services.AddSingleton<IHealthCheckup, DiskSpaceCheckup>();

        return services;
    }
}
