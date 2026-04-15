using XArchiver.Core.Models;

namespace XArchiver.ViewModels;

public sealed class ScheduledArchiveRunItemViewModel
{
    public ScheduledArchiveRunItemViewModel(ScheduledArchiveRunRecord run)
    {
        RunId = run.RunId;
        SourceText = run.SourceKind == ScheduledArchiveRunSourceKind.ApiSync ? "API sync" : "Web capture";
        Username = GetUsername(run);
        StateText = GetStateText(run.State);
        ScheduledTimeText = $"Scheduled for {run.ScheduledStartUtc.LocalDateTime:f}";
        ArchiveRangeText = GetArchiveRangeText(run);
        StatusText = run.StatusText;
    }

    public string ArchiveRangeText { get; }

    public Guid RunId { get; }

    public string ScheduledTimeText { get; }

    public string SourceText { get; }

    public string StateText { get; }

    public string StatusText { get; }

    public string Username { get; }

    private static DateTimeOffset? GetArchiveEndUtc(ScheduledArchiveRunRecord run)
    {
        return run.SourceKind == ScheduledArchiveRunSourceKind.ApiSync
            ? run.ApiSyncRequest?.ArchiveEndUtc
            : run.WebArchiveRequest?.ArchiveEndUtc;
    }

    private static DateTimeOffset? GetArchiveStartUtc(ScheduledArchiveRunRecord run)
    {
        return run.SourceKind == ScheduledArchiveRunSourceKind.ApiSync
            ? run.ApiSyncRequest?.ArchiveStartUtc
            : run.WebArchiveRequest?.ArchiveStartUtc;
    }

    private static string GetArchiveRangeText(ScheduledArchiveRunRecord run)
    {
        DateTimeOffset? archiveStartUtc = GetArchiveStartUtc(run);
        DateTimeOffset? archiveEndUtc = GetArchiveEndUtc(run);

        return archiveStartUtc.HasValue && archiveEndUtc.HasValue
            ? $"Archive range {archiveStartUtc.Value.LocalDateTime:g} to {archiveEndUtc.Value.LocalDateTime:g}"
            : "No archive range";
    }

    private static string GetStateText(ScheduledArchiveRunState state)
    {
        return state switch
        {
            ScheduledArchiveRunState.Pending => "Scheduled",
            ScheduledArchiveRunState.WaitingForCapacity => "Waiting",
            ScheduledArchiveRunState.Dispatched => "Dispatched",
            ScheduledArchiveRunState.Failed => "Failed",
            _ => state.ToString(),
        };
    }

    private static string GetUsername(ScheduledArchiveRunRecord run)
    {
        return run.SourceKind == ScheduledArchiveRunSourceKind.ApiSync
            ? $"@{run.ApiSyncRequest?.Profile.Username}"
            : $"@{run.WebArchiveRequest?.Username}";
    }
}
