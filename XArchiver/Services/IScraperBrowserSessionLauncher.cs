namespace XArchiver.Services;

public interface IScraperBrowserSessionLauncher
{
    ScraperBrowserSessionInfo? GetCurrentSession();

    Task<ScraperBrowserSessionInfo> OpenLoginBrowserAsync(ScraperBrowserLaunchOptions options, CancellationToken cancellationToken);

    Task ResetAsync(CancellationToken cancellationToken);

    Task<int> TerminateSessionProcessesAsync(CancellationToken cancellationToken);
}
