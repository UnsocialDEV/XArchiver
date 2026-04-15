namespace XArchiver.Services;

internal enum SensitiveMediaPostOutcomeKind
{
    None,
    Revealed,
    FailedArchiveTextOnly,
    SkippedNoRetry,
}
