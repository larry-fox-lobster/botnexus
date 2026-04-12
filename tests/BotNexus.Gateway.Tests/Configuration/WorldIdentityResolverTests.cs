using BotNexus.Gateway.Configuration;
using FluentAssertions;

namespace BotNexus.Gateway.Tests.Configuration;

public sealed class WorldIdentityResolverTests
{
    [Fact]
    public void Resolve_WithConfiguredWorld_UsesConfiguredValues()
    {
        var config = new PlatformConfig
        {
            Gateway = new GatewaySettingsConfig
            {
                World = new BotNexus.Domain.WorldIdentity
                {
                    Id = "local-dev",
                    Name = "Local Development",
                    Description = "Local development gateway",
                    Emoji = "🏠"
                }
            }
        };

        var world = WorldIdentityResolver.Resolve(config);

        world.Id.Should().Be("local-dev");
        world.Name.Should().Be("Local Development");
        world.Description.Should().Be("Local development gateway");
        world.Emoji.Should().Be("🏠");
    }

    [Fact]
    public void Resolve_WithoutConfiguredWorld_UsesDefaults()
    {
        var world = WorldIdentityResolver.Resolve(new PlatformConfig());

        world.Id.Should().Be(Environment.MachineName);
        world.Name.Should().Be("BotNexus Gateway");
    }
}
