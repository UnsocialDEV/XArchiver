namespace XArchiver.Services;

internal sealed record SensitiveMediaCandidate
{
    public string PostId { get; init; } = string.Empty;

    public string PostUrl { get; init; } = string.Empty;

    public string WarningMarker { get; init; } = string.Empty;
}
