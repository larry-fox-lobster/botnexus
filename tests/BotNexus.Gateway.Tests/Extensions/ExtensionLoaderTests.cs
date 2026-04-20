using System.Runtime.Loader;
using System.Text.Json;
using BotNexus.Gateway.Abstractions.Channels;
using BotNexus.Gateway.Abstractions.Extensions;
using BotNexus.Gateway.Abstractions.Hooks;
using BotNexus.Gateway.Extensions;
using BotNexus.Gateway.Hooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions;

namespace BotNexus.Gateway.Tests.Extensions;

public sealed class ExtensionLoaderTests : IDisposable
{
    private readonly string _rootPath;

    public ExtensionLoaderTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "botnexus-extension-loader-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public async Task DiscoverAsync_ReturnsValidExtensions_AndSkipsMissingOrInvalidManifests()
    {
        var valid = Path.Combine(_rootPath, "valid");
        Directory.CreateDirectory(valid);
        await File.WriteAllTextAsync(Path.Combine(valid, "botnexus-extension.json"), JsonSerializer.Serialize(new ExtensionManifest
        {
            Id = "valid-extension",
            Name = "Valid",
            Version = "1.0.0",
            EntryAssembly = "entry.dll",
            ExtensionTypes = ["channel"]
        }));
        await File.WriteAllTextAsync(Path.Combine(valid, "entry.dll"), "placeholder");

        var missingManifest = Path.Combine(_rootPath, "missing-manifest");
        Directory.CreateDirectory(missingManifest);

        var invalid = Path.Combine(_rootPath, "invalid");
        Directory.CreateDirectory(invalid);
        await File.WriteAllTextAsync(Path.Combine(invalid, "botnexus-extension.json"), """{"id":"","name":"Broken","version":"1.0.0"}""");

        var loader = CreateLoader(new ServiceCollection());

        var discovered = await loader.DiscoverAsync(_rootPath);

        discovered.ShouldHaveSingleItem();
        discovered[0].Manifest.Id.ShouldBe("valid-extension");
    }

    [Fact]
    public async Task DiscoverAsync_AllowsMediaHandlerExtensionType()
    {
        var mediaHandler = Path.Combine(_rootPath, "media-handler");
        Directory.CreateDirectory(mediaHandler);
        await File.WriteAllTextAsync(Path.Combine(mediaHandler, "entry.dll"), "placeholder");
        await File.WriteAllTextAsync(Path.Combine(mediaHandler, "botnexus-extension.json"), JsonSerializer.Serialize(new ExtensionManifest
        {
            Id = "media-handler-extension",
            Name = "Media Handler",
            Version = "1.0.0",
            EntryAssembly = "entry.dll",
            ExtensionTypes = ["media-handler"]
        }));

        var loader = CreateLoader(new ServiceCollection());

        var discovered = await loader.DiscoverAsync(_rootPath);

        discovered.ShouldContain(x => x.Manifest.Id == "media-handler-extension");
    }

