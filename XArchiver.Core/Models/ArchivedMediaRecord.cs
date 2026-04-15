namespace XArchiver.Core.Models;

public sealed class ArchivedMediaRecord
{
    public string PostId { get; set; } = string.Empty;

    public string MediaKey { get; set; } = string.Empty;

    public ArchiveMediaKind Kind { get; set; }

    public string SourceUrl { get; set; } = string.Empty;

    public string? RelativePath { get; set; }

    public bool IsPartial { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public long? DurationMs { get; set; }
}
