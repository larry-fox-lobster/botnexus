using BotNexus.Gateway.Api;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace BotNexus.Gateway.Tests;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_UsesIncomingCorrelationId()
    {
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = "incoming-id-123";

        await middleware.InvokeAsync(context);

        context.Response.Headers["X-Correlation-Id"].ToString().Should().Be("incoming-id-123");
        context.Items["CorrelationId"].Should().Be("incoming-id-123");
    }

    [Fact]
    public async Task InvokeAsync_GeneratesCorrelationId_WhenMissing()
    {
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        context.Response.Headers["X-Correlation-Id"].ToString().Should().NotBeNullOrWhiteSpace();
        context.Items["CorrelationId"].Should().Be(context.Response.Headers["X-Correlation-Id"].ToString());
    }
}
