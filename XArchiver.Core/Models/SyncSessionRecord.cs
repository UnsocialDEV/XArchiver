namespace XArchiver.Core.Models;

public sealed record SyncSessionRecord
{
    public DateTimeOffset? CompletedAtUtc { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public string LatestStatusText { get; init; } = string.Empty;

    public ApiSyncRequest Request { get; init; } = new();

    public ArchiveProfile Profile { get; init; } = new();

    public SyncProgressSnapshot Progress { get; init; } = new();

    public SyncResult? Result { get; init; }

    public Guid SessionId { get; init; }

    public DateTimeOffset? StartedAtUtc { get; init; }

    public SyncSessionState State { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; }
}
