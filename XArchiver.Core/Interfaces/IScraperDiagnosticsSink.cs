using XArchiver.Core.Models;

namespace XArchiver.Core.Interfaces;

public interface IScraperDiagnosticsSink
{
    string DiagnosticsDirectory { get; }

    void ReportDiscoveredPost(ScrapedPostRecord post);

    void ReportEvent(ScraperDiagnosticsEvent diagnosticsEvent);

    void ReportLiveSnapshot(ScraperLiveSnapshot snapshot);
}
