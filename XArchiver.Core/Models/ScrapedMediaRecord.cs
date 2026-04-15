namespace XArchiver.Core.Models;

public sealed class ScrapedMediaRecord
{
    public long? DurationMs { get; set; }

    public int? Height { get; set; }

    public bool IsPartial { get; set; }

    public ArchiveMediaKind Kind { get; set; }

    public string ManifestUrl { get; set; } = string.Empty;

    public string MediaKey { get; set; } = string.Empty;

    public string PreviewImageUrl { get; set; } = string.Empty;

    public bool RequiresResolution { get; set; }

    public string SourceUrl { get; set; } = string.Empty;

    public int? Width { get; set; }
}
