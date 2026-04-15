namespace XArchiver.Services;

internal sealed record ScraperFrictionSnapshot
{
    public string Category { get; init; } = string.Empty;

    public int Count { get; init; }

    public string Reason { get; init; } = string.Empty;

    public bool ShouldStop { get; init; }
}
