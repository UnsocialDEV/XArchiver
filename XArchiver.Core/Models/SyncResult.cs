namespace XArchiver.Core.Models;

public sealed class SyncResult
{
    public SyncStatus Status { get; init; }

    public int ArchivedPostCount { get; init; }

    public int DownloadedImageCount { get; init; }

    public int DownloadedVideoCount { get; init; }

    public int PartialMediaCount { get; init; }

    public int ScannedPageCount { get; init; }

    public ArchiveProfile? UpdatedProfile { get; init; }

    public string? ErrorMessage { get; init; }
}
