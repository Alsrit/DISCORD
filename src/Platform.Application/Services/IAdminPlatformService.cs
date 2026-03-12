using Microsoft.AspNetCore.Http;
using Platform.Application.Abstractions;
using Platform.Application.Models;

namespace Platform.Application.Services;

public interface IAdminPlatformService
{
    Task<DashboardSummaryDto> GetDashboardAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LicenseListItemDto>> GetLicensesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<DeviceListItemDto>> GetDevicesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AuditEventDto>> GetAuditEventsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ReleaseListItemDto>> GetReleasesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TelemetryListItemDto>> GetTelemetryAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SecurityIncidentDto>> GetSecurityIncidentsAsync(CancellationToken cancellationToken);

    Task<OperationResult<string>> CreateLicenseAsync(
        CreateLicenseRequest request,
        RequestContext context,
        CancellationToken cancellationToken);

    Task<OperationResult> ExtendLicenseAsync(
        ExtendLicenseRequest request,
        RequestContext context,
        CancellationToken cancellationToken);

    Task<OperationResult> RevokeLicenseAsync(
        RevokeLicenseRequest request,
        RequestContext context,
        CancellationToken cancellationToken);

    Task<OperationResult> RevokeDeviceAsync(
        RevokeDeviceRequest request,
        RequestContext context,
        CancellationToken cancellationToken);

    Task<OperationResult> PublishReleaseAsync(
        PublishReleaseRequest request,
        IFormFile package,
        RequestContext context,
        CancellationToken cancellationToken);

    Task<OperationResult<AdminAuthenticationResult>> ValidateAdminCredentialsAsync(
        string userName,
        string password,
        CancellationToken cancellationToken);
}
