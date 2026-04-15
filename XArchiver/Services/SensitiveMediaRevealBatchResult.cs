namespace XArchiver.Services;

internal sealed record SensitiveMediaRevealBatchResult
{
    public int FailedArchiveTextOnlyCount { get; init; }

    public int RevealedCount { get; init; }

    public int SkippedCount { get; init; }
}
