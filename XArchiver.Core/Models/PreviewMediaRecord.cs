namespace XArchiver.Core.Models;

public sealed class PreviewMediaRecord
{
    public string PostId { get; set; } = string.Empty;

    public string MediaKey { get; set; } = string.Empty;

    public ArchiveMediaKind Kind { get; set; }

    public string SourceUrl { get; set; } = string.Empty;

    public string? PreviewImageUrl { get; set; }

    public bool IsPartial { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public long? DurationMs { get; set; }
}
