namespace XArchiver.Core.Models;

public sealed class XTimelinePage
{
    public IReadOnlyList<ArchivedPostRecord> Posts { get; init; } = [];

    public string? NextToken { get; init; }
}
