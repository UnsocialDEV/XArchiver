namespace XArchiver.Core.Utilities;

public static class ArchiveRangeComposer
{
    public static bool TryComposeUtcRange(
        bool useArchiveRange,
        DateTimeOffset archiveStartDate,
        TimeSpan archiveStartTime,
        DateTimeOffset archiveEndDate,
        TimeSpan archiveEndTime,
        out DateTimeOffset? archiveStartUtc,
        out DateTimeOffset? archiveEndUtc,
        out string? validationError)
    {
        archiveStartUtc = null;
        archiveEndUtc = null;
        validationError = null;

        if (!useArchiveRange)
        {
            return true;
        }

        archiveStartUtc = ComposeUtc(archiveStartDate, archiveStartTime);
        archiveEndUtc = ComposeUtc(archiveEndDate, archiveEndTime);

        if (archiveStartUtc >= archiveEndUtc)
        {
            validationError = "Choose an archive range where the start time is earlier than the stop time.";
            return false;
        }

        return true;
    }

    private static DateTimeOffset ComposeUtc(DateTimeOffset date, TimeSpan time)
    {
        DateTime localTime = date.Date + time;
        DateTime unspecifiedLocalTime = DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecifiedLocalTime, TimeZoneInfo.Local);
    }
}
