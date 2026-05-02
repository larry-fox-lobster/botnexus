using BotNexus.Gateway.Updates;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BotNexus.Gateway.Api.Controllers;

/// <summary>
/// Exposes gateway auto-update state and control endpoints.
/// Uses cached state for the status GET to avoid latency; exposes explicit refresh and start actions.
/// </summary>
[ApiController]
[Route("api/gateway/update")]
public sealed class UpdateController(IUpdateCheckService updateCheckService) : ControllerBase
{
    /// <summary>
    /// Returns the cached update status without hitting GitHub.
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType<UpdateStatusResult>(StatusCodes.Status200OK)]
    public ActionResult<UpdateStatusResult> GetStatus()
        => Ok(updateCheckService.GetCurrentStatus());

    /// <summary>
    /// Forces an immediate GitHub poll and returns the refreshed status.
    /// </summary>
    [HttpPost("check")]
    [ProducesResponseType<UpdateStatusResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UpdateStatusResult>> CheckNow(CancellationToken cancellationToken)
        => Ok(await updateCheckService.CheckNowAsync(cancellationToken));

    /// <summary>
    /// Validates prerequisites and spawns the CLI update process if all checks pass.
    /// Returns 202 when started, 409 when already in progress, 412 when config is missing.
    /// </summary>
    [HttpPost("start")]
    [ProducesResponseType<UpdateStartResult>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<UpdateStartResult>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<UpdateStartResult>(StatusCodes.Status412PreconditionFailed)]
    public async Task<ActionResult<UpdateStartResult>> Start(CancellationToken cancellationToken)
    {
        var result = await updateCheckService.StartUpdateAsync(cancellationToken);
        if (result.Started)
            return Accepted(result);
        if (result.Message.Contains("409"))
            return Conflict(result);
        return StatusCode(StatusCodes.Status412PreconditionFailed, result);
    }
}
