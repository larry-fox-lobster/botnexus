namespace BotNexus.WebUI.Tests;

internal sealed class PlaywrightFactAttribute : FactAttribute
{
    private const string EnvVar = "BOTNEXUS_RUN_PLAYWRIGHT_E2E";

    public PlaywrightFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(EnvVar), "1", StringComparison.Ordinal))
        {
            Skip = $"Playwright browsers not installed. Set {EnvVar}=1 after running pwsh bin/Debug/net10.0/playwright.ps1 install chromium.";
        }
    }
}
