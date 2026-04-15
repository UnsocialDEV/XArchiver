using XArchiver.Core.Models;

namespace XArchiver.Core.Interfaces;

public interface ISyncPauseGate
{
    event EventHandler<SyncPauseStateChangedEventArgs>? StateChanged;

    bool IsPauseRequested { get; }

    void Pause();

    void ResumeSync();

    Task WaitIfPausedAsync(CancellationToken cancellationToken);
}
