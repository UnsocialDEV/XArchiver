namespace XArchiver.Core.Models;

public sealed class ArchivedPostMetadataReadResult
{
    public ArchivedPostMetadataDocument? Document { get; init; }

    public bool HasMetadataFile { get; init; }

    public bool IsExtendedMetadata { get; init; }

    public string MetadataFilePath { get; init; } = string.Empty;

    public string RawJson { get; init; } = string.Empty;
}
