using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.Extensions;
using Platform.Application.Models;
using Platform.Application.Services;

namespace Platform.Api.Controllers;

[ApiController]
[Route("api/client/v1")]
[Authorize(AuthenticationSchemes = "ClientBearer")]
public sealed class ClientTranslationController(ITranslationJobService translationJobService) : ControllerBase
{
    [HttpPost("mods/analyze")]
    [Consumes("application/json")]
    [RequestSizeLimit(5_000_000)]
    public async Task<IActionResult> Analyze([FromBody] AnalyzeModRequest request, CancellationToken cancellationToken)
    {
        var result = await translationJobService.AnalyzeAsync(User.GetSessionId(), request, HttpContext.ToRequestContext(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("translations/jobs")]
    [Consumes("application/json")]
    [RequestSizeLimit(5_000_000)]
    public async Task<IActionResult> CreateJob([FromBody] CreateTranslationJobRequest request, CancellationToken cancellationToken)
    {
        var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
        var result = await translationJobService.CreateJobAsync(
            User.GetSessionId(),
            request,
            idempotencyKey,
            HttpContext.ToRequestContext(),
            cancellationToken);

        if (result.Succeeded && result.Data is not null)
        {
            return CreatedAtAction(nameof(GetJob), new { jobId = result.Data.JobId }, result.Data);
        }

        return this.ToActionResult(result);
    }

    [HttpGet("translations/jobs/{jobId:guid}")]
    public async Task<IActionResult> GetJob(Guid jobId, CancellationToken cancellationToken)
    {
        var result = await translationJobService.GetJobAsync(User.GetSessionId(), jobId, HttpContext.ToRequestContext(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("translations/jobs/{jobId:guid}/files")]
    public async Task<IActionResult> GetJobFiles(Guid jobId, CancellationToken cancellationToken)
    {
        var result = await translationJobService.GetJobFilesAsync(User.GetSessionId(), jobId, HttpContext.ToRequestContext(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("translations/jobs/{jobId:guid}/download")]
    public async Task<IActionResult> Download(Guid jobId, CancellationToken cancellationToken)
    {
        var result = await translationJobService.OpenDownloadAsync(User.GetSessionId(), jobId, HttpContext.ToRequestContext(), cancellationToken);
        if (!result.Succeeded)
        {
            return this.ToProblemResult(result.Message, result.ErrorCode);
        }

        return File(result.Data.Stream, result.Data.ContentType, result.Data.FileName);
    }

    [HttpPost("translations/jobs/{jobId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid jobId, CancellationToken cancellationToken)
    {
        var result = await translationJobService.CancelJobAsync(User.GetSessionId(), jobId, HttpContext.ToRequestContext(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("languages")]
    public async Task<IActionResult> GetLanguages(CancellationToken cancellationToken)
    {
        var response = await translationJobService.GetLanguagesAsync(cancellationToken);
        return Ok(response);
    }

    [HttpGet("glossaries/active")]
    public async Task<IActionResult> GetActiveGlossaries(CancellationToken cancellationToken)
    {
        var response = await translationJobService.GetActiveGlossariesAsync(User.GetSessionId(), cancellationToken);
        return Ok(response);
    }

    [HttpGet("quotas/current")]
    public async Task<IActionResult> GetCurrentQuota(CancellationToken cancellationToken)
    {
        var result = await translationJobService.GetCurrentQuotaAsync(User.GetSessionId(), HttpContext.ToRequestContext(), cancellationToken);
        return this.ToActionResult(result);
    }
}
