namespace XArchiver.Core.Models;

public sealed class ArchivedGalleryMediaRecord
{
    public DateTimeOffset CreatedAtUtc { get; set; }

    public ArchivedMediaRecord Media { get; set; } = new();

    public string ParentPostId { get; set; } = string.Empty;

    public ArchivePostType PostType { get; set; }

    public string Username { get; set; } = string.Empty;
}
