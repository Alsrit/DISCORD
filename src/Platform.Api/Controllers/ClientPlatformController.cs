using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.Extensions;
using Platform.Application.Abstractions;
using Platform.Application.Models;
using Platform.Application.Services;
using Platform.Domain.Common;

namespace Platform.Api.Controllers;

[ApiController]
[Route("api/client/v1")]
public sealed class ClientPlatformController(IClientPlatformService clientPlatformService) : ControllerBase
{
    [HttpPost("activate")]
    [AllowAnonymous]
    public async Task<IActionResult> Activate([FromBody] ActivateLicenseRequest request, CancellationToken cancellationToken)
    {
        var result = await clientPlatformService.ActivateAsync(request, HttpContext.ToRequestContext(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshSessionRequest request, CancellationToken cancellationToken)
    {
        var result = await clientPlatformService.RefreshAsync(request, HttpContext.ToRequestContext(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("license/status")]
    [Authorize(AuthenticationSchemes = "ClientBearer")]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        var result = await clientPlatformService.GetStatusAsync(User.GetSessionId(), HttpContext.ToRequestContext(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("heartbeat")]
    [Authorize(AuthenticationSchemes = "ClientBearer")]
    public async Task<IActionResult> Heartbeat([FromBody] HeartbeatRequest request, CancellationToken cancellationToken)
    {
        var result = await clientPlatformService.HeartbeatAsync(User.GetSessionId(), request, HttpContext.ToRequestContext(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("telemetry")]
    [Authorize(AuthenticationSchemes = "ClientBearer")]
    public async Task<IActionResult> Telemetry([FromBody] TelemetryBatchRequest request, CancellationToken cancellationToken)
    {
        var result = await clientPlatformService.RecordTelemetryAsync(User.GetSessionId(), request, HttpContext.ToRequestContext(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("updates/check")]
    [Authorize(AuthenticationSchemes = "ClientBearer")]
    public async Task<IActionResult> CheckUpdates(
        [FromQuery] string currentVersion,
        [FromQuery] UpdateChannelCode preferredChannel = UpdateChannelCode.Stable,
        CancellationToken cancellationToken = default)
    {
        var result = await clientPlatformService.CheckUpdatesAsync(
            User.GetSessionId(),
            new UpdateCheckRequest(currentVersion, preferredChannel),
            HttpContext.ToRequestContext(),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("updates/download/{releaseId:guid}")]
    [Authorize(AuthenticationSchemes = "ClientBearer")]
    public async Task<IActionResult> DownloadRelease(Guid releaseId, CancellationToken cancellationToken)
    {
        var result = await clientPlatformService.OpenReleaseArtifactAsync(User.GetSessionId(), releaseId, HttpContext.ToRequestContext(), cancellationToken);
        if (!result.Succeeded)
        {
            return this.ToProblemResult(result.Message, result.ErrorCode);
        }

        var artifact = result.Data;
        return File(artifact.Stream, artifact.ContentType, artifact.FileName);
    }

    [HttpGet("system/info")]
    [AllowAnonymous]
    public async Task<IActionResult> SystemInfo(CancellationToken cancellationToken)
    {
        var info = await clientPlatformService.GetServerInfoAsync(cancellationToken);
        return Ok(info);
    }
}
