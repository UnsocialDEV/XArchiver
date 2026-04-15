namespace XArchiver.Services;

internal sealed class SensitiveMediaRevealAttemptTracker : ISensitiveMediaRevealAttemptTracker
{
    private readonly Dictionary<string, SensitiveMediaPostOutcome> _outcomes = new(StringComparer.Ordinal);

    public bool HasTerminalOutcome(string postId)
    {
        return !string.IsNullOrWhiteSpace(postId) && _outcomes.ContainsKey(postId);
    }

    public void MarkFailedArchiveTextOnly(string postId, SensitiveMediaPostOutcome outcome)
    {
        _outcomes[postId] = outcome with
        {
            Kind = SensitiveMediaPostOutcomeKind.FailedArchiveTextOnly,
        };
    }

    public void MarkRevealed(string postId, SensitiveMediaPostOutcome outcome)
    {
        _outcomes[postId] = outcome with
        {
            Kind = SensitiveMediaPostOutcomeKind.Revealed,
        };
    }

    public void MarkSkippedNoRetry(string postId, SensitiveMediaPostOutcome outcome)
    {
        _outcomes[postId] = outcome with
        {
            Kind = SensitiveMediaPostOutcomeKind.SkippedNoRetry,
        };
    }

    public bool TryGetOutcome(string postId, out SensitiveMediaPostOutcome outcome)
    {
        return _outcomes.TryGetValue(postId, out outcome!);
    }
}
