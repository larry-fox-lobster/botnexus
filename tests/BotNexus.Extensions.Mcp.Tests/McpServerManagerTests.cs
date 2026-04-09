using FluentAssertions;

namespace BotNexus.Extensions.Mcp.Tests;

public class McpServerManagerTests
{
    [Fact]
    public async Task StartServersAsync_SkipsServers_WithNoCommand()
    {
        var manager = new McpServerManager();
        var config = new McpExtensionConfig
        {
            Servers = new Dictionary<string, McpServerConfig>
            {
                ["empty"] = new McpServerConfig { Command = null },
            },
        };

        var tools = await manager.StartServersAsync(config);
        tools.Should().BeEmpty();

        await manager.DisposeAsync();
    }

    [Fact]
    public async Task StopAllAsync_CanBeCalledMultipleTimes()
    {
        var manager = new McpServerManager();
        await manager.StopAllAsync();
        await manager.StopAllAsync();

        // Should not throw
        await manager.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimes()
    {
        var manager = new McpServerManager();
        await manager.DisposeAsync();
        await manager.DisposeAsync();

        // Should not throw
    }

    [Fact]
    public void GetClients_ReturnsEmptyList_Initially()
    {
        var manager = new McpServerManager();
        manager.GetClients().Should().BeEmpty();
    }

    [Fact]
    public async Task StartServersAsync_EmptyConfig_ReturnsEmptyTools()
    {
        var manager = new McpServerManager();
        var config = new McpExtensionConfig { Servers = new Dictionary<string, McpServerConfig>() };

        var tools = await manager.StartServersAsync(config);

        tools.Should().BeEmpty();
        manager.GetClients().Should().BeEmpty();

        await manager.DisposeAsync();
    }

    [Fact]
    public async Task StartServersAsync_SkipsServers_WithEmptyCommand()
    {
        var manager = new McpServerManager();
        var config = new McpExtensionConfig
        {
            Servers = new Dictionary<string, McpServerConfig>
            {
                ["blank"] = new McpServerConfig { Command = "" },
                ["whitespace"] = new McpServerConfig { Command = "   " },
            },
        };

        var tools = await manager.StartServersAsync(config);

        tools.Should().BeEmpty();
        manager.GetClients().Should().BeEmpty();

        await manager.DisposeAsync();
    }

    [Fact]
    public async Task StopAllAsync_WhenNoServersRunning_DoesNotThrow()
    {
        var manager = new McpServerManager();

        // No servers started, StopAll should be a no-op
        await manager.StopAllAsync();
        manager.GetClients().Should().BeEmpty();

        await manager.DisposeAsync();
    }

    [Fact]
    public async Task GetClients_ThrowsObjectDisposed_AfterDispose()
    {
        var manager = new McpServerManager();
        await manager.DisposeAsync();

        var act = () => manager.GetClients();
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task StartServersAsync_ThrowsObjectDisposed_AfterDispose()
    {
        var manager = new McpServerManager();
        await manager.DisposeAsync();

        var config = new McpExtensionConfig();
        var act = () => manager.StartServersAsync(config);
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }
}
