namespace XArchiver.Services;

internal sealed record ScraperSessionPageState
{
    public bool HasGuestAuthPrompt { get; init; }

    public bool HasPrimaryColumn { get; init; }

    public bool HasSensitiveProfileInterstitial { get; init; }

    public bool HasTimelineContent { get; init; }

    public string Reason { get; init; } = string.Empty;

    public bool RequiresAuthentication { get; init; }
}
