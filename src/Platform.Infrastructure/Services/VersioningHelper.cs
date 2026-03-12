using Platform.Domain.Common;

namespace Platform.Infrastructure.Services;

internal static class VersioningHelper
{
    public static bool IsGreater(string candidate, string current)
    {
        var candidateVersion = Parse(candidate);
        var currentVersion = Parse(current);
        return candidateVersion > currentVersion;
    }

    public static long Parse(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return 0;
        }

        var parts = version.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(4)
            .Select(value => int.TryParse(value, out var parsed) ? parsed : 0)
            .ToArray();

        Array.Resize(ref parts, 4);
        return ((long)parts[0] << 48) | ((long)parts[1] << 32) | ((long)parts[2] << 16) | (uint)parts[3];
    }

    public static string ToChannelCode(UpdateChannelCode channel) =>
        channel switch
        {
            UpdateChannelCode.Beta => "beta",
            UpdateChannelCode.Internal => "internal",
            _ => "stable"
        };
}
