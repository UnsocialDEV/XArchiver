namespace XArchiver.Services;

internal interface IScraperFrictionMonitor
{
    bool HasStopCondition { get; }

    string StopReason { get; }

    ScraperFrictionSnapshot RecordAuthenticationRequired(string reason);

    ScraperFrictionSnapshot RecordBlockedGate(string reason);

    ScraperFrictionSnapshot RecordRouteRecovery(string route);

    ScraperFrictionSnapshot RecordSensitiveDetailPageOpen(string postId);

    ScraperFrictionSnapshot RecordSensitiveRevealFailure(string postId, string reason);

    ScraperFrictionSnapshot RecordVideoDetailPageOpen(string postId);

    ScraperFrictionSnapshot RecordVideoResolutionFailure(string postId, string reason);
}
