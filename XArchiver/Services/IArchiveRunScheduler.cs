using XArchiver.Core.Models;

namespace XArchiver.Services;

public interface IArchiveRunScheduler
{
    event EventHandler? ScheduledRunsChanged;

    IReadOnlyList<ScheduledArchiveRunRecord> GetRuns();

    Task InitializeAsync();

    Task<bool> RemoveAsync(Guid runId, CancellationToken cancellationToken);

    Task<ScheduledArchiveRunRecord> ScheduleApiSyncAsync(ApiSyncRequest request, DateTimeOffset scheduledStartUtc, CancellationToken cancellationToken);

    Task<ScheduledArchiveRunRecord> ScheduleWebCaptureAsync(WebArchiveRequest request, DateTimeOffset scheduledStartUtc, CancellationToken cancellationToken);
}
