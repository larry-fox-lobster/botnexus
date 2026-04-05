using BotNexus.Gateway.Abstractions.Models;
using BotNexus.Gateway.Api.Controllers;
using BotNexus.Gateway.Sessions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace BotNexus.Gateway.Tests;

public class SessionsControllerTests
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
}
