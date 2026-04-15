namespace XArchiver.Core.Models;

public sealed class DiscoveredArchiveRecord
{
    public string ArchiveFolderPath { get; init; } = string.Empty;

    public string ArchiveRootPath { get; init; } = string.Empty;

    public string Username { get; init; } = string.Empty;

    public string? UserId { get; init; }

    public Guid? ProfileId { get; init; }

    public int ArchivedPostCount { get; init; }

    public DateTimeOffset? LatestArchivedPostUtc { get; init; }
}
