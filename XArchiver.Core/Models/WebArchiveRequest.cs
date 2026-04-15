namespace XArchiver.Core.Models;

public sealed class WebArchiveRequest
{
    private DateTimeOffset? _archiveEndUtc;

    public DateTimeOffset? ArchiveEndUtc
    {
        get => _archiveEndUtc;
        init => _archiveEndUtc = value;
    }

    public string ArchiveRootPath { get; init; } = string.Empty;

    public DateTimeOffset? ArchiveStartUtc { get; init; }

    public ScraperExecutionMode ExecutionMode { get; init; } = ScraperExecutionMode.Normal;

    public int MaxPostsToScrape { get; init; }

    public string ProfileUrl { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    [System.Text.Json.Serialization.JsonPropertyName("ArchiveUntilUtc")]
    public DateTimeOffset? LegacyArchiveUntilUtc
    {
        get => null;
        init
        {
            if (!_archiveEndUtc.HasValue)
            {
                _archiveEndUtc = value;
            }
        }
    }

    public string Username { get; init; } = string.Empty;
}
