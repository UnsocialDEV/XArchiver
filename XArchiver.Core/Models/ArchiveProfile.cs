namespace XArchiver.Core.Models;

public sealed class ArchiveProfile
{
    public Guid ProfileId { get; set; } = Guid.NewGuid();

    public string Username { get; set; } = string.Empty;

    public string? UserId { get; set; }

    public ArchiveSourceKind PreferredSource { get; set; } = ArchiveSourceKind.Api;

    public string? ProfileUrl { get; set; }

    public string ArchiveRootPath { get; set; } = string.Empty;

    public int MaxPostsPerSync { get; set; } = 100;

    public int MaxPostsPerWebArchive { get; set; } = 100;

    public bool IncludeOriginalPosts { get; set; } = true;

    public bool IncludeReplies { get; set; } = true;

    public bool IncludeQuotes { get; set; } = true;

    public bool IncludeReposts { get; set; } = true;

    public bool DownloadImages { get; set; } = true;

    public bool DownloadVideos { get; set; } = true;

    public string? LastSinceId { get; set; }

    public DateTimeOffset? LastSuccessfulSyncUtc { get; set; }
}
