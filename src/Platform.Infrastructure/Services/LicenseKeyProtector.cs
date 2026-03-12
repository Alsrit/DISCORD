using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Platform.Application.Abstractions;
using Platform.Infrastructure.Configuration;

namespace Platform.Infrastructure.Services;

public sealed class LicenseKeyProtector(IOptions<SecurityOptions> options) : ILicenseKeyProtector
{
    private readonly SecurityOptions _options = options.Value;

    public string Hash(string rawLicenseKey)
    {
        if (string.IsNullOrWhiteSpace(rawLicenseKey))
        {
            return string.Empty;
        }

        var normalized = Normalize(rawLicenseKey);
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.LicensePepper));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash);
    }

    public string Mask(string rawLicenseKey)
    {
        var normalized = Normalize(rawLicenseKey);
        var visible = normalized.Length >= 4 ? normalized[^4..] : normalized;
        return $"****-****-****-{visible}";
    }

    public string GetLookupPrefix(string rawLicenseKey)
    {
        var normalized = Normalize(rawLicenseKey);
        return normalized.Length >= 8 ? normalized[..8] : normalized;
    }

    private static string Normalize(string rawLicenseKey) =>
        rawLicenseKey.Trim().Replace("-", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
}
