namespace XArchiver.Services;

public sealed record ScraperBrowserSessionInfo
{
    public string BrowserDisplayName { get; init; } = string.Empty;

    public string BrowserExecutablePath { get; init; } = string.Empty;

    public ScraperBrowserKind BrowserKind { get; init; } = ScraperBrowserKind.Chromium;

    public bool IsInitialized { get; init; }

    public bool IsValidated { get; init; }

    public DateTimeOffset? LastValidatedUtc { get; init; }

    public int? LastLaunchedBrowserProcessId { get; init; }

    public int RemoteDebuggingPort { get; init; }

    public string UserDataDirectory { get; init; } = string.Empty;
}
