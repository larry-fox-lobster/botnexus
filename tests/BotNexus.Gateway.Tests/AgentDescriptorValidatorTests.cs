using BotNexus.Gateway.Abstractions.Models;
using BotNexus.Gateway.Agents;
using BotNexus.Gateway.Configuration;
using FluentAssertions;

namespace BotNexus.Gateway.Tests;

public sealed class AgentDescriptorValidatorTests
{
    [Fact]
    public void Validate_WithValidDescriptor_ReturnsNoErrors()
    {
        var descriptor = CreateValidDescriptor();

        var errors = AgentDescriptorValidator.Validate(descriptor);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithoutAgentId_ReturnsAgentIdError()
    {
        var descriptor = CreateValidDescriptor() with { AgentId = string.Empty };

        var errors = AgentDescriptorValidator.Validate(descriptor);

        errors.Should().Contain("AgentId is required.");
    }

    [Fact]
    public void Validate_WithoutDisplayName_ReturnsDisplayNameError()
    {
        var descriptor = CreateValidDescriptor() with { DisplayName = string.Empty };

        var errors = AgentDescriptorValidator.Validate(descriptor);

        errors.Should().Contain("DisplayName is required.");
    }

    [Fact]
    public void Validate_WithoutModelId_ReturnsModelIdError()
    {
        var descriptor = CreateValidDescriptor() with { ModelId = string.Empty };

        var errors = AgentDescriptorValidator.Validate(descriptor);

        errors.Should().Contain("ModelId is required.");
    }

    [Fact]
    public void Validate_WithoutApiProvider_ReturnsApiProviderError()
    {
        var descriptor = CreateValidDescriptor() with { ApiProvider = string.Empty };

        var errors = AgentDescriptorValidator.Validate(descriptor);

        errors.Should().Contain("ApiProvider is required.");
    }

    [Fact]
    public void Validate_WithSystemPromptAndSystemPromptFile_ReturnsMutualExclusionError()
    {
        var descriptor = CreateValidDescriptor() with
        {
            SystemPrompt = "Prompt",
            SystemPromptFile = "prompt.txt"
        };

        var errors = AgentDescriptorValidator.Validate(descriptor);

        errors.Should().Contain("SystemPrompt and SystemPromptFile are mutually exclusive.");
    }

    [Fact]
    public void Validate_WithoutSystemPromptAndSystemPromptFile_ReturnsNoPromptErrors()
    {
        var descriptor = CreateValidDescriptor() with
        {
            SystemPrompt = null,
            SystemPromptFile = null
        };

        var errors = AgentDescriptorValidator.Validate(descriptor);

        errors.Should().NotContain(error => error.Contains("SystemPrompt", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WithNegativeMaxConcurrentSessions_ReturnsError()
    {
        var descriptor = CreateValidDescriptor() with { MaxConcurrentSessions = -1 };

        var errors = AgentDescriptorValidator.Validate(descriptor);

        errors.Should().Contain("MaxConcurrentSessions must be >= 0.");
    }

    [Fact]
    public void Validate_WithZeroMaxConcurrentSessions_ReturnsNoErrors()
    {
        var descriptor = CreateValidDescriptor() with { MaxConcurrentSessions = 0 };

        var errors = AgentDescriptorValidator.Validate(descriptor);

        errors.Should().BeEmpty();
    }

    private static AgentDescriptor CreateValidDescriptor()
        => new()
        {
            AgentId = "agent-a",
            DisplayName = "Agent A",
            ModelId = "model",
            ApiProvider = "provider",
            SystemPrompt = "Prompt",
            MaxConcurrentSessions = 1
        };
}

public sealed class GatewayOptionsTests
{
    [Fact]
    public void DefaultAgentId_CanBeNull()
    {
        var options = new GatewayOptions();

        options.DefaultAgentId.Should().BeNull();
    }

    [Fact]
    public void DefaultAgentId_CanBeAssigned()
    {
        var options = new GatewayOptions
        {
            DefaultAgentId = "agent-a"
        };

        options.DefaultAgentId.Should().Be("agent-a");
    }
}
