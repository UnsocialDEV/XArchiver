using XArchiver.Core.Interfaces;

namespace XArchiver.Core.Services;

public sealed class ScraperRunControl : IScraperRunControl
{
    private string _conservativeStopReason = string.Empty;
    private bool _conservativeStopRequested;
    private bool _forceKillRequested;
    private bool _stopAndSaveRequested;
    private readonly object _syncRoot = new();
    private bool _visibleBrowserRequested;

    public string ConservativeStopReason
    {
        get
        {
            lock (_syncRoot)
            {
                return _conservativeStopReason;
            }
        }
    }

    public bool HasConservativeStopRequested
    {
        get
        {
            lock (_syncRoot)
            {
                return _conservativeStopRequested;
            }
        }
    }

    public bool IsForceKillRequested
    {
        get
        {
            lock (_syncRoot)
            {
                return _forceKillRequested;
            }
        }
    }

    public bool IsStopAndSaveRequested
    {
        get
        {
            lock (_syncRoot)
            {
                return _stopAndSaveRequested;
            }
        }
    }

    public bool ConsumeVisibleBrowserRequest()
    {
        lock (_syncRoot)
        {
            bool wasRequested = _visibleBrowserRequested;
            _visibleBrowserRequested = false;
            return wasRequested;
        }
    }

    public void RequestConservativeStop(string reason)
    {
        lock (_syncRoot)
        {
            _conservativeStopRequested = true;
            _conservativeStopReason = reason;
        }
    }

    public void RequestForceKill()
    {
        lock (_syncRoot)
        {
            _forceKillRequested = true;
        }
    }

    public void RequestStopAndSave()
    {
        lock (_syncRoot)
        {
            _stopAndSaveRequested = true;
        }
    }

    public void RequestVisibleBrowser()
    {
        lock (_syncRoot)
        {
            _visibleBrowserRequested = true;
        }
    }
}
