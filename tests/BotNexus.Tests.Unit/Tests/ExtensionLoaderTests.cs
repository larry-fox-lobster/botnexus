using BotNexus.Core.Abstractions;
using BotNexus.Core.Extensions;
using BotNexus.Tests.Extensions.Convention;
using BotNexus.Tests.Extensions.Registrar;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BotNexus.Tests.Unit.Tests;

public class ExtensionLoaderTests : IDisposable
{
    private readonly string _testRoot;

    public ExtensionLoaderTests()
    {
        _testRoot = Path.Combine(AppContext.BaseDirectory, "extension-loader-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
    }

    [Fact]
    public async Task AddBotNexusExtensions_HappyPath_LoadsAndRegistersConfiguredExtension()
    {
        var result = await ExecuteSingleToolAsync(
            extensionType: "tools",
            extensionKey: "convention",
            extensionAssemblyPath: typeof(ConventionEchoTool).Assembly.Location,
            configValues: new Dictionary<string, string?>
            {
                ["BotNexus:Tools:Extensions:convention:Message"] = "hello"
            });

        result.Should().Be("convention:hello");
    }

    [Fact]
    public void AddBotNexusExtensions_MissingFolder_LogsWarningAndDoesNotThrow()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["BotNexus:ExtensionsPath"] = _testRoot,
            ["BotNexus:Tools:Extensions:missing:Enabled"] = "true"
        });

        var services = new ServiceCollection();
        var logs = CaptureConsole(() => services.AddBotNexusExtensions(config));

        logs.Should().Contain("Extension folder not found");
    }

    [Fact]
    public void AddBotNexusExtensions_EmptyFolder_LogsWarningAndDoesNotThrow()
    {
        Directory.CreateDirectory(Path.Combine(_testRoot, "tools", "empty"));

        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["BotNexus:ExtensionsPath"] = _testRoot,
            ["BotNexus:Tools:Extensions:empty:Enabled"] = "true"
        });

        var services = new ServiceCollection();
        var logs = CaptureConsole(() => services.AddBotNexusExtensions(config));

        logs.Should().Contain("No assemblies found in extension folder");
    }

    [Fact]
    public async Task AddBotNexusExtensions_RegistrarBasedLoading_UsesRegistrarRegistration()
    {
        var result = await ExecuteSingleToolAsync(
            extensionType: "tools",
            extensionKey: "registrar",
            extensionAssemblyPath: typeof(RegistrarEchoTool).Assembly.Location,
            configValues: new Dictionary<string, string?>
            {
                ["BotNexus:Tools:Extensions:registrar:Message"] = "from-registrar"
            });

        result.Should().Be("registrar:from-registrar");
    }

    [Fact]
    public async Task AddBotNexusExtensions_ConventionBasedLoading_RegistersInterfaceImplementations()
    {
        var result = await ExecuteSingleToolAsync(
            extensionType: "tools",
            extensionKey: "convention",
            extensionAssemblyPath: typeof(ConventionEchoTool).Assembly.Location,
            configValues: new Dictionary<string, string?>
            {
                ["BotNexus:Tools:Extensions:convention:Message"] = "from-convention"
            });

        result.Should().Be("convention:from-convention");
    }

    [Fact]
    public void AddBotNexusExtensions_PathTraversalAttempt_IsRejected()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["BotNexus:ExtensionsPath"] = _testRoot,
            ["BotNexus:Tools:Extensions:..\\evil:Enabled"] = "true"
        });

        var services = new ServiceCollection();
        var logs = CaptureConsole(() => services.AddBotNexusExtensions(config));

        logs.Should().Contain("Rejected extension");
    }

    [Fact]
    public async Task AddBotNexusExtensions_ConfigBinding_ExtensionReceivesConfigSection()
    {
        var result = await ExecuteSingleToolAsync(
            extensionType: "tools",
            extensionKey: "convention",
            extensionAssemblyPath: typeof(ConventionEchoTool).Assembly.Location,
            configValues: new Dictionary<string, string?>
            {
                ["BotNexus:Tools:Extensions:convention:Message"] = "config-bound"
            });

        result.Should().Be("convention:config-bound");
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private async Task<string> ExecuteSingleToolAsync(
        string extensionType,
        string extensionKey,
        string extensionAssemblyPath,
        Dictionary<string, string?> configValues)
    {
        var extensionFolder = Path.Combine(_testRoot, extensionType, extensionKey);
        Directory.CreateDirectory(extensionFolder);

        var assemblyFileName = Path.GetFileName(extensionAssemblyPath);
        File.Copy(extensionAssemblyPath, Path.Combine(extensionFolder, assemblyFileName), overwrite: true);

        configValues["BotNexus:ExtensionsPath"] = _testRoot;
        configValues[$"BotNexus:Tools:Extensions:{extensionKey}:Enabled"] = "true";

        var config = BuildConfiguration(configValues);
        var services = new ServiceCollection();
        services.AddBotNexusExtensions(config);

        using var provider = services.BuildServiceProvider();
        var tools = provider.GetServices<ITool>().ToList();
        tools.Should().ContainSingle();
        var result = await tools.Single().ExecuteAsync(new Dictionary<string, object?>());

        var contextStore = provider.GetService<ExtensionLoadContextStore>();
        if (contextStore is not null)
        {
            foreach (var context in contextStore.Contexts)
                context.Unload();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        return result;
    }

    private static string CaptureConsole(Action action)
    {
        var originalOut = Console.Out;
        using var writer = new StringWriter();

        try
        {
            Console.SetOut(writer);
            action();
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            try
            {
                Directory.Delete(_testRoot, recursive: true);
            }
            catch
            {
                // Best-effort cleanup for collectible AssemblyLoadContext file locks.
            }
        }
    }
}
