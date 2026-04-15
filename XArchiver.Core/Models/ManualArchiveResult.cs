namespace XArchiver.Core.Models;

public sealed class ManualArchiveResult
{
    public int ArchivedPostCount { get; init; }

    public int DownloadedImageCount { get; init; }

    public int DownloadedVideoCount { get; init; }

    public int SkippedAlreadyArchivedCount { get; init; }
}
