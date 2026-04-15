namespace XArchiver.Core.Models;

public sealed class PreviewPostRecord
{
    public string PostId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public ArchivePostType PostType { get; set; }

    public string? ConversationId { get; set; }

    public string? InReplyToUserId { get; set; }

    public int LikeCount { get; set; }

    public int ReplyCount { get; set; }

    public int RepostCount { get; set; }

    public int QuoteCount { get; set; }

    public bool IsSelected { get; set; }

    public bool IsAlreadyArchived { get; set; }

    public string? RawPayloadJson { get; set; }

    public List<ArchivedReferencedPostRecord> ReferencedPosts { get; set; } = [];

    public List<ArchivedMediaDetailRecord> MediaDetails { get; set; } = [];

    public List<PreviewMediaRecord> Media { get; set; } = [];
}