    [Fact]
    public async Task LoadAsync_RegistersDiscoveredTypes_InCollectibleAssemblyLoadContext_AndSupportsUnload()
    {
        var extensionDirectory = Path.Combine(_rootPath, "telegram-extension");
        Directory.CreateDirectory(extensionDirectory);

        var telegramAssembly = ResolveTelegramAssemblyPath();
        var copiedAssemblyName = Path.GetFileName(telegramAssembly);
        File.Copy(telegramAssembly, Path.Combine(extensionDirectory, copiedAssemblyName), overwrite: true);

        await File.WriteAllTextAsync(Path.Combine(extensionDirectory, "botnexus-extension.json"), JsonSerializer.Serialize(new ExtensionManifest
        {
            Id = "telegram-extension",
            Name = "Telegram Extension",
            Version = "1.0.0",
            EntryAssembly = copiedAssemblyName,
            ExtensionTypes = ["channel"]
        }));

        var services = new ServiceCollection();
        var loader = CreateLoader(services);
        var discovered = await loader.DiscoverAsync(_rootPath);

        var result = await loader.LoadAsync(discovered.Single());

        result.Success.ShouldBeTrue();
        result.RegisteredServices.ShouldContain(service => service.StartsWith("IChannelAdapter->", StringComparison.Ordinal));
        var descriptor = services.Single(d => d.ServiceType == typeof(IChannelAdapter));
        descriptor.ImplementationType.ShouldNotBeNull();
        descriptor.ImplementationType!.FullName.ShouldContain("TelegramChannelAdapter");

        var loadContext = AssemblyLoadContext.GetLoadContext(descriptor.ImplementationType.Assembly);
        loadContext.ShouldNotBeNull();
        loadContext!.IsCollectible.ShouldBeTrue();
        loadContext.ShouldNotBe(AssemblyLoadContext.Default);

        loader.GetLoaded().Where(x => x.ExtensionId == "telegram-extension").ShouldHaveSingleItem();
        await loader.UnloadAsync("telegram-extension");
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        loader.GetLoaded().ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadAsync_WithBadAssembly_ReturnsFailure()
    {
        var extensionDirectory = Path.Combine(_rootPath, "bad-assembly");
        Directory.CreateDirectory(extensionDirectory);

        await File.WriteAllTextAsync(Path.Combine(extensionDirectory, "not-an-assembly.dll"), "this is not a dll");
        await File.WriteAllTextAsync(Path.Combine(extensionDirectory, "botnexus-extension.json"), JsonSerializer.Serialize(new ExtensionManifest
        {
            Id = "bad-assembly",
            Name = "Bad Assembly",
            Version = "1.0.0",
            EntryAssembly = "not-an-assembly.dll",
            ExtensionTypes = ["channel"]
        }));

        var loader = CreateLoader(new ServiceCollection());
        var discovered = await loader.DiscoverAsync(_rootPath);

        var result = await loader.LoadAsync(discovered.Single(x => x.Manifest.Id == "bad-assembly"));

        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task DiscoverAsync_WithMissingEntryAssembly_SkipsExtension()
    {
        var extensionDirectory = Path.Combine(_rootPath, "missing-entry");
        Directory.CreateDirectory(extensionDirectory);
        await File.WriteAllTextAsync(Path.Combine(extensionDirectory, "botnexus-extension.json"), JsonSerializer.Serialize(new ExtensionManifest
        {
            Id = "missing-entry",
            Name = "Missing Entry",
            Version = "1.0.0",
            EntryAssembly = "missing.dll",
            ExtensionTypes = ["channel"]
        }));

        var loader = CreateLoader(new ServiceCollection());

        var discovered = await loader.DiscoverAsync(_rootPath);

        discovered.ShouldNotContain(x => x.Manifest.Id == "missing-entry");
    }

    private static AssemblyLoadContextExtensionLoader CreateLoader(IServiceCollection services)
        => new(services, new HookDispatcher(), NullLogger<AssemblyLoadContextExtensionLoader>.Instance, new FileSystem());

    private static string ResolveTelegramAssemblyPath()
    {
        var localCopy = Path.Combine(AppContext.BaseDirectory, "BotNexus.Extensions.Channels.Telegram.dll");
        if (File.Exists(localCopy))
            return localCopy;

        var root = FindRepositoryRoot();
        var fallback = Path.Combine(root, "src", "extensions", "BotNexus.Extensions.Channels.Telegram", "bin", "Debug", "net10.0", "BotNexus.Extensions.Channels.Telegram.dll");
        if (File.Exists(fallback))
            return fallback;

        throw new FileNotFoundException("Unable to locate BotNexus.Extensions.Channels.Telegram.dll for extension loader tests.");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "BotNexus.slnx")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root could not be resolved from test base path.");
    }

    public void Dispose()
    {
        if (!Directory.Exists(_rootPath))
            return;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Directory.Delete(_rootPath, recursive: true);
                return;
            }
            catch (UnauthorizedAccessException)
            {
                if (attempt == 4)
                    return;
                Thread.Sleep(100);
            }
            catch (IOException)
            {
                if (attempt == 4)
                    return;
                Thread.Sleep(100);
            }
        }
    }
}
