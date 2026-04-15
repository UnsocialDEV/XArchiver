namespace XArchiver.Core.Models;

public sealed class ScrapedPostRecord
{
    public bool ContainsSensitiveMediaWarning { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public List<ScrapedMediaRecord> Media { get; set; } = [];

    public string PostId { get; set; } = string.Empty;

    public string RawHtml { get; set; } = string.Empty;

    public string SensitiveMediaFailureReason { get; set; } = string.Empty;

    public bool SensitiveMediaRevealSucceeded { get; set; }

    public string SourceUrl { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string VideoResolutionFailureReason { get; set; } = string.Empty;

    public string VideoResolutionStatus { get; set; } = string.Empty;
}
