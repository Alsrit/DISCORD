using Microsoft.AspNetCore.Mvc;
using Platform.Application.Abstractions;

namespace Platform.Api.Extensions;

public static class OperationResultHttpExtensions
{
    public static IActionResult ToActionResult(this ControllerBase controller, OperationResult result)
    {
        if (result.Succeeded)
        {
            return controller.Ok(new { success = true, message = result.Message });
        }

        return controller.ToProblemResult(result.Message, result.ErrorCode);
    }

    public static IActionResult ToActionResult<T>(this ControllerBase controller, OperationResult<T> result)
    {
        if (result.Succeeded && result.Data is not null)
        {
            return controller.Ok(result.Data);
        }

        return controller.ToProblemResult(result.Message, result.ErrorCode);
    }

    public static IActionResult ToProblemResult(this ControllerBase controller, string detail, string? errorCode)
    {
        var statusCode = MapStatusCode(errorCode);
        var problem = new ProblemDetails
        {
            Title = errorCode ?? "request_failed",
            Detail = detail,
            Status = statusCode,
            Instance = controller.HttpContext.Request.Path
        };

        problem.Extensions["correlationId"] = controller.HttpContext.TraceIdentifier;
        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            problem.Extensions["errorCode"] = errorCode;
        }

        return controller.StatusCode(statusCode, problem);
    }

    public static int MapStatusCode(string? errorCode) =>
        errorCode switch
        {
            "session_not_found" or "session_expired" => StatusCodes.Status401Unauthorized,
            "license_unavailable" or "device_revoked" or "translation_disabled" => StatusCodes.Status403Forbidden,
            "job_not_found" or "license_not_found" or "device_not_found" or "translation_provider_not_found" => StatusCodes.Status404NotFound,
            "rate_limited" => StatusCodes.Status429TooManyRequests,
            "payload_too_large" => StatusCodes.Status413PayloadTooLarge,
            "unsupported_media_type" or "unsupported_package" => StatusCodes.Status415UnsupportedMediaType,
            "result_not_ready" => StatusCodes.Status409Conflict,
            "quota_files_exceeded" or
            "quota_segments_exceeded" or
            "quota_characters_per_job_exceeded" or
            "quota_characters_per_day_exceeded" or
            "quota_concurrent_jobs_exceeded" or
            "quota_jobs_per_hour_exceeded" or
            "provider_unavailable" or
            "provider_batch_too_large" or
            "provider_throttled" or
            "analysis_snapshot_mismatch" or
            "job_already_finished" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
}
