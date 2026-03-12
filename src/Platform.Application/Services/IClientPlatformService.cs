using System.IO;
using Platform.Application.Abstractions;
using Platform.Application.Models;

namespace Platform.Application.Services;

public interface IClientPlatformService
{
    Task<OperationResult<ActivationResponse>> ActivateAsync(
        ActivateLicenseRequest request,
        RequestContext context,
        CancellationToken cancellationToken);

    Task<OperationResult<ActivationResponse>> RefreshAsync(
        RefreshSessionRequest request,
        RequestContext context,
        CancellationToken cancellationToken);

    Task<OperationResult<LicenseSyncResponse>> HeartbeatAsync(
        Guid sessionId,
        HeartbeatRequest request,
        RequestContext context,
        CancellationToken cancellationToken);

    Task<OperationResult<LicenseSyncResponse>> GetStatusAsync(
        Guid sessionId,
        RequestContext context,
        CancellationToken cancellationToken);

    Task<OperationResult> RecordTelemetryAsync(
        Guid sessionId,
        TelemetryBatchRequest request,
        RequestContext context,
        CancellationToken cancellationToken);

    Task<OperationResult<UpdateCheckResponse>> CheckUpdatesAsync(
        Guid sessionId,
        UpdateCheckRequest request,
        RequestContext context,
        CancellationToken cancellationToken);

    Task<OperationResult<(Stream Stream, string FileName, string ContentType)>> OpenReleaseArtifactAsync(
        Guid sessionId,
        Guid releaseId,
        RequestContext context,
        CancellationToken cancellationToken);

    Task<ServerInfoDto> GetServerInfoAsync(CancellationToken cancellationToken);
}
