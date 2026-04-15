namespace XArchiver.Core.Models;

public sealed class PreviewPageResult
{
    public IReadOnlyList<PreviewPostRecord> Posts { get; init; } = [];

    public string? NextToken { get; init; }

    public int ScannedPostReads { get; init; }
}
