using XArchiver.Core.Models;

namespace XArchiver.Core.Interfaces;

public interface IProfileWebScraper
{
    Task<bool> HasSessionAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ScrapedPostRecord>> ScrapeAsync(
        WebArchiveRequest request,
        IProgress<WebArchiveProgressSnapshot> progress,
        IScraperDiagnosticsSink diagnosticsSink,
        IScraperPauseGate pauseGate,
        IScraperRunControl runControl,
        bool preferVisibleBrowser,
        CancellationToken cancellationToken);

    Task<bool> ValidateSessionAsync(string profileUrl, CancellationToken cancellationToken);
}
