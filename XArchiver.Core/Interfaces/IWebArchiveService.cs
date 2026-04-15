using XArchiver.Core.Models;

namespace XArchiver.Core.Interfaces;

public interface IWebArchiveService
{
    Task<WebArchiveResult> ArchiveAsync(
        WebArchiveRequest request,
        IProgress<WebArchiveProgressSnapshot> progress,
        IScraperDiagnosticsSink diagnosticsSink,
        IScraperPauseGate pauseGate,
        IScraperRunControl runControl,
        bool preferVisibleBrowser,
        CancellationToken cancellationToken);
}
