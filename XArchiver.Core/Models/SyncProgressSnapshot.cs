namespace XArchiver.Core.Models;

public sealed record SyncProgressSnapshot
{
    public int ArchivedPostCount { get; init; }

    public int DownloadedImageCount { get; init; }

    public int DownloadedVideoCount { get; init; }

    public int PartialMediaCount { get; init; }

    public int ScannedPageCount { get; init; }

    public string StageText { get; init; } = string.Empty;

    public int TargetPostCount { get; init; }
}
