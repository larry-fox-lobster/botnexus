using BotNexus.Gateway.Abstractions.Models;
using BotNexus.Gateway.Api.Controllers;
using BotNexus.Gateway.Sessions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace BotNexus.Gateway.Tests;

public sealed class SessionsControllerTests
{
    [Fact]
    public async Task List_WithExistingSessions_ReturnsSessions()
    {
        var store = new InMemorySessionStore();
        await store.GetOrCreateAsync("s1", "agent-a");
        var controller = new SessionsController(store);

        var result = await controller.List(null, CancellationToken.None);

        ((result.Result as OkObjectResult)?.Value as IReadOnlyList<GatewaySession>).Should().HaveCount(1);
    }

    [Fact]
    public async Task Get_WithUnknownSession_ReturnsNotFound()
    {
        var controller = new SessionsController(new InMemorySessionStore());

        var result = await controller.Get("missing", CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Delete_WithAnySession_ReturnsNoContent()
    {
        var store = new InMemorySessionStore();
        await store.GetOrCreateAsync("s1", "agent-a");
        var controller = new SessionsController(store);

        var result = await controller.Delete("s1", CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task GetHistory_WithDefaults_ReturnsPagedHistoryAndTotalCount()
    {
        var store = new InMemorySessionStore();
        var session = await store.GetOrCreateAsync("s1", "agent-a");
        for (var i = 0; i < 60; i++)
            session.AddEntry(new SessionEntry { Role = "user", Content = $"m-{i}" });

        var controller = new SessionsController(store);

        var result = await controller.GetHistory("s1", cancellationToken: CancellationToken.None);

        var response = (result.Result as OkObjectResult)?.Value as SessionHistoryResponse;
        response.Should().NotBeNull();
        response!.Offset.Should().Be(0);
        response.Limit.Should().Be(50);
        response.TotalCount.Should().Be(60);
        response.Entries.Should().HaveCount(50);
        response.Entries[0].Content.Should().Be("m-0");
    }

    [Fact]
    public async Task GetHistory_WithOffsetAndLargeLimit_AppliesPaginationAndLimitCap()
    {
        var store = new InMemorySessionStore();
        var session = await store.GetOrCreateAsync("s1", "agent-a");
        for (var i = 0; i < 260; i++)
            session.AddEntry(new SessionEntry { Role = "user", Content = $"m-{i}" });

        var controller = new SessionsController(store);

        var result = await controller.GetHistory("s1", offset: 10, limit: 500, cancellationToken: CancellationToken.None);

        var response = (result.Result as OkObjectResult)?.Value as SessionHistoryResponse;
        response.Should().NotBeNull();
        response!.Offset.Should().Be(10);
        response.Limit.Should().Be(200);
        response.TotalCount.Should().Be(260);
        response.Entries.Should().HaveCount(200);
        response.Entries[0].Content.Should().Be("m-10");
        response.Entries[^1].Content.Should().Be("m-209");
    }
}
