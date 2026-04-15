namespace XArchiver.Services;

public interface IVideoPreviewSegmentPlanner
{
    IReadOnlyList<TimeSpan> BuildSegments(TimeSpan duration);
}
