namespace Platform.Application.Abstractions;

public sealed record RequestContext(
    string IpAddress,
    string UserAgent,
    string CorrelationId,
    string? AdminUserName = null);
