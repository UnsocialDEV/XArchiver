namespace XArchiver.Services;

public sealed class VideoPreviewSegmentPlanner : IVideoPreviewSegmentPlanner
{
    private const int DefaultSegmentCount = 4;
    private static readonly TimeSpan MinimumDuration = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SegmentWindow = TimeSpan.FromMilliseconds(1400);

    public IReadOnlyList<TimeSpan> BuildSegments(TimeSpan duration)
    {
        if (duration <= MinimumDuration)
        {
            return [TimeSpan.Zero];
        }

        TimeSpan usableDuration = duration - TimeSpan.FromMilliseconds(250);
        if (usableDuration <= TimeSpan.Zero)
        {
            return [TimeSpan.Zero];
        }

        int segmentCount = Math.Min(DefaultSegmentCount, Math.Max(2, (int)Math.Ceiling(duration.TotalSeconds / 6d)));
        List<TimeSpan> segments = new(segmentCount);
        Random random = new(HashCode.Combine(duration.Ticks, Environment.TickCount));

        for (int index = 0; index < segmentCount; index++)
        {
            double bucketStart = usableDuration.TotalMilliseconds * index / segmentCount;
            double bucketEnd = usableDuration.TotalMilliseconds * (index + 1) / segmentCount;
            double latestStart = Math.Max(bucketStart, bucketEnd - SegmentWindow.TotalMilliseconds);
            double selectedStart = latestStart <= bucketStart
                ? bucketStart
                : bucketStart + ((latestStart - bucketStart) * random.NextDouble());

            segments.Add(TimeSpan.FromMilliseconds(Math.Clamp(selectedStart, 0, usableDuration.TotalMilliseconds)));
        }

        return segments;
    }
}
