namespace XArchiver.Core.Models;

public sealed class ArchivedPostMetadataDocument
{
    public int SchemaVersion { get; set; }

    public ArchivedPostRecord Post { get; set; } = new();
}
