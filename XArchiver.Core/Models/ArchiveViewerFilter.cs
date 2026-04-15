namespace XArchiver.Core.Models;

public sealed class ArchiveViewerFilter
{
    public bool IncludeOriginalPosts { get; set; } = true;

    public bool IncludeQuotes { get; set; } = true;

    public bool IncludeReplies { get; set; } = true;

    public bool IncludeReposts { get; set; } = true;

    public bool IncludeTextPosts { get; set; } = true;

    public bool IncludeImagePosts { get; set; } = true;

    public bool IncludeVideoPosts { get; set; } = true;

    public string SearchText { get; set; } = string.Empty;
}
