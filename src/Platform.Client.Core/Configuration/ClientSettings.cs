using Platform.Domain.Common;

namespace Platform.Client.Core.Configuration;

public sealed class ClientSettings
{
    public string ServerBaseUrl { get; set; } = "https://194.116.217.48";

    public UpdateChannelCode PreferredChannel { get; set; } = UpdateChannelCode.Stable;

    public bool AutoCheckUpdates { get; set; } = true;

    public bool EnableAutostart { get; set; }

    public bool RequireCertificatePinning { get; set; } = false;

    public List<string> PinnedSpkiSha256 { get; set; } = [];

    public string UpdatePublicKeyPath { get; set; } = string.Empty;

    public string InstallationId { get; set; } = string.Empty;
}
