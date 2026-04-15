namespace XArchiver.Core.Models;

public sealed record ScraperLiveSnapshot
{
    public DateTimeOffset CapturedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string PageTitle { get; init; } = string.Empty;

    public string PageUrl { get; init; } = string.Empty;

    public string ScreenshotPath { get; init; } = string.Empty;

    public string StageText { get; init; } = string.Empty;

    public int VisiblePostCount { get; init; }
}
