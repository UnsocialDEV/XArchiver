using XArchiver.Core.Models;

namespace XArchiver.Core.Services;

public static class ScheduledArchiveRunFactory
{
    public static ScheduledArchiveRunRecord CreateApiSync(ApiSyncRequest request, DateTimeOffset scheduledStartUtc)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new ScheduledArchiveRunRecord
        {
            ApiSyncRequest = request,
            CreatedAtUtc = now,
            ScheduledStartUtc = scheduledStartUtc,
            SourceKind = ScheduledArchiveRunSourceKind.ApiSync,
            State = ScheduledArchiveRunState.Pending,
            StatusText = $"Scheduled API sync for {scheduledStartUtc.LocalDateTime:g}.",
            UpdatedAtUtc = now,
        };
    }

    public static ScheduledArchiveRunRecord CreateWebCapture(WebArchiveRequest request, DateTimeOffset scheduledStartUtc)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new ScheduledArchiveRunRecord
        {
            CreatedAtUtc = now,
            ScheduledStartUtc = scheduledStartUtc,
            SourceKind = ScheduledArchiveRunSourceKind.WebCapture,
            State = ScheduledArchiveRunState.Pending,
            StatusText = $"Scheduled web capture for {scheduledStartUtc.LocalDateTime:g}.",
            UpdatedAtUtc = now,
            WebArchiveRequest = request,
        };
    }
}
