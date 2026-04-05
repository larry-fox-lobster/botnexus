using BotNexus.Gateway.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BotNexus.Gateway.Api.Controllers;

[ApiController]
[Route("api/config")]
public sealed class ConfigController : ControllerBase
{
    [HttpGet("validate")]
    public async Task<ActionResult<ConfigValidationResponse>> Validate([FromQuery] string? path, CancellationToken cancellationToken)
    {
        var resolvedPath = string.IsNullOrWhiteSpace(path)
            ? PlatformConfigLoader.DefaultConfigPath
            : Path.GetFullPath(path);

        if (!System.IO.File.Exists(resolvedPath))
        {
            return Ok(new ConfigValidationResponse(
                IsValid: false,
                ConfigPath: resolvedPath,
                Errors:
                [
                    $"Config file not found at '{resolvedPath}'.",
                    "Create ~/.botnexus/config.json (or pass ?path=...) and include gateway/providers/channels/agents sections."
                ]));
        }

        try
        {
            await PlatformConfigLoader.LoadAsync(resolvedPath, cancellationToken);
            return Ok(new ConfigValidationResponse(true, resolvedPath, []));
        }
        catch (OptionsValidationException ex)
        {
            var errors = ex.Failures
                .Where(error => !string.IsNullOrWhiteSpace(error))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(error => error, StringComparer.Ordinal)
                .ToArray();
            return Ok(new ConfigValidationResponse(false, resolvedPath, errors));
        }
    }
}

public sealed record ConfigValidationResponse(bool IsValid, string ConfigPath, IReadOnlyList<string> Errors);
