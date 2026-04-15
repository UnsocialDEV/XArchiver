namespace XArchiver.Core.Models;

public sealed class WebArchiveResult
{
    public ArchiveProfile? ArchiveProfile { get; init; }

    public string? ConservativeStopReason { get; init; }

    public int DownloadedImageCount { get; init; }

    public int DownloadedVideoCount { get; init; }

    public string? ErrorMessage { get; init; }

    public bool WasConservativeStop { get; init; }

    public bool WasForceKilled { get; init; }

    public bool WasPartialSave { get; init; }

    public int SavedPostCount { get; init; }

    public bool WasSuccessful { get; init; }
}
