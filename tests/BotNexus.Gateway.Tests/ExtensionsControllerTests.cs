using BotNexus.Gateway.Abstractions.Extensions;
using BotNexus.Gateway.Api.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace BotNexus.Gateway.Tests;

public sealed class ExtensionsControllerTests
{
    [Fact]
    public void List_WithLoadedExtensions_ReturnsFlattenedTypeRows()
    {
        var loader = new Mock<IExtensionLoader>();
        loader.Setup(value => value.GetLoaded()).Returns(
        [
            new LoadedExtension
            {
                ExtensionId = "ext-a",
                Name = "Extension A",
                Version = "1.2.3",
                DirectoryPath = "Q:\\extensions\\ext-a",
                EntryAssemblyPath = "Q:\\extensions\\ext-a\\ExtensionA.dll",
                ExtensionTypes = ["channel", "router"],
                LoadedAtUtc = DateTimeOffset.UtcNow,
                RegisteredServices = []
            }
        ]);

        var controller = new ExtensionsController(loader.Object);

        var result = controller.List();

        var payload = (result.Result as OkObjectResult)?.Value as IReadOnlyList<ExtensionResponse>;
        payload.Should().NotBeNull();
        payload!.Should().HaveCount(2);
        payload.Should().BeEquivalentTo(
        [
            new ExtensionResponse("Extension A", "1.2.3", "channel", "Q:\\extensions\\ext-a\\ExtensionA.dll"),
            new ExtensionResponse("Extension A", "1.2.3", "router", "Q:\\extensions\\ext-a\\ExtensionA.dll")
        ]);
    }
}
