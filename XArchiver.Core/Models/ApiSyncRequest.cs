namespace XArchiver.Core.Models;

public sealed record ApiSyncRequest
{
    private DateTimeOffset? _archiveEndUtc;

    public DateTimeOffset? ArchiveEndUtc
    {
        get => _archiveEndUtc;
        init => _archiveEndUtc = value;
    }

    public DateTimeOffset? ArchiveStartUtc { get; init; }

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

    public ArchiveProfile Profile { get; init; } = new();
}
