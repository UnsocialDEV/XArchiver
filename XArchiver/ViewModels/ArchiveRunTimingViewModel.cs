using CommunityToolkit.Mvvm.ComponentModel;
using XArchiver.Core.Utilities;

namespace XArchiver.ViewModels;

public sealed class ArchiveRunTimingViewModel : ObservableObject
{
    private DateTimeOffset _archiveEndDate;
    private TimeSpan _archiveEndTime;
    private DateTimeOffset _archiveStartDate;
    private TimeSpan _archiveStartTime;
    private DateTimeOffset _scheduledStartDate;
    private TimeSpan _scheduledStartTime;
    private bool _useArchiveRange;
    private bool _useScheduledStart;

    public ArchiveRunTimingViewModel()
    {
        Reset();
    }

    public DateTimeOffset ArchiveEndDate
    {
        get => _archiveEndDate;
        set => SetProperty(ref _archiveEndDate, value);
    }

    public TimeSpan ArchiveEndTime
    {
        get => _archiveEndTime;
        set => SetProperty(ref _archiveEndTime, value);
    }

    public DateTimeOffset ArchiveStartDate
    {
        get => _archiveStartDate;
        set => SetProperty(ref _archiveStartDate, value);
    }

    public TimeSpan ArchiveStartTime
    {
        get => _archiveStartTime;
        set => SetProperty(ref _archiveStartTime, value);
    }

    public DateTimeOffset ScheduledStartDate
    {
        get => _scheduledStartDate;
        set => SetProperty(ref _scheduledStartDate, value);
    }

    public TimeSpan ScheduledStartTime
    {
        get => _scheduledStartTime;
        set => SetProperty(ref _scheduledStartTime, value);
    }

    public bool UseArchiveRange
    {
        get => _useArchiveRange;
        set => SetProperty(ref _useArchiveRange, value);
    }

    public bool UseScheduledStart
    {
        get => _useScheduledStart;
        set => SetProperty(ref _useScheduledStart, value);
    }

    public void Reset()
    {
        DateTimeOffset now = DateTimeOffset.Now;
        DateTimeOffset today = new(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);
        DateTimeOffset hourAgo = now.AddHours(-1);
        DateTimeOffset nextRun = now.AddMinutes(5);

        ArchiveEndDate = today;
        ArchiveEndTime = now.TimeOfDay;
        ArchiveStartDate = new DateTimeOffset(hourAgo.Year, hourAgo.Month, hourAgo.Day, 0, 0, 0, hourAgo.Offset);
        ArchiveStartTime = hourAgo.TimeOfDay;
        ScheduledStartDate = new DateTimeOffset(nextRun.Year, nextRun.Month, nextRun.Day, 0, 0, 0, nextRun.Offset);
        ScheduledStartTime = nextRun.TimeOfDay;
        UseArchiveRange = false;
        UseScheduledStart = false;
    }

    public bool TryGetArchiveRangeUtc(out DateTimeOffset? archiveStartUtc, out DateTimeOffset? archiveEndUtc, out string? validationError)
    {
        return ArchiveRangeComposer.TryComposeUtcRange(
            UseArchiveRange,
            ArchiveStartDate,
            ArchiveStartTime,
            ArchiveEndDate,
            ArchiveEndTime,
            out archiveStartUtc,
            out archiveEndUtc,
            out validationError);
    }

    public bool TryGetScheduledStartUtc(out DateTimeOffset? scheduledStartUtc, out string? validationError)
    {
        return ScheduledStartComposer.TryComposeUtcScheduledStart(
            UseScheduledStart,
            ScheduledStartDate,
            ScheduledStartTime,
            out scheduledStartUtc,
            out validationError);
    }
}
