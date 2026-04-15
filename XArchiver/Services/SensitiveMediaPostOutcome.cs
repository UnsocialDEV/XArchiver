using XArchiver.Core.Models;

namespace XArchiver.Services;

internal sealed record SensitiveMediaPostOutcome
{
    public SensitiveMediaPostOutcomeKind Kind { get; init; } = SensitiveMediaPostOutcomeKind.None;

    public ScrapedPostRecord? PostSnapshot { get; init; }

    public string Reason { get; init; } = string.Empty;

    public string WarningMarker { get; init; } = string.Empty;
}
