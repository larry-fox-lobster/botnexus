using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace BotNexus.Gateway.Api.Controllers;

/// <summary>
/// Gateway lifecycle management endpoints.
/// </summary>
/// <summary>
/// Represents gateway controller.
/// </summary>
[ApiController]
[Route("api/gateway")]
public sealed class GatewayController(IHostApplicationLifetime lifetime) : ControllerBase
{
    /// <summary>
    /// Initiates a graceful shutdown of the gateway process.
    /// The process supervisor (systemd, Docker, dev-loop script, etc.) is expected to restart it.
    /// </summary>
    /// <summary>
    /// Executes shutdown.
    /// </summary>
    /// <returns>The shutdown result.</returns>
    /// <summary>Returns runtime and build information about the running gateway.</summary>
    [HttpGet("info")]
    public IActionResult Info() => Ok(new
    {
        startedAt     = GatewayBuildInfo.StartedAt,
        uptimeSeconds = (long)(DateTimeOffset.UtcNow - GatewayBuildInfo.StartedAt).TotalSeconds,
        commitSha     = GatewayBuildInfo.CommitSha,
        commitShort   = GatewayBuildInfo.CommitShort,
        version       = GatewayBuildInfo.Version
    });

    [HttpPost("shutdown")]
    public IActionResult Shutdown()
    {
        lifetime.StopApplication();
        return Ok(new { status = "shutting_down" });
    }
}
