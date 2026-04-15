namespace XArchiver.Core.Models;

public sealed record WebArchiveProgressSnapshot
{
    public string BlockingReason { get; init; } = string.Empty;

    public int CollectedPostCount { get; init; }

    public string CurrentUrl { get; init; } = string.Empty;

    public int DownloadedImageCount { get; init; }

    public int DownloadedVideoCount { get; init; }

    public string LatestScreenshotPath { get; init; } = string.Empty;

    public string PageTitle { get; init; } = string.Empty;

    public ScraperRunState RunState { get; init; } = ScraperRunState.Running;

    public int SavedPostCount { get; init; }

    public string StageText { get; init; } = string.Empty;

    public int TargetPostCount { get; init; }

    public int VisiblePostCount { get; init; }
}
