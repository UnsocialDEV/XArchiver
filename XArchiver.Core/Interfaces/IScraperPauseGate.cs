using XArchiver.Core.Models;

namespace XArchiver.Core.Interfaces;

public interface IScraperPauseGate
{
    event EventHandler<ScraperPauseStateChangedEventArgs>? StateChanged;

    bool IsPauseRequested { get; }

    void Pause();

    void ResumeScrape();

    Task WaitIfPausedAsync(CancellationToken cancellationToken);
}
