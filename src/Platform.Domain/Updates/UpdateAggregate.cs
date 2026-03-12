using Platform.Domain.Common;

namespace Platform.Domain.Updates;

public sealed class UpdateChannelDefinition : EntityBase
{
    public string Code { get; set; } = "stable";

    public string DisplayName { get; set; } = "Стабильный";

    public string Description { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public ICollection<ApplicationRelease> Releases { get; set; } = new List<ApplicationRelease>();
}

public sealed class ApplicationRelease : EntityBase
{
    public string Version { get; set; } = string.Empty;

    public Guid UpdateChannelDefinitionId { get; set; }

    public UpdateChannelDefinition UpdateChannelDefinition { get; set; } = null!;

    public ReleaseState State { get; set; } = ReleaseState.Draft;

    public bool IsMandatory { get; set; }

    public string Summary { get; set; } = string.Empty;

    public string MinimumSupportedVersion { get; set; } = string.Empty;

    public DateTimeOffset? PublishedUtc { get; set; }

    public ICollection<ReleaseArtifact> Artifacts { get; set; } = new List<ReleaseArtifact>();

    public bool IsPublished => State == ReleaseState.Published && PublishedUtc.HasValue;
}

public sealed class ReleaseArtifact : EntityBase
{
    public Guid ApplicationReleaseId { get; set; }

    public ApplicationRelease ApplicationRelease { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";

    public string StoragePath { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public string SignatureBase64 { get; set; } = string.Empty;

    public string SignaturePayload { get; set; } = string.Empty;

    public string SignatureAlgorithm { get; set; } = "RSA-SHA256";

    public bool IsPrimary { get; set; } = true;
}
