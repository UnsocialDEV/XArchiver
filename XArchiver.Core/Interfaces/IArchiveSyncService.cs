using XArchiver.Core.Models;

namespace XArchiver.Core.Interfaces;

public interface IArchiveSyncService
{
    Task<SyncResult> SyncAsync(
        ApiSyncRequest request,
        IProgress<SyncProgressSnapshot> progress,
        ISyncPauseGate pauseGate,
        CancellationToken cancellationToken);
}
