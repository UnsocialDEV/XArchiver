namespace XArchiver.Core.Utilities;

public static class ScheduledStartComposer
{
    public static bool TryComposeUtcScheduledStart(
        bool useScheduledStart,
        DateTimeOffset scheduledStartDate,
        TimeSpan scheduledStartTime,
        out DateTimeOffset? scheduledStartUtc,
        out string? validationError)
    {
        scheduledStartUtc = null;
        validationError = null;

        if (!useScheduledStart)
        {
            return true;
        }

        DateTime localTime = scheduledStartDate.Date + scheduledStartTime;
        DateTime unspecifiedLocalTime = DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);
        scheduledStartUtc = TimeZoneInfo.ConvertTimeToUtc(unspecifiedLocalTime, TimeZoneInfo.Local);

        if (!scheduledStartUtc.HasValue || scheduledStartUtc <= DateTimeOffset.UtcNow)
        {
            validationError = "Choose a future date and time for the scheduled run.";
            return false;
        }

        return true;
    }
}
