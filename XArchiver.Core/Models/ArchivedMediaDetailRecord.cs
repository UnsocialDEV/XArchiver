namespace XArchiver.Core.Models;

public sealed class ArchivedMediaDetailRecord
{
    public string MediaKey { get; set; } = string.Empty;

    public string MediaType { get; set; } = string.Empty;

    public string? Url { get; set; }

    public string? PreviewImageUrl { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public long? DurationMs { get; set; }

    public List<ArchivedMediaVariantRecord> Variants { get; set; } = [];
}
