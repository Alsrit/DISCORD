using Platform.Application.Models;

namespace Platform.Client.Core.Models;

public enum StellarisModSourceKind
{
    Local = 1,
    Workshop = 2,
    Custom = 3
}

public sealed record StellarisLocalizationEntry(
    string LocalizationKey,
    string SourceText,
    int LineNumber);

public sealed record StellarisLocalizationFile(
    string RelativePath,
    string FullPath,
    string HeaderKey,
    string SourceLanguage,
    int SegmentCount,
    int CharacterCount,
    long SizeBytes,
    string Sha256,
    IReadOnlyCollection<string> Warnings,
    IReadOnlyCollection<StellarisLocalizationEntry> Entries);

public sealed record StellarisModDescriptor(
    string Name,
    string RootPath,
    string DescriptorPath,
    string OriginalReference,
    StellarisModSourceKind SourceKind,
    string SourceLabel,
    string? Version,
    string? SupportedVersion,
    string? RemoteFileId,
    IReadOnlyCollection<string> Tags,
    IReadOnlyCollection<StellarisLocalizationFile> LocalizationFiles);

public sealed record TranslationApiResult<T>(
    bool Succeeded,
    T? Data,
    string Message,
    string? ErrorCode,
    int? StatusCode)
{
    public static TranslationApiResult<T> Success(T data, string message) =>
        new(true, data, message, null, 200);

    public static TranslationApiResult<T> Failure(string message, string? errorCode = null, int? statusCode = null) =>
        new(false, default, message, errorCode, statusCode);
}

public sealed record TranslationApiResult(
    bool Succeeded,
    string Message,
    string? ErrorCode,
    int? StatusCode)
{
    public static TranslationApiResult Success(string message) =>
        new(true, message, null, 200);

    public static TranslationApiResult Failure(string message, string? errorCode = null, int? statusCode = null) =>
        new(false, message, errorCode, statusCode);
}

public sealed record SubmodBuildPreview(
    string SubmodName,
    string OutputRootPath,
    string OutputFolderPath,
    string DescriptorPath,
    string ExternalDescriptorPath,
    string ManifestPath,
    bool OutputFolderExists,
    bool CreateBackup,
    bool DryRun,
    IReadOnlyCollection<string> OutputFiles,
    IReadOnlyCollection<string> Notes);

public sealed record SubmodBuildResult(
    bool Succeeded,
    string Message,
    string OutputFolderPath,
    string DescriptorPath,
    string ExternalDescriptorPath,
    string ManifestPath,
    string? BackupPath);
