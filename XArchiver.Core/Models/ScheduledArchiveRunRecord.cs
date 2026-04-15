namespace XArchiver.Core.Models;

public sealed record ScheduledArchiveRunRecord
{
    public ApiSyncRequest? ApiSyncRequest { get; init; }

    public DateTimeOffset? CompletedAtUtc { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? DispatchedAtUtc { get; init; }

    public Guid RunId { get; init; } = Guid.NewGuid();

    public DateTimeOffset ScheduledStartUtc { get; init; }

    public ScheduledArchiveRunSourceKind SourceKind { get; init; }

    public ScheduledArchiveRunState State { get; init; } = ScheduledArchiveRunState.Pending;

    public string StatusText { get; init; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public WebArchiveRequest? WebArchiveRequest { get; init; }
}
