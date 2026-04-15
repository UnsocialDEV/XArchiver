using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;

namespace XArchiver.Services;

internal sealed class ScraperDiagnosticsBuffer : IScraperDiagnosticsSink
{
    private readonly Action<ScrapedPostRecord> _discoveredPostAction;
    private readonly Action<ScraperDiagnosticsEvent> _eventAction;
    private readonly Action<ScraperLiveSnapshot> _snapshotAction;

    public ScraperDiagnosticsBuffer(
        string diagnosticsDirectory,
        Action<ScraperDiagnosticsEvent> eventAction,
        Action<ScraperLiveSnapshot> snapshotAction,
        Action<ScrapedPostRecord> discoveredPostAction)
    {
        DiagnosticsDirectory = diagnosticsDirectory;
        _eventAction = eventAction;
        _snapshotAction = snapshotAction;
        _discoveredPostAction = discoveredPostAction;
    }

    public string DiagnosticsDirectory { get; }

    public void ReportDiscoveredPost(ScrapedPostRecord post)
    {
        _discoveredPostAction(post);
    }

    public void ReportEvent(ScraperDiagnosticsEvent diagnosticsEvent)
    {
        _eventAction(diagnosticsEvent);
    }

    public void ReportLiveSnapshot(ScraperLiveSnapshot snapshot)
    {
        _snapshotAction(snapshot);
    }
}
