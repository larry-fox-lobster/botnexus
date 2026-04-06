using System.Net;
using BotNexus.Gateway.Abstractions.Security;
using BotNexus.Gateway.Api;
using BotNexus.Gateway.Configuration;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace BotNexus.Gateway.Tests;

public sealed class RateLimitingMiddlewareTests
{
    private const string CallerIdentityItemKey = "BotNexus.Gateway.CallerIdentity";

    [Fact]
    public async Task InvokeAsync_WhenWithinLimit_AllowsRequest()
    {
        var nextCalled = false;
        var middleware = new RateLimitingMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            CreateConfig(requestsPerMinute: 2, windowSeconds: 60));

        var context = CreateContext("127.0.0.1");

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task InvokeAsync_WhenOverLimit_Returns429WithRetryAfter()
    {
        var middleware = new RateLimitingMiddleware(
            _ => Task.CompletedTask,
            CreateConfig(requestsPerMinute: 1, windowSeconds: 60));

        var context = CreateContext("127.0.0.1");
        await middleware.InvokeAsync(context);

        var secondContext = CreateContext("127.0.0.1");
        await middleware.InvokeAsync(secondContext);

        secondContext.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        secondContext.Response.Headers.RetryAfter.ToString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task InvokeAsync_ForHealthPath_SkipsRateLimiting()
    {
        var nextCallCount = 0;
        var middleware = new RateLimitingMiddleware(
            _ =>
            {
                nextCallCount++;
                return Task.CompletedTask;
            },
            CreateConfig(requestsPerMinute: 1, windowSeconds: 60));

        var firstContext = new DefaultHttpContext();
        firstContext.Request.Path = "/health";
        await middleware.InvokeAsync(firstContext);

        var secondContext = new DefaultHttpContext();
        secondContext.Request.Path = "/health";
        await middleware.InvokeAsync(secondContext);

        nextCallCount.Should().Be(2);
    }

    [Fact]
    public async Task InvokeAsync_UsesCallerIdentityForRateLimitKey()
    {
        var middleware = new RateLimitingMiddleware(
            _ => Task.CompletedTask,
            CreateConfig(requestsPerMinute: 1, windowSeconds: 60));

        var firstContext = CreateContext("127.0.0.1");
        firstContext.Items[CallerIdentityItemKey] = new GatewayCallerIdentity
        {
            CallerId = "tenant-a",
            Permissions = []
        };
        await middleware.InvokeAsync(firstContext);

        var secondContext = CreateContext("127.0.0.2");
        secondContext.Items[CallerIdentityItemKey] = new GatewayCallerIdentity
        {
            CallerId = "tenant-a",
            Permissions = []
        };
        await middleware.InvokeAsync(secondContext);

        secondContext.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
    }

    [Fact]
    public void GetRateLimit_PrefersGatewayScopedRateLimit()
    {
        var config = new PlatformConfig
        {
            RateLimit = new RateLimitConfig { RequestsPerMinute = 10, WindowSeconds = 30 },
            Gateway = new GatewaySettingsConfig
            {
                RateLimit = new RateLimitConfig { RequestsPerMinute = 75, WindowSeconds = 45 }
            }
        };

        var rateLimit = config.GetRateLimit();

        rateLimit.Should().NotBeNull();
        rateLimit!.RequestsPerMinute.Should().Be(75);
        rateLimit.WindowSeconds.Should().Be(45);
    }

    private static PlatformConfig CreateConfig(int requestsPerMinute, int windowSeconds)
        => new()
        {
            Gateway = new GatewaySettingsConfig
            {
                RateLimit = new RateLimitConfig
                {
                    RequestsPerMinute = requestsPerMinute,
                    WindowSeconds = windowSeconds
                }
            }
        };

    private static DefaultHttpContext CreateContext(string remoteIpAddress)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/chat";
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIpAddress);
        return context;
    }
}
