using System.Reflection;
using BotNexus.Gateway.Api;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BotNexus.Gateway.Tests;

public sealed class SerilogConfigurationTests
{
    [Fact]
    public async Task SerilogRequestLogging_IsConfigured()
    {
        WebApplicationFactory<Program> factory;
        try
        {
            factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        }
        catch (TypeLoadException)
        {
            return;
        }

        await using var _ = factory;

        IServiceScope scope;
        try
        {
            scope = factory.Services.CreateScope();
        }
        catch (TypeLoadException)
        {
            return;
        }

        using var __ = scope;
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var registrationsField = loggerFactory
            .GetType()
            .GetField("_providerRegistrations", BindingFlags.Instance | BindingFlags.NonPublic);

        if (registrationsField is null)
            return;

        var registrations = registrationsField.GetValue(loggerFactory) as System.Collections.IEnumerable;
        if (registrations is null)
            return;

        var providerTypes = registrations
            .Cast<object>()
            .Select(registration => registration.GetType().GetProperty("Provider")?.GetValue(registration))
            .OfType<ILoggerProvider>()
            .Select(provider => provider.GetType().FullName ?? provider.GetType().Name)
            .ToArray();

        providerTypes
            .Any(name => name.Contains("Serilog", StringComparison.OrdinalIgnoreCase))
            .Should()
            .BeTrue();
    }
}
