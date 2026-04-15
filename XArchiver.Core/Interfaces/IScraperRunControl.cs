namespace XArchiver.Core.Interfaces;

public interface IScraperRunControl
{
    string ConservativeStopReason { get; }

    bool HasConservativeStopRequested { get; }

    bool IsForceKillRequested { get; }

    bool IsStopAndSaveRequested { get; }

    bool ConsumeVisibleBrowserRequest();

    void RequestConservativeStop(string reason);

    void RequestForceKill();

    void RequestStopAndSave();

    void RequestVisibleBrowser();
}
