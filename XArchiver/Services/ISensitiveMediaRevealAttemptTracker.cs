namespace XArchiver.Services;

internal interface ISensitiveMediaRevealAttemptTracker
{
    bool HasTerminalOutcome(string postId);

    void MarkFailedArchiveTextOnly(string postId, SensitiveMediaPostOutcome outcome);

    void MarkRevealed(string postId, SensitiveMediaPostOutcome outcome);

    void MarkSkippedNoRetry(string postId, SensitiveMediaPostOutcome outcome);

    bool TryGetOutcome(string postId, out SensitiveMediaPostOutcome outcome);
}
