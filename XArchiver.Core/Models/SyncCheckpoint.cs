namespace XArchiver.Core.Models;

public sealed class SyncCheckpoint
{
    public string? LastSinceId { get; set; }

    public DateTimeOffset? LastSuccessfulSyncUtc { get; set; }
}
