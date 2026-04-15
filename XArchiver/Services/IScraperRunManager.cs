using XArchiver.Core.Models;

namespace XArchiver.Services;

public interface IScraperRunManager
{
    event EventHandler? RunChanged;

    bool ForceKill();

    ScraperRunSnapshot? GetCurrentRun();

    bool OpenLiveBrowser();

    bool Pause();

    bool Resume();

    Task<bool> StartAsync(WebArchiveRequest request, CancellationToken cancellationToken);

    bool StopAndSave();

    bool Stop();
}
