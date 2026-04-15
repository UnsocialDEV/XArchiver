namespace XArchiver.Core.Models;

public sealed record ScraperRunSnapshot
{
    public string BlockingReason { get; init; } = string.Empty;

    public DateTimeOffset? CompletedAtUtc { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string CurrentUrl { get; init; } = string.Empty;

    public IReadOnlyList<ScraperDiagnosticsEvent> Events { get; init; } = [];

    public string HtmlSnapshotPath { get; init; } = string.Empty;

    public ScraperLiveSnapshot? LiveSnapshot { get; init; }

    public string PageTitle { get; init; } = string.Empty;

    public IReadOnlyList<ScrapedPostRecord> PreviewPosts { get; init; } = [];

    public WebArchiveProgressSnapshot Progress { get; init; } = new();

    public WebArchiveRequest Request { get; init; } = new();

    public Guid RunId { get; init; } = Guid.NewGuid();

    public ScraperRunState State { get; init; } = ScraperRunState.Idle;

    public string StatusText { get; init; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
