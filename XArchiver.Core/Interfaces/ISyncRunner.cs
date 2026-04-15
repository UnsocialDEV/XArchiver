using XArchiver.Core.Models;

namespace XArchiver.Core.Interfaces;

public interface ISyncRunner
{
    Task<SyncResult> RunAsync(
        ApiSyncRequest request,
        IProgress<SyncProgressSnapshot> progress,
        ISyncPauseGate pauseGate,
        CancellationToken cancellationToken);
}
