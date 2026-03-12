using Platform.Domain.Common;

namespace Platform.Domain.Administration;

public sealed class AdminUser : EntityBase
{
    public string UserName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public AdminRole Role { get; set; } = AdminRole.Auditor;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset? LastLoginUtc { get; set; }
}
