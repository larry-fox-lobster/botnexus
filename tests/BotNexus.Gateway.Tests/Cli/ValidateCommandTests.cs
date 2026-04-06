using FluentAssertions;

namespace BotNexus.Gateway.Tests.Cli;

public sealed class ValidateCommandTests
{
    [Fact]
    public async Task Validate_WithValidConfig_ReturnsZero()
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

        var result = await fixture.RunCliAsync("validate");

        result.ExitCode.Should().Be(0);
        result.StdOut.Should().Contain("Result: VALID");
    }

    [Fact]
    public async Task Validate_WithInvalidConfig_ReturnsOne()
    {
        await using var fixture = await CliTestFixture.CreateAsync("""
            {
              "agents": {
                "assistant": {
                  "provider": "",
                  "model": ""
                }
              }
            }
            """);

        var result = await fixture.RunCliAsync("validate");

        result.ExitCode.Should().Be(1);
        result.CombinedOutput.Should().Contain("agents.assistant.provider");
        result.CombinedOutput.Should().Contain("agents.assistant.model");
    }

    [Fact]
    public async Task Validate_WithMissingConfig_ReturnsOne()
    {
        await using var fixture = await CliTestFixture.CreateAsync();

        var result = await fixture.RunCliAsync("validate");

        result.ExitCode.Should().Be(1);
        result.CombinedOutput.Should().Contain("Config file not found");
    }
}
