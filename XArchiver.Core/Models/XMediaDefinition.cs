namespace XArchiver.Core.Models;

public sealed class XMediaDefinition
{
    public string MediaKey { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string? Url { get; set; }

    public string? PreviewImageUrl { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public long? DurationMs { get; set; }

    public string? RawJson { get; set; }

    public List<XMediaVariant> Variants { get; set; } = [];
}
