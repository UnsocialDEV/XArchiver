namespace XArchiver.Services;

internal sealed class ScraperFrictionMonitor : IScraperFrictionMonitor
{
    private readonly ScraperExecutionPolicy _policy;
    private int _authenticationFailureCount;
    private int _blockedGateCount;
    private int _routeRecoveryCount;
    private int _sensitiveDetailPageCount;
    private int _sensitiveRevealFailureCount;
    private int _videoDetailPageCount;
    private int _videoResolutionFailureCount;

    public ScraperFrictionMonitor(ScraperExecutionPolicy policy)
    {
        _policy = policy;
    }

    public bool HasStopCondition { get; private set; }

    public string StopReason { get; private set; } = string.Empty;

    public ScraperFrictionSnapshot RecordAuthenticationRequired(string reason)
    {
        return Record(
            "Authentication",
            ref _authenticationFailureCount,
            _policy.AuthenticationFailureThreshold,
            $"Conservative mode stopped early after repeated authentication-required pages. {reason}");
    }

    public ScraperFrictionSnapshot RecordBlockedGate(string reason)
    {
        return Record(
            "Gate",
            ref _blockedGateCount,
            _policy.BlockedGateThreshold,
            $"Conservative mode stopped early after repeated blocking gates. {reason}");
    }

    public ScraperFrictionSnapshot RecordRouteRecovery(string route)
    {
        return Record(
            "Navigation",
            ref _routeRecoveryCount,
            _policy.RouteRecoveryThreshold,
            $"Conservative mode stopped early after repeated route recovery events. Last route: {route}");
    }

    public ScraperFrictionSnapshot RecordSensitiveDetailPageOpen(string postId)
    {
        return Record(
            "SensitiveMedia",
            ref _sensitiveDetailPageCount,
            _policy.SensitiveDetailPageThreshold,
            $"Conservative mode stopped early after reaching the sensitive-media detail-page limit. Last post: {postId}");
    }

    public ScraperFrictionSnapshot RecordSensitiveRevealFailure(string postId, string reason)
    {
        return Record(
            "SensitiveMedia",
            ref _sensitiveRevealFailureCount,
            _policy.SensitiveRevealFailureThreshold,
            $"Conservative mode stopped early after repeated sensitive-media reveal failures. Last post: {postId}. {reason}");
    }

    public ScraperFrictionSnapshot RecordVideoDetailPageOpen(string postId)
    {
        return Record(
            "Video",
            ref _videoDetailPageCount,
            _policy.VideoDetailPageThreshold,
            $"Conservative mode stopped early after reaching the video detail-page limit. Last post: {postId}");
    }

    public ScraperFrictionSnapshot RecordVideoResolutionFailure(string postId, string reason)
    {
        return Record(
            "Video",
            ref _videoResolutionFailureCount,
            _policy.VideoResolutionFailureThreshold,
            $"Conservative mode stopped early after repeated video resolution failures. Last post: {postId}. {reason}");
    }

    private ScraperFrictionSnapshot Record(string category, ref int count, int threshold, string stopReason)
    {
        count++;
        if (!HasStopCondition && count >= threshold)
        {
            HasStopCondition = true;
            StopReason = stopReason;
        }

        return new ScraperFrictionSnapshot
        {
            Category = category,
            Count = count,
            Reason = HasStopCondition ? StopReason : string.Empty,
            ShouldStop = HasStopCondition,
        };
    }
}
