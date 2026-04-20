
namespace BotNexus.Gateway.Tests;

public sealed class SerilogConfigurationTests
{
    [Fact]
    public void SerilogRequestLogging_IsConfigured()
    {
        var programPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "gateway", "BotNexus.Gateway.Api", "Program.cs"));

        File.Exists(programPath).ShouldBeTrue();

        var programSource = File.ReadAllText(programPath);
        programSource.ShouldContain("builder.Host.UseSerilog(");
        programSource.ShouldContain("app.UseSerilogRequestLogging();");
    }
}
