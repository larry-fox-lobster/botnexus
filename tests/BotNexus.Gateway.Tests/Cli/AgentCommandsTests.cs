using FluentAssertions;

namespace BotNexus.Gateway.Tests.Cli;

public sealed class AgentCommandsTests
{
    [Fact]
    public async Task AgentList_WithConfiguredAgents_ReturnsZero()
    {
        await using var fixture = await CliTestFixture.CreateAsync("""
            {
              "agents": {
                "assistant": {
                  "provider": "copilot",
                  "model": "gpt-4.1",
                  "enabled": true
                }
              }
            }
            """);

        var result = await fixture.RunCliAsync("agent", "list");

        result.ExitCode.Should().Be(0);
        result.StdOut.Should().Contain("assistant");
        result.StdOut.Should().Contain("provider=copilot");
    }

    [Fact]
    public async Task AgentAdd_AddsAgentAndReturnsZero()
    {
        await using var fixture = await CliTestFixture.CreateAsync("""{"agents":{}}""");

        var result = await fixture.RunCliAsync("agent", "add", "reviewer", "--provider", "copilot", "--model", "gpt-5", "--enabled", "true");
        var config = await fixture.LoadConfigAsync();

        result.ExitCode.Should().Be(0);
        config.Agents.Should().ContainKey("reviewer");
        config.Agents!["reviewer"].Model.Should().Be("gpt-5");
    }

    [Fact]
    public async Task AgentAdd_WhenAgentExists_ReturnsOne()
    {
        await using var fixture = await CliTestFixture.CreateAsync("""
            {
              "agents": {
                "assistant": {
                  "provider": "copilot",
                  "model": "gpt-4.1"
                }
              }
            }
            """);

        var result = await fixture.RunCliAsync("agent", "add", "assistant");

        result.ExitCode.Should().Be(1);
        result.CombinedOutput.Should().Contain("already exists");
    }

    [Fact]
    public async Task AgentRemove_RemovesAgentAndReturnsZero()
    {
        await using var fixture = await CliTestFixture.CreateAsync("""
            {
              "agents": {
                "assistant": {
                  "provider": "copilot",
                  "model": "gpt-4.1"
                },
                "reviewer": {
                  "provider": "copilot",
                  "model": "gpt-5"
                }
              }
            }
            """);

        var result = await fixture.RunCliAsync("agent", "remove", "reviewer");
        var config = await fixture.LoadConfigAsync();

        result.ExitCode.Should().Be(0);
        config.Agents.Should().NotContainKey("reviewer");
    }

    [Fact]
    public async Task AgentRemove_WithMissingConfig_ReturnsOne()
    {
        await using var fixture = await CliTestFixture.CreateAsync();

        var result = await fixture.RunCliAsync("agent", "remove", "assistant");

        result.ExitCode.Should().Be(1);
        result.CombinedOutput.Should().Contain("config file not found");
    }
}
